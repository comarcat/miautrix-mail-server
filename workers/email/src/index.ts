// Recovered verbatim from the deployed Cloudflare Worker `miautrix-main-worker`,
// version 327baddf-2d9d-494f-9630-e383c4ca4aa4 (number 6, deployed 2026-09-21T19:33:11Z).
//
// MIA-69 is recovery and version control only. Behaviour is NOT changed here, and this
// file is NOT redeployed by this task. The `POST /send` defects annotated below are
// diagnosis for the follow-up fix task; do not fix them in this file without that task.
//
// The deployed artefact was a single JavaScript module (`index.js`). It is stored here as
// TypeScript because the upstream repo named `src/index.ts`, and because `wrangler` compiles
// TS to the same module shape. The only edits applied to the deployed bytes are these
// comments and the type annotations; no statement was added, removed or reordered.

import { EmailMessage } from "cloudflare:email";

interface Env {
  // `send_email` binding, declared in wrangler.jsonc. Grants env.EMAIL.send().
  EMAIL: { send(message: EmailMessage): Promise<void> };
}

interface SendRequestBody {
  from: string;
  to: string;
  // Base64-encoded raw MIME, as produced by CloudflareApiMailTransport.SendAsync.
  raw: string;
}

export default {
  // INBOUND: sender -> MX (Cloudflare Email Routing) -> this handler.
  async email(message: ForwardableEmailMessage, env: Env, ctx: ExecutionContext): Promise<void> {
    // DEFECT (inbound, documented not fixed): `indexOf` is an exact string compare, so the
    // "*@miautrix.tech" wildcard never matches any real sender. Only the literal
    // "admin@miautrix.org" can pass. Everything else is rejected.
    const allowList = ["*@miautrix.tech", "admin@miautrix.org"];
    if (allowList.indexOf(message.from) == -1) {
      message.setReject("Address not allowed");
    } else {
      // DEFECT (inbound, documented not fixed): "inbox@corp" is not a routable address and
      // is not a verified Email Routing destination. This forward cannot succeed as written.
      // The architecture calls for POST /api/v1/inbound/cloudflare into the app instead.
      await message.forward("inbox@corp");
    }
  },

  // OUTBOUND: app -> POST /send -> env.EMAIL.send() -> recipient.
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/health")) {
      return Response.json({ ok: true });
    }

    if (request.method === "POST" && url.pathname === "/send") {
      // ROOT CAUSE of Cloudflare 1101 on every request shape (see README.md):
      // these three statements sit OUTSIDE the try block, so any throw here is uncaught
      // and the runtime returns 1101 rather than a 4xx.
      //   1. request.json() throws on a non-JSON body.
      //   2. atob(body.raw) throws InvalidCharacterError when `raw` is absent
      //      (atob(undefined) -> "undefined" is not valid base64) or not base64.
      //   3. new EmailMessage(...) throws on a missing/invalid from or to.
      // There is also no bearer-token check anywhere in this handler, which is why a wrong
      // token returns 1101 instead of 401: the request is never authenticated, it just
      // reaches the same unguarded parse and throws.
      const body = (await request.json()) as SendRequestBody;
      const rawMime = atob(body.raw);
      const message = new EmailMessage(body.from, body.to, rawMime);

      try {
        await env.EMAIL.send(message);
        return Response.json({ ok: true });
      } catch (err) {
        return Response.json({ ok: false, error: String(err) }, { status: 500 });
      }
    }

    return new Response("Not found", { status: 404 });
  },
};
