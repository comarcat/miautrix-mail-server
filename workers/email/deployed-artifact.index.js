var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/index.ts
import { EmailMessage } from "cloudflare:email";
var DEFAULT_ALLOWED_SENDER_PATTERNS = "*@miautrix.tech,admin@miautrix.org";
var DEFAULT_INBOUND_FORWARD_TO = "inbox@corp";
var MAX_RAW_BYTES = 25 * 1024 * 1024;
function jsonError(status, error, detail) {
  return Response.json(detail ? { ok: false, error, detail } : { ok: false, error }, {
    status,
    headers: { "cache-control": "no-store" }
  });
}
__name(jsonError, "jsonError");
function timingSafeEqual(a, b) {
  const encoder = new TextEncoder();
  const left = encoder.encode(a);
  const right = encoder.encode(b);
  if (left.length !== right.length) return false;
  let diff = 0;
  for (let i = 0; i < left.length; i += 1) diff |= left[i] ^ right[i];
  return diff === 0;
}
__name(timingSafeEqual, "timingSafeEqual");
function senderAllowed(from, patterns) {
  const candidate = from.trim().toLowerCase();
  return patterns.split(",").map((pattern) => pattern.trim().toLowerCase()).filter((pattern) => pattern.length > 0).some(
    (pattern) => pattern.startsWith("*@") ? candidate.endsWith(pattern.slice(1)) : candidate === pattern
  );
}
__name(senderAllowed, "senderAllowed");
function looksLikeAddress(value) {
  if (value.length < 3 || value.length > 320) return false;
  if (/[\s<>",;]/.test(value)) return false;
  const at = value.indexOf("@");
  return at > 0 && at === value.lastIndexOf("@") && at < value.length - 1 && value.includes(".", at);
}
__name(looksLikeAddress, "looksLikeAddress");
function decodeBase64(raw) {
  if (!/^[A-Za-z0-9+/\r\n]*={0,2}$/.test(raw)) return null;
  try {
    const binary = atob(raw.replace(/[\r\n]/g, ""));
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) bytes[i] = binary.charCodeAt(i);
    return bytes;
  } catch {
    return null;
  }
}
__name(decodeBase64, "decodeBase64");
function authorize(request, env) {
  const expected = env.SEND_TOKEN;
  if (typeof expected !== "string" || expected.length === 0) {
    return jsonError(503, "send_disabled", "SEND_TOKEN is not configured on this Worker.");
  }
  const header = request.headers.get("authorization") ?? "";
  const match = /^Bearer\s+(.+)$/i.exec(header.trim());
  if (!match) return jsonError(401, "unauthorized", "Expected an Authorization: Bearer header.");
  if (!timingSafeEqual(match[1].trim(), expected)) return jsonError(401, "unauthorized");
  return null;
}
__name(authorize, "authorize");
async function handleSend(request, env) {
  const denied = authorize(request, env);
  if (denied) return denied;
  const contentType = request.headers.get("content-type") ?? "";
  if (contentType && !contentType.toLowerCase().includes("json")) {
    return jsonError(415, "unsupported_media_type", "Send application/json.");
  }
  let body;
  try {
    body = await request.json();
  } catch {
    return jsonError(400, "invalid_json", "Request body is not valid JSON.");
  }
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return jsonError(400, "invalid_body", "Expected a JSON object with from, to and raw.");
  }
  const { from, to, raw } = body;
  const missing = ["from", "to", "raw"].filter(
    (field) => typeof body[field] !== "string" || body[field].trim().length === 0
  );
  if (missing.length > 0) {
    return jsonError(400, "missing_fields", `Required string field(s): ${missing.join(", ")}.`);
  }
  const sender = from.trim();
  const recipient = to.trim();
  if (!looksLikeAddress(sender)) return jsonError(400, "invalid_from", "from is not an email address.");
  if (!looksLikeAddress(recipient)) return jsonError(400, "invalid_to", "to is not an email address.");
  const rawBase64 = raw.trim();
  if (rawBase64.length > Math.ceil(MAX_RAW_BYTES * 4 / 3)) {
    return jsonError(413, "payload_too_large", `raw exceeds ${MAX_RAW_BYTES} bytes decoded.`);
  }
  const decoded = decodeBase64(rawBase64);
  if (decoded === null) return jsonError(400, "invalid_raw", "raw is not valid base64.");
  if (decoded.byteLength === 0) return jsonError(400, "invalid_raw", "raw decodes to an empty message.");
  if (decoded.byteLength > MAX_RAW_BYTES) {
    return jsonError(413, "payload_too_large", `raw exceeds ${MAX_RAW_BYTES} bytes decoded.`);
  }
  let rawMime;
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
__name(handleSend, "handleSend");
var index_default = {
  /**
   * Inbound. Preserved behaviour from the recovered deployed Worker: allow-list the
   * envelope sender, then forward to the verified destination. Only the hard-coded
   * values became configuration.
   */
  async email(message, env) {
    const patterns = env.ALLOWED_SENDER_PATTERNS ?? DEFAULT_ALLOWED_SENDER_PATTERNS;
    if (!senderAllowed(message.from, patterns)) {
      message.setReject("Address not allowed");
      return;
    }
    await message.forward(env.INBOUND_FORWARD_TO ?? DEFAULT_INBOUND_FORWARD_TO);
  },
  async fetch(request, env) {
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
        if (request.method !== "POST") return jsonError(405, "method_not_allowed", "Use POST.");
        return await handleSend(request, env);
      }
      return jsonError(404, "not_found");
    } catch (err) {
      const detail = err instanceof Error ? err.message : String(err);
      console.error("unhandled", detail);
      return jsonError(500, "internal_error");
    }
  }
};
export {
  index_default as default
};
//# sourceMappingURL=index.js.map