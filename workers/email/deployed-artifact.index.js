import { EmailMessage } from "cloudflare:email";

export default {
  async email(message, env, ctx) {
    const allowList = ["*@miautrix.tech", "admin@miautrix.org"];
    if (allowList.indexOf(message.from) == -1) {
      message.setReject("Address not allowed");
    } else {
      await message.forward("inbox@corp");
    }
  },
  async fetch(request, env, ctx) {
    const url = new URL(request.url);

    if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/health")) {
      return Response.json({ ok: true });
    }

    if (request.method === "POST" && url.pathname === "/send") {
      const body = await request.json();
      const rawMime = atob(body.raw);
      const message = new EmailMessage(body.from, body.to, rawMime);

      try {
        await env.EMAIL.send(message);
        return Response.json({ ok: true });
      } catch (err) {
        return Response.json(
          { ok: false, error: String(err) },
          { status: 500 }
        );
      }
    }

    return new Response("Not found", { status: 404 });
  },
};