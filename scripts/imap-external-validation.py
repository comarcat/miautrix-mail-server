#!/usr/bin/env python3
"""Manual IMAP compatibility probe. Credentials and host are supplied through the environment."""
import imaplib
import os
import ssl
import sys


host = os.environ.get("MIAUTRIX_IMAP_HOST")
username = os.environ.get("MIAUTRIX_IMAP_USER")
password = os.environ.get("MIAUTRIX_IMAP_PASSWORD")
if not all((host, username, password)):
    sys.exit("Set MIAUTRIX_IMAP_HOST, MIAUTRIX_IMAP_USER, and MIAUTRIX_IMAP_PASSWORD.")

with imaplib.IMAP4_SSL(host, 993, ssl_context=ssl.create_default_context()) as client:
    print("CAPABILITY:", client.capabilities)
    print("LIST:", client.list())
    print("EXAMINE:", client.select("INBOX", readonly=True))
    print("SEARCH:", client.search(None, "ALL"))
    print("FETCH:", client.fetch("1", "(UID FLAGS BODY.PEEK[HEADER])"))
    print("LOGOUT:", client.logout())
