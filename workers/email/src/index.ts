// Cloudflare Worker `miautrix-main-worker`. Owns both halves of the external mail path.
//
// Baseline: version 327baddf-2d9d-494f-9630-e383c4ca4aa4 (number 6, deployed 2026-09-21T19:33:11Z),
// recovered into this repo by MIA-69. MIA-76 changes the OUTBOUND `POST /send` handler only:
// authentication, request validation and error mapping. The INBOUND `email()` handler below is
// left byte-for-byte as recovered — its two defects are a separate task with its own dependency.
//
// Contract `POST /send` now honours, and the only contract the app may rely on:
//
//   401  missing or wrong bearer token            (checked BEFORE the body is read)
//   400  unreadable or invalid body               ({ok:false,error})
//   200  accepted and handed to env.EMAIL.send()  ({ok:true})
//   500  env.EMAIL.send() failed, or anything unforeseen ({ok:false,error})
//   503  the Worker has no SEND_TOKEN secret configured — fail closed, never fail open
//
// No request shape may produce a Cloudflare `1101`. `1101` is what the runtime returns when a
// handler throws uncaught, so every path below is inside a guard and the whole handler is
// wrapped in a final catch.

import { EmailMessage } from "cloudflare:email";

interface Env {
  // `send_email` binding, declared in wrangler.jsonc. Grants env.EMAIL.send().
  EMAIL: { send(message: EmailMessage): Promise<void> };

  // Shared bearer token the app must present on POST /send. Set as a Worker SECRET
  // (`npx wrangler secret put SEND_TOKEN`) — deliberately absent from wrangler.jsonc and from
  // this file. Configuration at runtime: the value is injected, never committed.
  //
  // Optional in the type, not in the behaviour: when it is missing the handler returns 503 and
  // refuses the request. An unset secret must never mean "allow everyone".
  SEND_TOKEN?: string;
}

interface SendRequestBody {
  from: string;
  to: string;
  // Base64-encoded raw MIME, as produced by CloudflareApiMailTransport.SendAsync.
  raw: string;
}

/** Uniform error envelope. Same `{ok:false,error}` shape the 500 path already used. */
function fail(status: number, error: string): Response {
  return Response.json({ ok: false, error }, { status });
}

/**
 * Length-independent-ish constant-time string compare. Workers exposes no timingSafeEqual, so
 * compare every byte of a fixed-length window and fold the length difference into the result
 * rather than returning early on the first mismatch.
 */
function tokensMatch(presented: string, expected: string): boolean {
  let diff = presented.length ^ expected.length;
  const span = Math.max(presented.length, expected.length);
  for (let i = 0; i < span; i++) {
    diff |= (presented.charCodeAt(i) || 0) ^ (expected.charCodeAt(i) || 0);
  }
  return diff === 0;
}

/** Strict base64: the alphabet, correct padding, and a length that is a multiple of four. */
const BASE64_RE = /^[A-Za-z0-9+/]*={0,2}$/;

function decodeBase64(value: string): string {
  if (value.length === 0 || value.length % 4 !== 0 || !BASE64_RE.test(value)) {
    throw new Error("not base64");
  }
  // atob still throws on some inputs the regex admits, so the caller keeps this in a try.
  return atob(value);
}

/**
 * Minimal address sanity check. Deliberately not an RFC 5322 parser — the Worker is transport
 * only. It rejects what would make `new EmailMessage()` throw, and leaves real validation to
 * the app, which owns MIME.
 */
function isPlausibleAddress(value: unknown): value is string {
  if (typeof value !== "string") return false;
  const trimmed = value.trim();
  if (trimmed.length === 0 || trimmed.length > 320) return false;
  if (/[\s<>,;]/.test(trimmed)) return false;
  const at = trimmed.indexOf("@");
  return at > 0 && at === trimmed.lastIndexOf("@") && at < trimmed.length - 1;
}

export default {
  // INBOUND: sender -> MX (Cloudflare Email Routing) -> this handler.
  //
  // UNCHANGED by MIA-76, intentionally. Two known defects, documented in ERRORS_AND_ISSUES.md
  // and fixed by a separate task:
  //   1. `indexOf` is an exact string compare, so the "*@miautrix.tech" wildcard never matches
  //      a real sender. Only the literal "admin@miautrix.org" can pass.
  //   2. "inbox@corp" is not a routable address and not a verified Email Routing destination,
  //      so the forward cannot succeed. The architecture calls for
  //      POST /api/v1/inbound/cloudflare into the app instead.
  async email(message: ForwardableEmailMessage, env: Env, ctx: ExecutionContext): Promise<void> {
    const allowList = ["*@miautrix.tech", "admin@miautrix.org"];
    if (allowList.indexOf(message.from) == -1) {
      message.setReject("Address not allowed");
    } else {
      await message.forward("inbox@corp");
    }
  },

  // OUTBOUND: app -> POST /send -> env.EMAIL.send() -> recipient.
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    try {
      const url = new URL(request.url);

      if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/health")) {
        return Response.json({ ok: true });
      }

      if (request.method === "POST" && url.pathname === "/send") {
        // ---- 1. Authenticate FIRST. Nothing about the body is read before this passes, so a
        // bad token on an unreadable body still answers 401, not 400. An unauthenticated caller
        // learns nothing about request validation.
        const expected = env.SEND_TOKEN;
        if (typeof expected !== "string" || expected.length === 0) {
          // Fail closed. Misconfiguration is a server fault, and it is not a 401: saying
          // "unauthorized" to a correct caller would send an operator hunting the wrong bug.
          return fail(503, "Worker is not configured for authenticated sending.");
        }

        const header = request.headers.get("Authorization") ?? "";
        const prefix = "Bearer ";
        const presented = header.startsWith(prefix) ? header.slice(prefix.length).trim() : "";
        if (presented.length === 0 || !tokensMatch(presented, expected)) {
          return fail(401, "Missing or invalid bearer token.");
        }

        // ---- 2. Parse and validate INSIDE a guard. Every statement that the recovered code ran
        // unguarded lives here: request.json(), atob(raw), and the EmailMessage constructor.
        let message: EmailMessage;
        try {
          const body = (await request.json()) as Partial<SendRequestBody> | null;

          if (body === null || typeof body !== "object" || Array.isArray(body)) {
            return fail(400, "Body must be a JSON object with from, to and raw.");
          }
          if (!isPlausibleAddress(body.from)) {
            return fail(400, "Field 'from' is missing or is not a valid email address.");
          }
          if (!isPlausibleAddress(body.to)) {
            return fail(400, "Field 'to' is missing or is not a valid email address.");
          }
          if (typeof body.raw !== "string" || body.raw.length === 0) {
            return fail(400, "Field 'raw' is missing or is not a base64 string.");
          }

          let rawMime: string;
          try {
            rawMime = decodeBase64(body.raw);
          } catch {
            return fail(400, "Field 'raw' is not valid base64.");
          }
          if (rawMime.length === 0) {
            return fail(400, "Field 'raw' decoded to an empty message.");
          }

          message = new EmailMessage(body.from.trim(), body.to.trim(), rawMime);
        } catch (err) {
          // A non-JSON body lands here (SyntaxError from request.json()), as does any
          // constructor rejection the checks above did not anticipate. Client error, so 400 —
          // and never an uncaught throw, so never 1101.
          return fail(400, `Invalid request body: ${String(err)}`);
        }

        // ---- 3. The send itself. A transport failure is not the client's fault: 500, with the
        // same envelope the recovered handler already returned.
        try {
          await env.EMAIL.send(message);
          return Response.json({ ok: true });
        } catch (err) {
          return fail(500, String(err));
        }
      }

      return new Response("Not found", { status: 404 });
    } catch (err) {
      // Last line of defence. Nothing reaching here should be reachable, which is exactly why
      // it is here: an uncaught throw is the 1101 this task exists to eliminate.
      return fail(500, `Unhandled worker error: ${String(err)}`);
    }
  },
};
