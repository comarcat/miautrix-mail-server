// Cloudflare Worker `miautrix-main-worker`. Owns both halves of the external mail path.
//
// Baseline: version 327baddf-2d9d-494f-9630-e383c4ca4aa4 (number 6, deployed 2026-09-21T19:33:11Z),
// recovered into this repo by MIA-69. Reconciled under MIA-76 to align with live production
// contract (version 8, 51e41cc4-db42-4e85-99ac-d0cef1bbdceb).
//
// MIA-76 changes the OUTBOUND `POST /send` handler only: authentication, media-type validation,
// payload limits, MIME validation, and error mapping. The INBOUND `email()` handler below is
// left byte-for-byte as recovered — its remediation belongs to MIA-77.
//
// Contract `POST /send` honours:
//
//   401  missing or wrong bearer token (checked BEFORE the body is read)
//   400  unreadable or invalid JSON body, missing fields, invalid address, invalid MIME
//   413  payload exceeds 25 MB decoded limit
//   415  unsupported media type (non-JSON Content-Type)
//   202  accepted and handed to env.EMAIL.send() ({ok:true})
//   502  env.EMAIL.send() failed ({ok:false,error:"send_failed",detail:...})
//   503  missing SEND_TOKEN secret or EMAIL binding — fail closed, never open
//   500  unhandled worker error
//
// No request shape may produce a Cloudflare `1101`.

import { EmailMessage } from "cloudflare:email";

interface Env {
  // `send_email` binding, declared in wrangler.jsonc. Grants env.EMAIL.send().
  EMAIL: { send(message: EmailMessage): Promise<void> };

  // Shared bearer token the app must present on POST /send. Set as a Worker SECRET
  // (`npx wrangler secret put SEND_TOKEN`) — deliberately absent from wrangler.jsonc.
  SEND_TOKEN?: string;
}

interface SendRequestBody {
  from: string;
  to: string;
  // Base64-encoded raw MIME, as produced by CloudflareApiMailTransport.SendAsync.
  raw: string;
}

const MAX_RAW_BYTES = 25 * 1024 * 1024;

function jsonError(status: number, error: string, detail?: string): Response {
  return Response.json(detail ? { ok: false, error, detail } : { ok: false, error }, {
    status,
    headers: { "cache-control": "no-store" },
  });
}

function timingSafeEqual(a: string, b: string): boolean {
  const encoder = new TextEncoder();
  const left = encoder.encode(a);
  const right = encoder.encode(b);
  if (left.length !== right.length) return false;
  let diff = 0;
  for (let i = 0; i < left.length; i += 1) {
    diff |= left[i] ^ right[i];
  }
  return diff === 0;
}

function looksLikeAddress(value: unknown): value is string {
  if (typeof value !== "string") return false;
  const trimmed = value.trim();
  if (trimmed.length < 3 || trimmed.length > 320) return false;
  if (/[\s<>",;]/.test(trimmed)) return false;
  const at = trimmed.indexOf("@");
  return at > 0 && at === trimmed.lastIndexOf("@") && at < trimmed.length - 1 && trimmed.includes(".", at);
}

function decodeBase64(raw: string): Uint8Array | null {
  if (!/^[A-Za-z0-9+/\r\n]*={0,2}$/.test(raw)) return null;
  try {
    const binary = atob(raw.replace(/[\r\n]/g, ""));
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
  } catch {
    return null;
  }
}

function authorize(request: Request, env: Env): Response | null {
  const expected = env.SEND_TOKEN;
  if (typeof expected !== "string" || expected.length === 0) {
    return jsonError(503, "send_disabled", "SEND_TOKEN is not configured on this Worker.");
  }
  const header = request.headers.get("authorization") ?? "";
  const match = /^Bearer\s+(.+)$/i.exec(header.trim());
  if (!match) {
    return jsonError(401, "unauthorized", "Expected an Authorization: Bearer header.");
  }
  if (!timingSafeEqual(match[1].trim(), expected)) {
    return jsonError(401, "unauthorized");
  }
  return null;
}

async function handleSend(request: Request, env: Env): Promise<Response> {
  // 1. Authenticate FIRST.
  const denied = authorize(request, env);
  if (denied) return denied;

  // 2. Media type check.
  const contentType = request.headers.get("content-type") ?? "";
  if (contentType && !contentType.toLowerCase().includes("json")) {
    return jsonError(415, "unsupported_media_type", "Send application/json.");
  }

  // 3. Body parse & structural validation.
  let body: Partial<SendRequestBody> | null;
  try {
    body = (await request.json()) as Partial<SendRequestBody> | null;
  } catch {
    return jsonError(400, "invalid_json", "Request body is not valid JSON.");
  }

  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return jsonError(400, "invalid_body", "Expected a JSON object with from, to and raw.");
  }

  const { from, to, raw } = body;
  const missing = (["from", "to", "raw"] as const).filter(
    (field) => typeof body[field] !== "string" || (body[field] as string).trim().length === 0
  );
  if (missing.length > 0) {
    return jsonError(400, "missing_fields", `Required string field(s): ${missing.join(", ")}.`);
  }

  const sender = (from as string).trim();
  const recipient = (to as string).trim();
  if (!looksLikeAddress(sender)) return jsonError(400, "invalid_from", "from is not an email address.");
  if (!looksLikeAddress(recipient)) return jsonError(400, "invalid_to", "to is not an email address.");

  const rawBase64 = (raw as string).trim();
  if (rawBase64.length > Math.ceil((MAX_RAW_BYTES * 4) / 3)) {
    return jsonError(413, "payload_too_large", `raw exceeds ${MAX_RAW_BYTES} bytes decoded.`);
  }

  const decoded = decodeBase64(rawBase64);
  if (decoded === null) return jsonError(400, "invalid_raw", "raw is not valid base64.");
  if (decoded.byteLength === 0) return jsonError(400, "invalid_raw", "raw decodes to an empty message.");
  if (decoded.byteLength > MAX_RAW_BYTES) {
    return jsonError(413, "payload_too_large", `raw exceeds ${MAX_RAW_BYTES} bytes decoded.`);
  }

  let rawMime: string;
  try {
    rawMime = new TextDecoder("utf-8", { fatal: false }).decode(decoded);
  } catch {
    return jsonError(400, "invalid_raw", "raw does not decode to text.");
  }

  if (!/\r?\n\r?\n/.test(rawMime)) {
    return jsonError(
      400,
      "invalid_mime",
      "raw must be an RFC 5322 message: headers, a blank line, then the body."
    );
  }

  const email = env.EMAIL;
  if (!email || typeof email.send !== "function") {
    return jsonError(503, "email_binding_missing", "This Worker has no send_email binding named EMAIL.");
  }

  try {
    await email.send(new EmailMessage(sender, recipient, rawMime));
    return Response.json({ ok: true }, { status: 202, headers: { "cache-control": "no-store" } });
  } catch (err) {
    const detail = err instanceof Error ? err.message : String(err);
    console.error("send_failed", detail);
    return jsonError(502, "send_failed", detail);
  }
}

export default {
  // INBOUND: sender -> MX (Cloudflare Email Routing) -> this handler.
  //
  // UNCHANGED by MIA-76, intentionally. Two known defects, documented in ERRORS_AND_ISSUES.md
  // and fixed by MIA-77:
  //   1. `indexOf` is an exact string compare, so the "*@miautrix.tech" wildcard never matches
  //      a real sender. Only the literal "admin@miautrix.org" can pass.
  //   2. "inbox@corp" is not a routable address and not a verified Email Routing destination.
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
      const path = url.pathname.replace(/\/+$/, "") || "/";

      if (path === "/" || path === "/health") {
        if (request.method !== "GET" && request.method !== "HEAD") {
          return jsonError(405, "method_not_allowed", "Use GET.");
        }
        return Response.json({ ok: true }, { headers: { "cache-control": "no-store" } });
      }

      if (path === "/send") {
        if (request.method !== "POST") {
          return jsonError(405, "method_not_allowed", "Use POST.");
        }
        return await handleSend(request, env);
      }

      return jsonError(404, "not_found");
    } catch (err) {
      const detail = err instanceof Error ? err.message : String(err);
      console.error("unhandled", detail);
      return jsonError(500, "internal_error");
    }
  },
};
