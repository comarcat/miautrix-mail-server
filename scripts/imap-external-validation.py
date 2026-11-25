#!/usr/bin/env python3
"""External IMAP compatibility probe, including an optional delegated mailbox.

Host and credentials come from the environment, never from argv:
  MIAUTRIX_IMAP_HOST      hostname that mail clients use (certificate must match)
  MIAUTRIX_IMAP_USER      mailbox address
  MIAUTRIX_IMAP_PASSWORD  mailbox password or application password
  MIAUTRIX_IMAP_INSECURE  optional; "1" skips certificate validation (testing by IP only)
  MIAUTRIX_IMAP_SHARED_MAILBOX optional; full address to probe under Shared/<address>

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

    status, namespaces = client.namespace()
    personal, other_users, shared = namespaces if status == "OK" else (None, None, None)
    decode = lambda value: value.decode(errors="replace") if isinstance(value, bytes) else value
    personal_ok = bool(personal and any(decode(prefix) == "" and decode(delimiter) == "/" for prefix, delimiter in personal))
    shared_ok = bool(shared and any(decode(prefix) == "Shared/" and decode(delimiter) == "/" for prefix, delimiter in shared))
    check("IMAP-010 NAMESPACE personal and shared", status == "OK" and personal_ok and shared_ok,
          f"personal={personal!r}; shared={shared!r}; other-users={other_users!r}")

    status, folders = client.list()
    names = [f.decode(errors="replace") for f in (folders or []) if f]
    has_inbox = any('"INBOX"' in n.upper() or n.upper().endswith(" INBOX") for n in names)
    check("IMAP-001 LIST returns folders", status == "OK" and has_inbox, f"{len(names)} folders")
    status, folders = client.lsub()
    subscribed_names = [f.decode(errors="replace") for f in (folders or []) if f]
    subscribed_upper = "\n".join(subscribed_names).upper()
    special_subscribed = all(name in subscribed_upper for name in ("SENT", "DRAFT", "ARCHIVE", "JUNK", "TRASH"))
    check("IMAP-009 default folders are subscribed", status == "OK" and special_subscribed,
          f"{len(subscribed_names)} subscribed folders")

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
        check("IMAP-008 RFC822.SIZE equals BODY.PEEK[] octets", ok and declared is not None and declared == body_len,
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

    shared_address = os.environ.get("MIAUTRIX_IMAP_SHARED_MAILBOX")
    if shared_address:
        shared_prefix = f"Shared/{shared_address}"
        status, folders = client.list('""', f'"{shared_prefix}/*"')
        shared_names = [f.decode(errors="replace") for f in (folders or []) if f]
        check("IMAP-009 authorized shared LIST", status == "OK" and bool(shared_names),
              f"{len(shared_names)} folders for configured shared mailbox")
        status, folders = client.lsub('""', f'"{shared_prefix}/*"')
        subscribed_shared = [f.decode(errors="replace") for f in (folders or []) if f]
        check("IMAP-009 shared folders are subscribed by default", status == "OK" and bool(subscribed_shared),
              f"{len(subscribed_shared)} subscribed shared folders")
        shared_inbox = f"{shared_prefix}/INBOX"
        status, data = client.select(shared_inbox, readonly=True)
        shared_count = int(data[0]) if status == "OK" and data and data[0] else 0
        check("IMAP-009 shared EXAMINE", status == "OK", f"{shared_count} messages")
        if status == "OK":
            status, data = client.search(None, "ALL")
            shared_ids = data[0].split() if status == "OK" and data and data[0] else []
            check("IMAP-009 shared SEARCH", status == "OK", f"{len(shared_ids)} ids")
            if shared_ids:
                status, data = client.fetch("1", "(UID FLAGS RFC822.SIZE BODY.PEEK[])")
                ok = status == "OK" and data and isinstance(data[0], tuple)
                literal_len = len(data[0][1]) if ok else 0
                declared = None
                if ok:
                    meta = data[0][0].decode(errors="replace")
                    if "RFC822.SIZE" in meta:
                        declared = int(meta.split("RFC822.SIZE", 1)[1].split()[0].strip(")"))
                check("IMAP-009 shared FETCH literal and size", ok and declared == literal_len,
                      f"{literal_len} bytes, RFC822.SIZE {declared}")
            client.close()
    else:
        print("SKIP  IMAP-009 shared checks: MIAUTRIX_IMAP_SHARED_MAILBOX is unset")

    client.logout()

# Run a separate session in explicit rev2 mode so the read-only rev1 pass above
# remains a regression check for clients (including Thunderbird) that never ENABLE.
with imaplib.IMAP4_SSL(host, 993, ssl_context=context) as rev2:
    status, _ = rev2.login(username, password)
    check("IMAP4rev2 LOGIN", status == "OK")
    status, data = rev2._simple_command("ENABLE", "IMAP4rev2")
    enabled = status == "OK" and any(b"IMAP4REV2" in (line or b"").upper() for line in (data or []))
    check("IMAP4rev2 ENABLE", enabled)
    status, data = rev2.select("INBOX", readonly=True)
    check("IMAP4rev2 EXAMINE/SELECT", status == "OK", f"{data[0].decode(errors='replace') if data and data[0] else '0'} messages")
    if status == "OK":
        status, data = rev2.search(None, "ALL")
        check("IMAP4rev2 SEARCH", status == "OK", f"{len(data[0].split()) if data and data[0] else 0} ids")
        status, data = rev2.status("INBOX", "(MESSAGES DELETED SIZE)")
        check("IMAP4rev2 STATUS DELETED/SIZE", status == "OK")
        status, _ = rev2.status("INBOX", "(RECENT)")
        check("IMAP4rev2 rejects RECENT", status == "NO" or status == "BAD", status)
        status, _ = rev2.lsub()
        check("IMAP4rev2 LSUB rejected", status == "NO" or status == "BAD", status)
        rev2.close()
    rev2.logout()

print(f"\n{sum(results)}/{len(results)} checks passed")
sys.exit(0 if all(results) else 1)
