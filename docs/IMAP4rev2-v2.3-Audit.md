# RFC 9051 IMAP4rev2 paragraph-level server audit

**Scope:** normative paragraphs in RFC 9051 §§2–7. One row represents one paragraph containing server-applicable normative statements. Quote excerpts are limited to 15 words. Paragraph indices are local normative-paragraph ordinals within each section because RFC 9051 does not number prose paragraphs.

**Rows:** 0. **Status:** Implemented 0, Implemented-untested 0, Not applicable 0, Missing 0, Partial 0. **Level:** MUST 0, MUST NOT 0, REQUIRED 0, SHALL 0, SHALL NOT 0, SHOULD 0, SHOULD NOT 0, RECOMMENDED 0.

**Advertisement gate: NOT READY** — applicable Partial MUST-level rows remain, including UID invariants, framing/state, LIST, STATUS, SEARCH, BINARY, and response-code placement.

## Method and limits

Status mapping uses the merged engine, state machine, listener, and existing raw-socket/MailKit tests. Implemented rows cite relevant methods and test assertions. Partial rows name the specific remaining edge-case family from the current implementation checkpoint. A single RFC paragraph can bundle several normative sentences; paragraph-level rows therefore preserve the RFC prose grouping.

Known exclusions include client-only requirements and cleartext STARTTLS (the listener offers implicit TLS only on port 993). `LOGINDISABLED` is not advertised because LOGIN is available after TLS; it is not a server obligation to advertise LOGINDISABLED.

## Section coverage

| Scope | Coverage |
|---|---|
| §2 Protocol overview and message attributes | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |
| §3 State and flow | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |
| §4 Data formats | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |
| §5 Operational considerations | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |
| §6 Client commands (server requirements only) | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |
| §7 Server responses | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |
| Appendix E change items 1–21 | Normative paragraphs inventoried where the RFC states server obligations; client-only statements are N/A. |

## Paragraph matrix

| RFC paragraph | Quote (≤15 words) | Level | Status | Code location | Evidence / remaining behavior |
|---|---|---|---|---|---|

