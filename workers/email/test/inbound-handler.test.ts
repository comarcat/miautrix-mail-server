import test from "node:test";
import assert from "node:assert/strict";
import worker, { isSenderAllowed } from "../src/index.ts";

test("isSenderAllowed: wildcard matching on tenant domain", () => {
  assert.equal(isSenderAllowed("someone@miautrix.tech"), true);
  assert.equal(isSenderAllowed("alex.vance@miautrix.tech"), true);
  assert.equal(isSenderAllowed("support+tag@miautrix.tech"), true);
});

test("isSenderAllowed: exact address matching", () => {
  assert.equal(isSenderAllowed("admin@miautrix.org"), true);
  assert.equal(isSenderAllowed("other@miautrix.org"), false);
});

test("isSenderAllowed: case-insensitive matching", () => {
  assert.equal(isSenderAllowed("Someone@MiAutrix.tech"), true);
  assert.equal(isSenderAllowed("ADMIN@MIAUTRIX.ORG"), true);
  assert.equal(isSenderAllowed("User.Name@MIAUTRIX.TECH"), true);
});

test("isSenderAllowed: rejects untrusted domains", () => {
  assert.equal(isSenderAllowed("attacker@evil.com"), false);
  assert.equal(isSenderAllowed("user@gmail.com"), false);
  assert.equal(isSenderAllowed("spammer@fakemiautrix.tech"), false);
  assert.equal(isSenderAllowed("someone@miautrix.tech.attacker.com"), false);
  assert.equal(isSenderAllowed("someone@sub.miautrix.tech"), false);
});

test("isSenderAllowed: rejects malformed or missing senders without throwing", () => {
  assert.equal(isSenderAllowed(undefined), false);
  assert.equal(isSenderAllowed(null), false);
  assert.equal(isSenderAllowed(""), false);
  assert.equal(isSenderAllowed("   "), false);
  assert.equal(isSenderAllowed("not-an-email"), false);
  assert.equal(isSenderAllowed("@miautrix.tech"), false);
  assert.equal(isSenderAllowed("user@"), false);
  assert.equal(isSenderAllowed("<user@miautrix.tech>"), false);
  assert.equal(isSenderAllowed("user@miautrix.tech; user@evil.com"), false);
  assert.equal(isSenderAllowed(12345), false);
  assert.equal(isSenderAllowed({}), false);
});

test("isSenderAllowed: custom configured patterns and formatting", () => {
  const custom = " *@partner.org , alerts@security.net , \n *@sub.domain.com ";
  assert.equal(isSenderAllowed("bot@partner.org", custom), true);
  assert.equal(isSenderAllowed("alerts@security.net", custom), true);
  assert.equal(isSenderAllowed("test@sub.domain.com", custom), true);
  assert.equal(isSenderAllowed("someone@miautrix.tech", custom), false);
});

function createMockEmailMessage(from?: any, to: string = "inbox@miautrix.tech", rawContent: string = "Subject: Test\r\n\r\nHello") {
  let rejectedReason: string | null = null;
  let forwardedTo: string | null = null;

  return {
    from,
    to,
    headers: new Headers(),
    raw: new ReadableStream({
      start(controller) {
        controller.enqueue(new TextEncoder().encode(rawContent));
        controller.close();
      },
    }),
    rawSize: rawContent.length,
    setReject(reason: string) {
      rejectedReason = reason;
    },
    async forward(rcptTo: string) {
      forwardedTo = rcptTo;
    },
    async reply() {},
    getRejectedReason: () => rejectedReason,
    getForwardedTo: () => forwardedTo,
  };
}

test("email(): rejects disallowed sender without calling forward or webhook", async () => {
  const msg = createMockEmailMessage("attacker@evil.com");
  let fetchCalled = false;
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => {
    fetchCalled = true;
    return new Response(null, { status: 200 });
  }) as any;

  try {
    await worker.email(msg as any, { EMAIL: { send: async () => {} } } as any, {} as any);
    assert.equal(msg.getRejectedReason(), "Address not allowed");
    assert.equal(msg.getForwardedTo(), null);
    assert.equal(fetchCalled, false);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("email(): rejects missing or malformed sender cleanly (no uncaught error / 1101)", async () => {
  for (const badFrom of [undefined, null, "", "   ", "not-an-email", "<user@miautrix.tech>", "user@miautrix.tech; user@evil.com"]) {
    const msg = createMockEmailMessage(badFrom);
    await worker.email(msg as any, { EMAIL: { send: async () => {} } } as any, {} as any);
    assert.equal(msg.getRejectedReason(), "Address not allowed");
  }
});

test("email(): webhook delivery succeeds when token and destination are configured", async () => {
  const msg = createMockEmailMessage("someone@miautrix.tech", "recipient@miautrix.tech");
  let requestedUrl = "";
  let requestHeaders: Headers | null = null;

  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    requestedUrl = String(input);
    requestHeaders = new Headers(init?.headers);
    return new Response(JSON.stringify({ ok: true }), { status: 200 });
  }) as any;

  try {
    const env = {
      EMAIL: { send: async () => {} },
      INBOUND_DESTINATION: "https://mail.miautrix.tech/api/v1/inbound/cloudflare",
      INBOUND_TOKEN: "secret-inbound-token-12345",
    };

    await worker.email(msg as any, env as any, {} as any);
    assert.equal(msg.getRejectedReason(), null);
    assert.equal(requestedUrl, "https://mail.miautrix.tech/api/v1/inbound/cloudflare");
    assert.equal(requestHeaders?.get("X-Miautrix-Inbound-Token"), "secret-inbound-token-12345");
    assert.equal(requestHeaders?.get("X-Miautrix-Envelope-From"), "someone@miautrix.tech");
    assert.equal(requestHeaders?.get("X-Miautrix-Envelope-To"), "recipient@miautrix.tech");
    assert.equal(requestHeaders?.get("Content-Type"), "message/rfc822");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("email(): fails closed if webhook token is not configured", async () => {
  const msg = createMockEmailMessage("someone@miautrix.tech");
  let fetchCalled = false;
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => {
    fetchCalled = true;
    return new Response(null, { status: 200 });
  }) as any;

  try {
    const env = {
      EMAIL: { send: async () => {} },
      INBOUND_DESTINATION: "https://mail.miautrix.tech/api/v1/inbound/cloudflare",
      // INBOUND_TOKEN is unset
    };

    await worker.email(msg as any, env as any, {} as any);
    assert.equal(msg.getRejectedReason(), "Inbound webhook authentication not configured");
    assert.equal(fetchCalled, false);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("email(): handles webhook delivery HTTP failure (e.g. 403 containment or 502) and rejects loudly", async () => {
  const msg = createMockEmailMessage("someone@miautrix.tech");
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => {
    return new Response("Forbidden (Cloudflare containment challenge)", { status: 403 });
  }) as any;

  try {
    const env = {
      EMAIL: { send: async () => {} },
      INBOUND_DESTINATION: "https://mail.miautrix.tech/api/v1/inbound/cloudflare",
      INBOUND_TOKEN: "test-token",
    };

    await worker.email(msg as any, env as any, {} as any);
    assert.equal(msg.getRejectedReason(), "Inbound delivery failed (HTTP 403)");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("email(): handles webhook fetch network exception and rejects loudly", async () => {
  const msg = createMockEmailMessage("someone@miautrix.tech");
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => {
    throw new Error("Connection refused");
  }) as any;

  try {
    const env = {
      EMAIL: { send: async () => {} },
      INBOUND_DESTINATION: "https://mail.miautrix.tech/api/v1/inbound/cloudflare",
      INBOUND_TOKEN: "test-token",
    };

    await worker.email(msg as any, env as any, {} as any);
    assert.equal(msg.getRejectedReason(), "Inbound delivery failed");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("email(): forwards to non-HTTP email address destination if configured", async () => {
  const msg = createMockEmailMessage("admin@miautrix.org");
  const env = {
    EMAIL: { send: async () => {} },
    INBOUND_DESTINATION: "ops-inbox@miautrix.org",
  };

  await worker.email(msg as any, env as any, {} as any);
  assert.equal(msg.getRejectedReason(), null);
  assert.equal(msg.getForwardedTo(), "ops-inbox@miautrix.org");
});

test("email(): handles forward exception cleanly and rejects loudly", async () => {
  const msg = createMockEmailMessage("admin@miautrix.org");
  msg.forward = async () => {
    throw new Error("Destination unverified in Email Routing");
  };

  const env = {
    EMAIL: { send: async () => {} },
    INBOUND_DESTINATION: "unverified@nowhere.com",
  };

  await worker.email(msg as any, env as any, {} as any);
  assert.equal(msg.getRejectedReason(), "Inbound delivery failed");
});
