#!/usr/bin/env python3
"""External IMAP compliance probe for the IMAP Report defects (IMAP-001..005).

Host and credentials come from the environment, never from argv:
  MIAUTRIX_IMAP_HOST      hostname that mail clients use (certificate must match)
  MIAUTRIX_IMAP_USER      mailbox address
  MIAUTRIX_IMAP_PASSWORD  mailbox password or application password
  MIAUTRIX_IMAP_INSECURE  optional; "1" skips certificate validation (testing by IP only)

Read-only: it uses EXAMINE and BODY.PEEK, so no flags change. It prints counts,
sizes and status only, never message contents.
"""
import imaplib
import os
import ssl
import sys

host = os.environ.get("MIAUTRIX_IMAP_HOST")
username = os.environ.get("MIAUTRIX_IMAP_USER")
password = os.environ.get("MIAUTRIX_IMAP_PASSWORD")
if not host or not username or not password:
    sys.exit("Set MIAUTRIX_IMAP_HOST, MIAUTRIX_IMAP_USER, and MIAUTRIX_IMAP_PASSWORD.")

context = ssl.create_default_context()
if os.environ.get("MIAUTRIX_IMAP_INSECURE") == "1":
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE

results = []


def check(name, ok, detail=""):
    results.append(ok)
    print(f"{'PASS' if ok else 'FAIL'}  {name}{('  - ' + detail) if detail else ''}")


with imaplib.IMAP4_SSL(host, 993, ssl_context=context) as client:
    caps = " ".join(client.capabilities)
    check("CAPABILITY", "IMAP4REV1" in caps.upper(), caps)

    status, _ = client.login(username, password)
    check("LOGIN", status == "OK")

    status, folders = client.list()
    names = [f.decode(errors="replace") for f in (folders or []) if f]
    has_inbox = any('"INBOX"' in n.upper() or n.upper().endswith(" INBOX") for n in names)
    check("IMAP-001 LIST returns folders", status == "OK" and has_inbox, f"{len(names)} folders")

    status, data = client.select("INBOX", readonly=True)
    exists = int(data[0]) if status == "OK" and data and data[0] else 0
    check("IMAP-002 EXAMINE INBOX", status == "OK", f"{exists} messages")

    status, data = client.search(None, "ALL")
    ids = data[0].split() if status == "OK" and data and data[0] else []
    check("IMAP-003 SEARCH ALL", status == "OK" and len(ids) == exists, f"{len(ids)} ids")

    status, data = client.uid("SEARCH", None, "ALL")
    check("IMAP-003 UID SEARCH ALL", status == "OK", f"{len(data[0].split()) if data and data[0] else 0} uids")

    if exists:
        status, data = client.fetch("1", "(UID FLAGS RFC822.SIZE BODY.PEEK[HEADER])")
        ok = status == "OK" and data and isinstance(data[0], tuple) and len(data[0][1]) > 0
        check("IMAP-004 FETCH headers literal", ok, f"{len(data[0][1]) if ok else 0} header bytes")

        status, data = client.fetch("1", "(RFC822.SIZE BODY.PEEK[])")
        ok = status == "OK" and data and isinstance(data[0], tuple)
        declared = None
        if ok:
            meta = data[0][0].decode(errors="replace")
            if "RFC822.SIZE" in meta:
                declared = int(meta.split("RFC822.SIZE")[1].split()[0].strip(")"))
        body_len = len(data[0][1]) if ok else 0
        check("IMAP-004 FETCH full body framing", ok and (declared is None or declared == body_len),
              f"{body_len} bytes, RFC822.SIZE {declared}")

        last = ids[-1].decode() if ids else str(exists)
        status, data = client.fetch(f"1:{last}", "(UID FLAGS)")
        check("IMAP-004 FETCH sequence range", status == "OK" and len([d for d in data if d]) >= 1,
              f"{len([d for d in data if d])} responses for 1:{last}")

        uids = []
        for item in data or []:
            text = item.decode(errors="replace") if isinstance(item, bytes) else ""
            if "UID " in text:
                uids.append(int(text.split("UID ")[1].split()[0].strip(")")))
        zero = sum(1 for u in uids if u == 0)
        check("IMAP-006 UIDs non-zero and unique", bool(uids) and zero == 0 and len(set(uids)) == len(uids),
              f"{len(uids)} uids, {zero} zero, {len(uids) - len(set(uids))} duplicates")
    else:
        print("SKIP  FETCH checks: INBOX is empty")

    status, _ = client.close()
    check("CLOSE", status == "OK")
    client.logout()

print(f"\n{sum(results)}/{len(results)} checks passed")
sys.exit(0 if all(results) else 1)
