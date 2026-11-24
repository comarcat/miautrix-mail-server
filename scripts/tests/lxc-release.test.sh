#!/usr/bin/env bash
set -euo pipefail
SCRIPT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lxc-release.sh
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
link_value() { if [[ -L "$1" ]]; then readlink "$1"; else sed -n 's/^MIAUTRIX_LINK://p' "$1"; fi; }
export MIAUTRIX_ROOT="$TMP/root" MIAUTRIX_TEST_MODE=1
mkdir -p "$TMP/bin" "$MIAUTRIX_ROOT"/{data,releases,backups} "$MIAUTRIX_ROOT/releases/current"/{app,worker,cli,admin,webmail} "$MIAUTRIX_ROOT/releases/current/app/mail_data" "$MIAUTRIX_ROOT/releases/current/worker/mail_data"
printf '{"tfm":"net10.0"}\n' > "$MIAUTRIX_ROOT/releases/current/app/old.runtimeconfig.json"
for d in app worker cli admin webmail; do if [[ "$OSTYPE" == msys* || "$OSTYPE" == cygwin* ]]; then printf 'MIAUTRIX_LINK:%s\n' "$MIAUTRIX_ROOT/releases/current/$d" > "$MIAUTRIX_ROOT/$d"; else ln -s "$MIAUTRIX_ROOT/releases/current/$d" "$MIAUTRIX_ROOT/$d"; fi; done
export PATH="$TMP/bin:$PATH"
cat > "$MIAUTRIX_ROOT/.env" <<ENV
MIAUTRIX_STORAGE_DIR="$MIAUTRIX_ROOT/data"
MIAUTRIX_DB_CONNECTION=Host=db;Port=5432;Database=mail;Username=user;Password="p;ss=word=1"
ENV
for tool in systemctl ss pg_dump pg_restore nginx curl openssl; do
  cat > "$TMP/bin/$tool" <<'SHIM'
#!/usr/bin/env bash
set -euo pipefail
case "$(basename "$0")" in
  systemctl) [[ "${1:-}" == show ]] && { printf 'Environment="MIAUTRIX_STORAGE_DIR=%s/data ASPNETCORE_URLS=http://127.0.0.1:5000"\n' "$MIAUTRIX_ROOT"; exit; }; exit 0;;
  ss) printf 'State Recv-Q Send-Q Local Address:Port\nLISTEN 0 128 0.0.0.0:25\nLISTEN 0 128 0.0.0.0:465\nLISTEN 0 128 0.0.0.0:587\nLISTEN 0 128 0.0.0.0:993\n';;
  pg_dump) [[ -n "${PGPASSFILE:-}" && -f "$PGPASSFILE" ]] || exit 31; if [[ "$OSTYPE" != msys* && "$OSTYPE" != cygwin* ]]; then [[ "$(stat -c %a "$PGPASSFILE")" == 600 ]] || exit 32; fi; printf '%s\n' "$*" | grep -qi 'p;ss=word' && exit 33 || true; while (($#)); do if [[ "$1" == --file=* ]]; then : > "${1#*=}"; fi; shift; done;;
  pg_restore) [[ "${1:-}" == --list ]] && printf 'fixture dump\n';;
  nginx) exit 0;;
  curl) printf '%s' "${MIAUTRIX_TEST_HTTP_CODE:-401}";;
  openssl) printf '* OK [CAPABILITY IMAP4rev1] fixture\r\n';;
esac
SHIM
  chmod +x "$TMP/bin/$tool"
done
mkdir -p "$MIAUTRIX_ROOT/releases/old" "$MIAUTRIX_ROOT/releases/current" "$MIAUTRIX_ROOT/backups"
printf 'old\n' > "$MIAUTRIX_ROOT/releases/.previous"
printf 'current\n' > "$MIAUTRIX_ROOT/releases/.current"
printf 'current\n' > "$MIAUTRIX_ROOT/releases/.verified"
mkdir -p "$MIAUTRIX_ROOT/releases/current" "$MIAUTRIX_ROOT/releases/old"
mkdir -p "$TMP/upload/new"/{app,worker,cli,admin,webmail}
for item in 'app/Miautrix.Mail.Web' 'worker/Miautrix.Mail.Worker' 'cli/Miautrix.Mail.Cli'; do touch "$TMP/upload/new/$item"; done
for dir in app worker cli; do printf '{"tfm":"net10.0"}\n' > "$TMP/upload/new/$dir/test.runtimeconfig.json"; done
touch "$TMP/upload/new/admin/index.html" "$TMP/upload/new/webmail/index.html"
printf '{"id":"new","gitSha":"fixture","builtAtUtc":"2026-11-24T00:00:00Z","tfm":"net10.0"}\n' > "$TMP/upload/new/RELEASE.json"
"$SCRIPT" stage --release-id new --upload "$TMP/upload/new"
[[ -f "$MIAUTRIX_ROOT/releases/new/app/Miautrix.Mail.Web" ]]
# Switch must atomically expose the new versioned directories as stable symlinks.
"$SCRIPT" switch --release-id new
[[ "$(link_value "$MIAUTRIX_ROOT/app")" == "$MIAUTRIX_ROOT/releases/new/app" ]]
[[ "$(cat "$MIAUTRIX_ROOT/releases/.previous")" == current ]]
# Rollback changes all stable links back to the recorded prior release.
"$SCRIPT" rollback
[[ "$(cat "$MIAUTRIX_ROOT/releases/.current")" == current ]]
[[ "$(link_value "$MIAUTRIX_ROOT/app")" == "$MIAUTRIX_ROOT/releases/current/app" ]]
# Cleanup requires verified current and keeps only current + previous; protected storage/backups survive.
printf 'current\n' > "$MIAUTRIX_ROOT/releases/.verified"
mkdir -p "$MIAUTRIX_ROOT/releases/older"
mkdir -p "$MIAUTRIX_ROOT/releases/dry-run-candidate"
touch "$MIAUTRIX_ROOT/data/mail.blob" "$MIAUTRIX_ROOT/backups/keep.dump"
for base in 20260101T000000Z_old 20260102T000000Z_middle 20260103T000000Z_newest; do printf 'fixture\n' > "$MIAUTRIX_ROOT/backups/$base.manifest"; touch "$MIAUTRIX_ROOT/backups/$base.dump" "$MIAUTRIX_ROOT/backups/$base.config.tar.gz"; done
touch -t 202601010000 "$MIAUTRIX_ROOT/backups/20260101T000000Z_old.manifest"
touch -t 202601020000 "$MIAUTRIX_ROOT/backups/20260102T000000Z_middle.manifest"
touch -t 202601030000 "$MIAUTRIX_ROOT/backups/20260103T000000Z_newest.manifest"
"$SCRIPT" cleanup --keep 2 --dry-run >/dev/null
[[ -d "$MIAUTRIX_ROOT/releases/dry-run-candidate" ]]
"$SCRIPT" cleanup --keep 2
[[ ! -e "$MIAUTRIX_ROOT/releases/older" ]]
[[ ! -e "$MIAUTRIX_ROOT/releases/dry-run-candidate" ]]
[[ -d "$MIAUTRIX_ROOT/releases/current" && -d "$MIAUTRIX_ROOT/releases/new" ]]
[[ -f "$MIAUTRIX_ROOT/data/mail.blob" && -f "$MIAUTRIX_ROOT/backups/keep.dump" ]]
[[ ! -f "$MIAUTRIX_ROOT/backups/20260101T000000Z_old.dump" && -f "$MIAUTRIX_ROOT/backups/20260102T000000Z_middle.dump" && -f "$MIAUTRIX_ROOT/backups/20260103T000000Z_newest.dump" ]]
# Connection string parser must preserve quoted semicolons and equals in the password.
source "$SCRIPT" status >/dev/null
[[ "$(connection_value 'Host=db;Port=5432;Database=mail;Username=user;Password="p;ss=word=1"' password)" == 'p;ss=word=1' ]]
[[ "$(connection_value 'sErVeR=db;Database=mail;User ID=user;Password="p;ss=word=1"' host)" == db ]]
[[ "$(connection_value 'Server=db;Database=mail;User ID=user;Password="p;ss=word=1"' username)" == user ]]
"$SCRIPT" backup --release-id new >/dev/null
# Unpinned storage and files under the old deploy target are hard failures.
cat > "$TMP/bin/systemctl" <<'SHIM'
#!/usr/bin/env bash
if [[ "${1:-}" == show ]]; then printf 'Environment="ASPNETCORE_URLS=http://127.0.0.1:5000"\n'; fi
SHIM
chmod +x "$TMP/bin/systemctl"
if "$SCRIPT" preflight --release-id new >/dev/null 2>&1; then echo 'preflight unexpectedly accepted unpinned storage'; exit 1; fi
cat > "$TMP/bin/systemctl" <<'SHIM'
#!/usr/bin/env bash
if [[ "${1:-}" == show ]]; then printf 'Environment="MIAUTRIX_STORAGE_DIR=%s/app/mail_data ASPNETCORE_URLS=http://127.0.0.1:5000"\n' "$MIAUTRIX_ROOT"; fi
SHIM
chmod +x "$TMP/bin/systemctl"
rm -f "$MIAUTRIX_ROOT/app"; mkdir -p "$MIAUTRIX_ROOT/app/mail_data"; touch "$MIAUTRIX_ROOT/app/mail_data/old.blob"
unsafe_output=$("$SCRIPT" preflight --release-id new 2>&1) && { echo 'preflight unexpectedly accepted unsafe storage'; exit 1; }
[[ "$unsafe_output" == *'scripts/lxc-prepare-storage.sh --pin'* ]]
rm -rf "$MIAUTRIX_ROOT/app"; printf 'MIAUTRIX_LINK:%s\n' "$MIAUTRIX_ROOT/releases/current/app" > "$MIAUTRIX_ROOT/app"
# Exercise first legacy conversion in an independent fake root.
export MIAUTRIX_ROOT="$TMP/legacy"
mkdir -p "$MIAUTRIX_ROOT"/{app,worker,cli,admin,webmail,data,releases,backups}
mkdir -p "$MIAUTRIX_ROOT/releases/new"/{app,worker,cli,admin,webmail}
for item in 'app/Miautrix.Mail.Web' 'worker/Miautrix.Mail.Worker' 'cli/Miautrix.Mail.Cli'; do touch "$MIAUTRIX_ROOT/releases/new/$item"; chmod +x "$MIAUTRIX_ROOT/releases/new/$item"; done
for dir in app worker cli; do printf '{"tfm":"net10.0"}\n' > "$MIAUTRIX_ROOT/releases/new/$dir/test.runtimeconfig.json"; done
touch "$MIAUTRIX_ROOT/releases/new/admin/index.html" "$MIAUTRIX_ROOT/releases/new/webmail/index.html"
mkdir -p "$TMP/legacy-upload"/{app,worker}
printf '{"id":"new","gitSha":"fixture","builtAtUtc":"2026-11-24T00:00:00Z","tfm":"net10.0"}\n' > "$MIAUTRIX_ROOT/releases/new/RELEASE.json"
for d in app worker admin webmail; do touch "$MIAUTRIX_ROOT/$d/legacy.txt"; done
touch "$MIAUTRIX_ROOT/cli/legacy.txt"
printf '{"tfm":"net8.0"}\n' > "$MIAUTRIX_ROOT/app/legacy.runtimeconfig.json"
cat > "$MIAUTRIX_ROOT/.env" <<ENV
MIAUTRIX_STORAGE_DIR="$MIAUTRIX_ROOT/data"
ASPNETCORE_URLS=http://127.0.0.1:5000
MIAUTRIX_DB_CONNECTION=Host=db;Port=5432;Database=mail;Username=user;Password="p;ss=word=1"
ENV
cat > "$TMP/bin/systemctl" <<'SHIM'
#!/usr/bin/env bash
set -euo pipefail
case "${1:-}" in
  show) printf 'Environment="MIAUTRIX_STORAGE_DIR=%s/data ASPNETCORE_URLS=http://127.0.0.1:5000"\n' "$MIAUTRIX_ROOT";;
esac
SHIM
chmod +x "$TMP/bin/systemctl"
# Preflight blocks server-local settings/secrets/data absent from the uploaded payload.
touch "$MIAUTRIX_ROOT/app/appsettings.Production.json" "$MIAUTRIX_ROOT/worker/tls.key"
guard_output=$("$SCRIPT" preflight --release-id new --upload "$TMP/legacy-upload" 2>&1) && { echo 'preflight accepted server-local files missing from payload'; exit 1; }
[[ "$guard_output" == *'app/appsettings.Production.json'* && "$guard_output" == *'worker/tls.key'* ]]
rm -f "$MIAUTRIX_ROOT/app/appsettings.Production.json" "$MIAUTRIX_ROOT/worker/tls.key"
cat > "$TMP/bin/systemctl" <<'SHIM'
#!/usr/bin/env bash
if [[ "${1:-}" == show ]]; then printf 'Environment="MIAUTRIX_STORAGE_DIR=%s/data MIAUTRIX_TLS_CERT_PATH=%s/app/cert.pem ASPNETCORE_URLS=http://127.0.0.1:5000"\n' "$MIAUTRIX_ROOT" "$MIAUTRIX_ROOT"; fi
SHIM
chmod +x "$TMP/bin/systemctl"
path_output=$("$SCRIPT" preflight --release-id new --upload "$TMP/legacy-upload" 2>&1) && { echo 'preflight accepted path env inside managed app directory'; exit 1; }
[[ "$path_output" == *'MIAUTRIX_TLS_CERT_PATH'* ]]
cat > "$TMP/bin/systemctl" <<'SHIM'
#!/usr/bin/env bash
set -euo pipefail
case "${1:-}" in
  show) printf 'Environment="MIAUTRIX_STORAGE_DIR=%s/data ASPNETCORE_URLS=http://127.0.0.1:5000"\n' "$MIAUTRIX_ROOT";;
esac
SHIM
chmod +x "$TMP/bin/systemctl"
"$SCRIPT" switch --release-id new
legacy_file=$(find "$MIAUTRIX_ROOT/releases" -path '*/app/legacy.txt' -print -quit)
[[ -n "$(link_value "$MIAUTRIX_ROOT/app")" && -n "$legacy_file" ]]
# A failed new-release verify followed by rollback must accept the legacy TFM.
rm -f "$MIAUTRIX_ROOT/releases/new/app/test.runtimeconfig.json"
if "$SCRIPT" verify >/dev/null 2>&1; then echo 'verify unexpectedly accepted missing net10 runtimeconfig'; exit 1; fi
"$SCRIPT" rollback
[[ "$(cat "$MIAUTRIX_ROOT/releases/.current")" == legacy-net8.0-* ]]
"$SCRIPT" verify
printf 'All lxc-release local tests passed.\n'
