#!/usr/bin/env bash
set -euo pipefail

# Server-side versioned release manager. MIAUTRIX_ROOT is reserved for isolated local tests.
ROOT=$(realpath -m "${MIAUTRIX_ROOT:-/opt/miautrix-mail}")
TEST_MODE=${MIAUTRIX_TEST_MODE:-0}
RELEASES="$ROOT/releases"
BACKUPS="$ROOT/backups"
APP="$ROOT/app"
WORKER="$ROOT/worker"
CLI="$ROOT/cli"
ADMIN="$ROOT/admin"
WEBMAIL="$ROOT/webmail"

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
summary() { printf '== %s ==\n' "$*"; }
guard_managed_roots() {
  local path resolved
  for path in "$RELEASES" "$BACKUPS"; do
    [[ ! -L "$path" ]] || die "refusing managed directory symlink: $path"
    if [[ -e "$path" ]]; then resolved=$(realpath "$path"); [[ "$resolved" == "$path" ]] || die "managed directory resolves outside its configured path: $path"; fi
  done
}
need_root() { [[ "$TEST_MODE" == 1 || $(id -u) == 0 ]] || die 'must run as root'; guard_managed_roots; }
safe_id() { [[ "$1" =~ ^[A-Za-z0-9._-]+$ ]] || die 'invalid release id'; }
current_id() { if [[ -f "$RELEASES/.current" ]]; then local id; id=$(cat "$RELEASES/.current"); safe_id "$id"; printf '%s' "$id"; else printf 'legacy'; fi; }
previous_id() { if [[ -f "$RELEASES/.previous" ]]; then local id; id=$(cat "$RELEASES/.previous"); [[ -z "$id" ]] && return 0; safe_id "$id"; printf '%s' "$id"; fi; }
tfm_of() { local f; f=$(find "$1" -maxdepth 1 -name '*.runtimeconfig.json' -type f -print -quit 2>/dev/null || true); [[ -n "$f" ]] || return 1; sed -nE 's/.*"tfm"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$f" | head -n1; }
active_environment() { systemctl show -p Environment -p EnvironmentFiles "$1" 2>/dev/null || true; }
env_value() { local data=$1 key=$2; printf '%s\n' "$data" | grep -oE "${key}=[^[:space:]]+" | tail -n1 | cut -d= -f2- | tr -d '"' || true; }
resolve_storage() {
  local web worker envfile webdir workerdir
  web=$(active_environment miautrix-mail)
  worker=$(active_environment miautrix-mail-worker)
  webdir=$(env_value "$web" MIAUTRIX_STORAGE_DIR)
  workerdir=$(env_value "$worker" MIAUTRIX_STORAGE_DIR)
  envfile="$ROOT/.env"
  if [[ -f "$envfile" ]]; then
    local fromfile
    fromfile=$(sed -nE 's/^MIAUTRIX_STORAGE_DIR=(.*)$/\1/p' "$envfile" | tail -n1)
    fromfile=${fromfile#\"}; fromfile=${fromfile%\"}; fromfile=${fromfile#\'}; fromfile=${fromfile%\'}
    [[ -n "$workerdir" ]] || workerdir=$fromfile
  fi
  [[ -n "$webdir" && -n "$workerdir" ]] || die 'MIAUTRIX_STORAGE_DIR must be explicit in the Web unit and worker environment/.env'
  [[ "$webdir" == /* && "$workerdir" == /* ]] || die 'MIAUTRIX_STORAGE_DIR must be an absolute path for both services'
  webdir=$(realpath -m "$webdir"); workerdir=$(realpath -m "$workerdir")
  [[ "$webdir" == "$workerdir" ]] || die 'Web and Worker MIAUTRIX_STORAGE_DIR values differ'
  [[ -d "$webdir" ]] || die "storage path does not exist: $webdir"
  for d in "$APP/mail_data" "$WORKER/mail_data"; do
    if [[ -d "$d" ]] && find "$d" -type f -print -quit | grep -q .; then
      die "mail files exist at $d; remediate with scripts/lxc-prepare-storage.sh --pin, then set MIAUTRIX_STORAGE_DIR in $envfile; data was not moved"
    fi
  done
  case "$webdir/" in "$APP/"*|"$WORKER/"*|"$CLI/"*|"$ADMIN/"*|"$WEBMAIL/"*|"$RELEASES/"*) die "storage path is inside a deploy-managed directory: $webdir";; esac
  printf '%s' "$webdir"
}
connection_value() {
  # Split Npgsql key/value pairs on unquoted semicolons; support single/double quoted values.
  local input=$1 wanted=$2 key value rest quote ch
  while [[ -n "$input" ]]; do
    key=${input%%=*}; [[ "$input" == *=* ]] || break; rest=${input#*=}; value=; quote=
    while [[ -n "$rest" ]]; do
      ch=${rest:0:1}; rest=${rest:1}
      if [[ -n "$quote" ]]; then
        if [[ "$ch" == "$quote" ]]; then quote=; else value+=$ch; fi
      elif [[ "$ch" == "'" || "$ch" == '"' ]]; then quote=$ch
      elif [[ "$ch" == ';' ]]; then break
      else value+=$ch
      fi
    done
    key=$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
    case "$key" in
      host|server) [[ "$wanted" == host ]] && { printf '%s' "$value"; return 0; };;
      port) [[ "$wanted" == port ]] && { printf '%s' "$value"; return 0; };;
      database) [[ "$wanted" == database ]] && { printf '%s' "$value"; return 0; };;
      username|userid|user_id) [[ "$wanted" == username ]] && { printf '%s' "$value"; return 0; };;
      password) [[ "$wanted" == password ]] && { printf '%s' "$value"; return 0; };;
    esac
    input=$rest
  done
  return 1
}
pgpass_escape() { local value=$1; value=${value//\\/\\\\}; value=${value//:/\\:}; printf '%s' "$value"; }
read_db_connection() {
  [[ -f "$ROOT/.env" ]] || die "missing $ROOT/.env"
  local line
  line=$(sed -nE 's/^MIAUTRIX_DB_CONNECTION=(.*)$/\1/p' "$ROOT/.env" | tail -n1)
  line=${line#\"}; line=${line%\"}; line=${line#\'}; line=${line%\'}
  [[ -n "$line" ]] || die 'MIAUTRIX_DB_CONNECTION is missing from .env'
  DB_CONNECTION=$line
  DB_HOST=$(connection_value "$line" host) || die 'DB Host missing from connection string'
  DB_PORT=$(connection_value "$line" port || printf '5432')
  DB_NAME=$(connection_value "$line" database) || die 'DB Database missing from connection string'
  DB_USER=$(connection_value "$line" username) || die 'DB Username missing from connection string'
  DB_PASSWORD=$(connection_value "$line" password) || die 'DB Password missing from connection string'
  [[ -n "$DB_HOST" && -n "$DB_NAME" && -n "$DB_USER" && -n "$DB_PASSWORD" ]] || die 'DB connection details are incomplete'
}
manifest_timestamp() { date -u +%Y%m%dT%H%M%SZ; }
backup_base() { printf '%s_%s' "$(manifest_timestamp)" "${1:-$(current_id)}"; }
record_current() { mkdir -p "$RELEASES"; printf '%s\n' "$1" > "$RELEASES/.current.tmp"; mv -f "$RELEASES/.current.tmp" "$RELEASES/.current"; }
point_link() {
  local name=$1 id=$2 temp="$ROOT/.${1}.link.$$"
  if [[ "$TEST_MODE" == 1 && ( "$OSTYPE" == msys* || "$OSTYPE" == cygwin* ) ]]; then
    printf 'MIAUTRIX_LINK:%s\n' "$RELEASES/$id/$name" > "$temp"
    mv -f "$temp" "$ROOT/$name"
    return
  fi
  ln -s "$RELEASES/$id/$name" "$temp"
  mv -Tf "$temp" "$ROOT/$name"
}
runtimeconfig_check() {
  local directory=$1 expected=${2:-net10.0} tfm marker
  if [[ "$TEST_MODE" == 1 && -f "$directory" ]]; then marker=$(cat "$directory"); [[ "$marker" == MIAUTRIX_LINK:* ]] && directory=${marker#MIAUTRIX_LINK:}; fi
  tfm=$(tfm_of "$directory") || return 1
  [[ "$tfm" == "$expected" ]]
}

release_tfm() {
  local id=$1 metadata="$RELEASES/$1/RELEASE.json" tfm
  if [[ "$id" == legacy-* ]]; then tfm=$(tfm_of "$RELEASES/$id/app") || return 1
  elif [[ -f "$metadata" ]]; then tfm=$(sed -nE 's/.*"tfm"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$metadata" | head -n1)
  else tfm=$(tfm_of "$RELEASES/$id/app") || return 1
  fi
  [[ -n "$tfm" ]] || return 1
  printf '%s' "$tfm"
}

cmd_preflight() {
  need_root; local release=$1 upload=${2:-} path total current_size=0 item tfm
  safe_id "$release"
  local missing=()
  for t in tar systemctl ss pg_dump pg_restore curl openssl timeout nginx; do command -v "$t" >/dev/null 2>&1 || missing+=("$t"); done
  ((${#missing[@]} == 0)) || {
    printf 'Missing required tools: %s\n' "${missing[*]}" >&2
    local -a packages=()
    for t in "${missing[@]}"; do case "$t" in
      tar) packages+=(tar);;
      systemctl) packages+=(systemd);;
      ss) packages+=(iproute2);;
      pg_dump|pg_restore)
        if command -v psql >/dev/null 2>&1; then major=$(psql --version | awk '{print $3}' | cut -d. -f1); elif command -v pg_config >/dev/null 2>&1; then major=$(pg_config --version | awk '{print $2}' | cut -d. -f1); else major='<server-major>'; fi
        packages+=("postgresql-client-$major");;
      curl) packages+=(curl);;
      openssl) packages+=(openssl);;
      timeout) packages+=(coreutils);;
      nginx) packages+=(nginx);;
    esac; done
    printf 'Install missing tools with: apt install %s\n' "$(printf '%s\n' "${packages[@]}" | sort -u | tr '\n' ' ')" >&2
    return 1
  }
  path=$(resolve_storage)
  if [[ -n "$upload" && ! -f "$RELEASES/.current" ]]; then
    local component rel candidate name sensitive absent=()
    for component in app worker; do
      [[ -d "$ROOT/$component" ]] || continue
      while IFS= read -r candidate; do
        rel=${candidate#"$ROOT/$component"/}
        name=${candidate##*/}; sensitive=false
        case "$name" in appsettings.*.json|*.pem|*.key|*.pfx|*.crt|*.env|logs) sensitive=true;; esac
        case "$candidate" in */logs/*|*/dataprotection*|*/DataProtection*|*/data-protection*|*/dataProtection*|*/dataprotection*/*|*/DataProtection*/*|*/data-protection*/*|*/dataProtection*/*) sensitive=true;; esac
        [[ "$sensitive" == true ]] && [[ ! -e "$upload/$component/$rel" ]] && absent+=("$component/$rel")
      done < <(find "$ROOT/$component" \( -type f -o -type d \) -print)
    done
    if ((${#absent[@]})); then printf 'Server-local files absent from uploaded payload:\n' >&2; printf '  %s\n' "${absent[@]}" >&2; die 'move server-local configuration/data outside deploy directories (for example /opt/miautrix-mail/config or /etc/miautrix) and reference it via environment variables'; fi
  fi
  local env_data key value resolved service
  for service in miautrix-mail miautrix-mail-worker; do
    env_data=$(active_environment "$service")
    while IFS= read -r token; do
      key=${token%%=*}; value=${token#*=}; value=${value#\"}; value=${value%\"}
      [[ "$key" == MIAUTRIX_STORAGE_DIR || "$key" == MIAUTRIX_TLS_CERT_PATH || "$key" == MIAUTRIX_TLS_KEY_PATH || "$key" == *_PATH || "$key" == *_DIR || "$key" == *_FILE ]] || continue
      [[ "$value" == /* ]] || continue
      resolved=$(realpath -m "$value")
      case "$resolved/" in "$APP/"*|"$WORKER/"*|"$CLI/"*|"$ADMIN/"*|"$WEBMAIL/"*|"$RELEASES/"*) die "path-valued environment $key resolves inside a deploy-managed directory: $resolved";; esac
    done < <(printf '%s\n' "$env_data" | grep -oE '[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]+' || true)
  done
  if [[ -f "$ROOT/.env" ]]; then
    while IFS='=' read -r key value; do
      [[ "$key" == MIAUTRIX_STORAGE_DIR || "$key" == MIAUTRIX_TLS_CERT_PATH || "$key" == MIAUTRIX_TLS_KEY_PATH || "$key" == *_PATH || "$key" == *_DIR || "$key" == *_FILE ]] || continue
      value=${value#\"}; value=${value%\"}; value=${value#\'}; value=${value%\'}; [[ "$value" == /* ]] || continue
      resolved=$(realpath -m "$value")
      case "$resolved/" in "$APP/"*|"$WORKER/"*|"$CLI/"*|"$ADMIN/"*|"$WEBMAIL/"*|"$RELEASES/"*) die "path-valued environment $key resolves inside a deploy-managed directory: $resolved";; esac
    done < "$ROOT/.env"
  fi
  [[ -d "$APP" && -d "$WORKER" ]] || die 'legacy app and worker directories are required for size/runtime checks'
  for d in "$APP" "$WORKER" "$CLI" "$ADMIN" "$WEBMAIL" "$RELEASES"; do [[ -e "$d" ]] && current_size=$((current_size + $(du -sb "$d" | awk '{print $1}'))); done
  total=$(df -PB1 "$ROOT" | awk 'NR==2 {print $4}')
  (( total >= current_size * 2 + 1073741824 )) || die 'free disk must be at least 2x current install size plus 1 GiB'
  tfm=$(tfm_of "$APP") || tfm=unknown
  summary "preflight $release passed; storage=$path; layout=$(for d in app worker cli admin webmail; do [[ -L "$ROOT/$d" ]] && printf '%s=symlink ' "$d" || printf '%s=directory ' "$d"; done); active=$(current_id); runtime=$tfm"
  for service in miautrix-mail miautrix-mail-worker; do systemctl is-active "$service" || true; done
  if [[ -d "$RELEASES" ]]; then find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -exec du -sh {} + 2>/dev/null || true; fi
}

cmd_backup() {
  need_root; local release=$1 id base dump config tmp passfile sha size
  safe_id "$release"; id=$(current_id); base=$(backup_base "$id"); mkdir -p "$BACKUPS"; chmod 700 "$BACKUPS"
  read_db_connection
  tmp=$(mktemp); passfile=$(mktemp); trap 'rm -f "${tmp:-}" "${passfile:-}"' EXIT
  chmod 600 "$passfile"; printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
  unset DB_PASSWORD
  PGPASSFILE="$passfile" pg_dump -Fc --no-owner --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" --dbname="$DB_NAME" --file="$tmp"
  pg_restore --list "$tmp" >/dev/null
  dump="$BACKUPS/$base.dump"
  if [[ "$TEST_MODE" == 1 ]]; then install -m 600 "$tmp" "$dump"; else install -o root -g root -m 600 "$tmp" "$dump"; fi
  config="$BACKUPS/$base.config.tar.gz"
  local -a config_files=("$ROOT/.env")
  for service in miautrix-mail miautrix-mail-worker; do
    while IFS='=' read -r key value; do
      case "$key" in
        FragmentPath) [[ -f "$value" ]] && config_files+=("$value");;
        DropInPaths) for f in $value; do [[ -f "$f" ]] && config_files+=("$f"); done;;
      esac
    done < <(systemctl show -p FragmentPath -p DropInPaths "$service")
  done
  while IFS= read -r f; do config_files+=("$f"); done < <(find /etc/systemd/system -maxdepth 3 -type f \( -name 'miautrix-mail.service' -o -path '*/miautrix-mail.service.d/*' -o -name 'miautrix-mail-worker.service' -o -path '*/miautrix-mail-worker.service.d/*' \) -print 2>/dev/null)
  while IFS= read -r f; do config_files+=("$f"); done < <(grep -rl '/opt/miautrix-mail' /etc/nginx/sites-available /etc/nginx/sites-enabled 2>/dev/null || true)
  tar -czf "$config" --absolute-names "${config_files[@]}"; [[ "$TEST_MODE" == 1 ]] || chown root:root "$config"; chmod 600 "$config"
  size=$(stat -c %s "$dump"); sha=$(sha256sum "$dump" | awk '{print $1}')
  printf 'release_id=%s\nprevious_release=%s\ndump=%s\nsize=%s\nsha256=%s\nlegacy_conversion_pending=%s\n' "$release" "$id" "$dump" "$size" "$sha" "$([[ -f "$RELEASES/.current" ]] && echo no || echo yes)" > "$BACKUPS/$base.manifest"
  [[ "$TEST_MODE" == 1 ]] || chown root:root "$BACKUPS/$base.manifest"; chmod 600 "$BACKUPS/$base.manifest"
  summary "database dump verified: $dump ($size bytes, sha256 $sha); config snapshot: $config"
}

cmd_stage() {
  need_root; local release=$1 upload=$2 target="$RELEASES/$1"; safe_id "$release"
  [[ -d "$upload" ]] || die 'upload directory does not exist'
  [[ -f "$upload/app/Miautrix.Mail.Web" && -f "$upload/worker/Miautrix.Mail.Worker" && -f "$upload/cli/Miautrix.Mail.Cli" ]] || die 'payload lacks Web, Worker, or CLI executable'
  [[ -f "$upload/admin/index.html" && -f "$upload/webmail/index.html" ]] || die 'payload lacks admin or webmail index.html'
  runtimeconfig_check "$upload/app" || die 'Web runtimeconfig must target net10.0'
  runtimeconfig_check "$upload/worker" || die 'Worker runtimeconfig must target net10.0'
  runtimeconfig_check "$upload/cli" || die 'CLI runtimeconfig must target net10.0'
  [[ ! -e "$target" ]] || die "release already exists: $target"
  local metadata_id metadata_sha metadata_built
  metadata_id=$(sed -nE 's/.*"id"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$upload/RELEASE.json" 2>/dev/null | head -n1 || true)
  metadata_sha=$(sed -nE 's/.*"gitSha"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$upload/RELEASE.json" 2>/dev/null | head -n1 || true)
  metadata_built=$(sed -nE 's/.*"builtAtUtc"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$upload/RELEASE.json" 2>/dev/null | head -n1 || true)
  [[ "$metadata_id" == "$release" && -n "$metadata_sha" && -n "$metadata_built" ]] || die 'payload RELEASE.json is missing or mismatched'
  mkdir -p "$RELEASES"; mv "$upload" "$target"; [[ "$TEST_MODE" == 1 ]] || chown -R www-data:www-data "$target"
  chmod +x "$target/app/Miautrix.Mail.Web" "$target/worker/Miautrix.Mail.Worker" "$target/cli/Miautrix.Mail.Cli"
  printf '{"id":"%s","gitSha":"%s","builtAtUtc":"%s","tfm":"net10.0"}\n' "$release" "$metadata_sha" "$metadata_built" > "$target/RELEASE.json"
  [[ "$TEST_MODE" == 1 ]] || chown www-data:www-data "$target/RELEASE.json"; summary "staged $release at $target"
}

cmd_migrate() {
  need_root; [[ ${1:-} == --bundle && -n ${2:-} ]] || die 'migrate requires --bundle FILE'
  local bundle; bundle=$(realpath "$2")
  [[ -f "$bundle" ]] || die 'migration bundle does not exist'
  chmod +x "$bundle"
  [[ -x "$bundle" ]] || die 'migration bundle is not executable'
  read_db_connection
  export MIAUTRIX_DB_CONNECTION="$DB_CONNECTION"
  unset DB_PASSWORD
  summary 'running the published migration bundle with the server environment connection'
  exec "$bundle"
}

cmd_switch() {
  need_root; local release=$1 old=$2; safe_id "$release"; [[ -d "$RELEASES/$release" ]] || die 'release is not staged'
  trap 'rc=$?; trap - ERR; printf "ERROR: switch failed (current=%s previous=%s); inspect systemd and symlink state before retrying\n" "$(current_id)" "$(previous_id)" >&2; exit "$rc"' ERR
  systemctl stop miautrix-mail-worker; systemctl stop miautrix-mail
  if [[ ! -f "$RELEASES/.current" ]]; then
    local legacy="legacy-$(tfm_of "$APP" 2>/dev/null || echo unknown)-$(manifest_timestamp)"
    mkdir -p "$RELEASES/$legacy"
    for d in app worker admin webmail; do [[ -e "$ROOT/$d" && ! -L "$ROOT/$d" ]] && mv "$ROOT/$d" "$RELEASES/$legacy/$d"; done
    [[ -e "$ROOT/cli" && ! -L "$ROOT/cli" ]] && mv "$ROOT/cli" "$RELEASES/$legacy/cli"
    mkdir -p "$RELEASES/$legacy/cli"
    printf '%s\n' "$legacy" > "$RELEASES/.previous"
    while IFS= read -r manifest; do printf 'legacy_release=%s\n' "$legacy" >> "$manifest"; done < <(grep -rl "^release_id=$release$" "$BACKUPS" --include='*.manifest' 2>/dev/null || true)
  else
    old=$(current_id); printf '%s\n' "$old" > "$RELEASES/.previous"
  fi
  for d in app worker cli admin webmail; do point_link "$d" "$release"; done
  record_current "$release"
  systemctl daemon-reload; systemctl start miautrix-mail; systemctl start miautrix-mail-worker
  if command -v nginx >/dev/null 2>&1; then nginx -t; systemctl reload nginx; fi
  rm -f "$RELEASES/.verified"; trap - ERR
  summary "switched active release to $release; previous=${old:-$(previous_id)}"
}

# Kestrel is serving if it answers any HTTP status below 500. The health URL
# requires authentication, so 401/403 are expected and still prove liveness.
http_alive() { local code; code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 5 "$1" 2>/dev/null || true); [[ "$code" =~ ^[1-4][0-9][0-9]$ ]]; }

cmd_verify() {
  need_root; local release expected_tfm current=0 appinfo greeting urls health attempts=60
  [[ "$TEST_MODE" == 1 ]] && attempts=${MIAUTRIX_VERIFY_ATTEMPTS:-2}
  release=$(current_id)
  expected_tfm=$(release_tfm "$release") || die "cannot determine active release TFM for $release"
  appinfo=$(systemctl show -p Environment -p ExecStart miautrix-mail)
  urls=$(printf '%s' "$appinfo" | grep -oE 'ASPNETCORE_URLS=[^[:space:]"]+' | tail -n1 | cut -d= -f2- || true)
  if [[ -z "$urls" ]]; then urls=$(printf '%s' "$appinfo" | grep -oE -- '--urls[= ][^[:space:]]+' | tail -n1 | sed -E 's/^--urls[= ]+//' || true); fi
  if [[ -z "$urls" && -f "$ROOT/.env" ]]; then urls=$(sed -nE 's/^ASPNETCORE_URLS=(.*)$/\1/p' "$ROOT/.env" | tail -n1 | tr -d '"'); fi
  [[ -n "$urls" ]] || die 'cannot determine Kestrel URL from systemd unit/environment'
  health="${urls%%,*}"; health="${health%/}/api/v1/system/info"
  [[ "$health" == http://* || "$health" == https://* ]] || die 'configured Kestrel URL must be http(s)'
  for ((i=0;i<attempts;i++)); do
    if systemctl is-active --quiet miautrix-mail && systemctl is-active --quiet miautrix-mail-worker && \
       runtimeconfig_check "$APP" "$expected_tfm" && \
       { ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':25$' && ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':465$' && ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':587$' && ss -ltn 2>/dev/null | awk '{print $4}' | grep -Eq ':993$'; } && \
       http_alive "$health" && \
       greeting=$(timeout 5 openssl s_client -connect 127.0.0.1:993 -quiet < /dev/null 2>/dev/null | head -n1 || true) && [[ "$greeting" == '* OK [CAPABILITY IMAP4rev1'* ]]; then current=1; break; fi
    sleep 1
  done
  if (( current != 1 )); then
    local p listening
    listening=$(ss -ltn 2>/dev/null | awk '{print $4}' || true)
    printf 'verify diagnostics for %s:\n' "$release" >&2
    printf '  unit miautrix-mail: %s\n' "$(systemctl is-active miautrix-mail 2>/dev/null || true)" >&2
    printf '  unit miautrix-mail-worker: %s\n' "$(systemctl is-active miautrix-mail-worker 2>/dev/null || true)" >&2
    printf '  runtimeconfig %s: %s\n' "$expected_tfm" "$(runtimeconfig_check "$APP" "$expected_tfm" && echo ok || echo FAIL)" >&2
    for p in 25 465 587 993; do printf '  port %s: %s\n' "$p" "$(grep -Eq ":$p\$" <<<"$listening" && echo listening || echo NOT-LISTENING)" >&2; done
    printf '  http %s: status %s\n' "$health" "$(curl -sS -o /dev/null -w '%{http_code}' --max-time 5 "$health" 2>/dev/null || echo none)" >&2
    printf '  imaps greeting: %s\n' "$(timeout 5 openssl s_client -connect 127.0.0.1:993 -quiet < /dev/null 2>/dev/null | head -n1 | tr -d '\r' || true)" >&2
    die "services, listener ports, health endpoint, $expected_tfm runtimeconfig, or IMAPS greeting failed within 60 seconds"
  fi
  printf '%s\n' "$release" > "$RELEASES/.verified"; summary "verified release $release (units, ports, health, $expected_tfm, IMAPS greeting)"
}

cmd_rollback() {
  need_root; local restore_db=${1:-false} previous dump='' answer old
  previous=$(previous_id); [[ -n "$previous" && -d "$RELEASES/$previous" ]] || die 'no previous release available'
  old=$(current_id); if [[ -d "$RELEASES/$old" ]]; then printf '%s\n' "$old" > "$RELEASES/.previous"; elif [[ -f "$RELEASES/.previous" ]] && [[ ! -d "$RELEASES/$(cat "$RELEASES/.previous")" ]]; then rm -f "$RELEASES/.previous"; fi
  for d in app worker cli admin webmail; do point_link "$d" "$previous"; done
  record_current "$previous"
  systemctl daemon-reload; systemctl restart miautrix-mail; systemctl restart miautrix-mail-worker
  summary "rolled back to $previous; database was not restored"
  read_db_connection; unset DB_PASSWORD
  dump=$(find "$BACKUPS" -maxdepth 1 -name '*.dump' -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n1 | cut -d' ' -f2- || true)
  [[ -n "$dump" ]] && printf 'To restore this deploy backup manually: pg_restore --clean --if-exists --no-owner --dbname="%s" "%s"\n' "$DB_NAME" "$dump"
  if [[ "$restore_db" == true ]]; then
    [[ -n "$dump" ]] || die 'no deploy database dump found'
    printf 'Type RESTORE DATABASE to confirm destructive restore: '; read -r answer
    [[ "$answer" == 'RESTORE DATABASE' ]] || die 'confirmation did not match'
    read_db_connection
    local passfile; passfile=$(mktemp); chmod 600 "$passfile"; trap 'rm -f "${passfile:-}"' EXIT
    printf '%s:%s:%s:%s:%s\n' "$(pgpass_escape "$DB_HOST")" "$DB_PORT" "$(pgpass_escape "$DB_NAME")" "$(pgpass_escape "$DB_USER")" "$(pgpass_escape "$DB_PASSWORD")" > "$passfile"
    unset DB_PASSWORD
    PGPASSFILE="$passfile" pg_restore --clean --if-exists --no-owner --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" --dbname="$DB_NAME" "$dump"
    rm -f "$passfile"
  fi
  cmd_verify
}

cmd_cleanup() {
  need_root; local keep=2 dry=false; while (($#)); do case "$1" in --keep) keep=$2; shift 2;; --dry-run) dry=true; shift;; *) die "unknown cleanup option $1";; esac; done
  [[ "$keep" =~ ^[0-9]+$ ]] && (( keep >= 2 )) || die '--keep must be an integer of at least 2'
  [[ -f "$RELEASES/.verified" && "$(cat "$RELEASES/.verified")" == "$(current_id)" ]] || die 'cleanup refused: active release has not verified'
  local current previous resolved bytes_before bytes_after; current=$(current_id); previous=$(previous_id); bytes_before=$(du -sb "$RELEASES" "$BACKUPS" 2>/dev/null | awk '{s+=$1} END{print s+0}')
  local -a dirs=(); while IFS= read -r d; do dirs+=("$d"); done < <(find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -print | sort)
  for d in "${dirs[@]}"; do
    [[ "$d" == "$RELEASES/$current" || "$d" == "$RELEASES/$previous" ]] && continue
    resolved=$(realpath "$d"); [[ "$resolved" == "$RELEASES/"* ]] || die "refusing to remove path outside releases/: $resolved"
    summary "remove release $resolved"
    [[ "$dry" == true ]] || rm -rf -- "$resolved"
  done
  # Keep dump/config/manifest sets for the two newest timestamped deploys.
  local -a retained_bases=() manifests=(); while IFS= read -r f; do retained_bases+=("${f%.manifest}"); done < <(find "$BACKUPS" -maxdepth 1 -name '*.manifest' -type f -printf '%T@ %p\n' | sort -nr | head -n "$keep" | cut -d' ' -f2-)
  while IFS= read -r f; do manifests+=("$f"); done < <(find "$BACKUPS" -maxdepth 1 -name '*.manifest' -type f -printf '%T@ %p\n' | sort -nr | tail -n +$((keep + 1)) | cut -d' ' -f2-)
  for f in "${manifests[@]}"; do base=${f%.manifest}; for x in "$f" "$base.dump" "$base.config.tar.gz"; do [[ -f "$x" ]] || continue; summary "remove backup $x"; [[ "$dry" == true ]] || rm -f -- "$x"; done; done
  for f in "$BACKUPS"/20*_*.dump "$BACKUPS"/20*_*.config.tar.gz; do [[ -f "$f" ]] || continue; base=${f%.dump}; base=${base%.config.tar.gz}; retained=false; for kept in "${retained_bases[@]}"; do [[ "$base" == "$kept" ]] && retained=true; done; if [[ "$retained" == false ]]; then summary "remove unretained backup $f"; [[ "$dry" == true ]] || rm -f -- "$f"; fi; done
  bytes_after=$(du -sb "$RELEASES" "$BACKUPS" 2>/dev/null | awk '{s+=$1} END{print s+0}'); summary "cleanup complete; freed $((bytes_before - bytes_after)) bytes"
}

cmd_status() {
  guard_managed_roots
  summary "current=$(current_id) previous=$(previous_id)"
  [[ -d "$RELEASES" ]] && find "$RELEASES" -mindepth 1 -maxdepth 1 -type d -exec du -sh {} + 2>/dev/null || true
  [[ -d "$BACKUPS" ]] && find "$BACKUPS" -maxdepth 1 -type f -exec du -sh {} + 2>/dev/null || true
}

usage() { printf 'Usage: %s {preflight|backup|stage|migrate|switch|verify|rollback|cleanup|status} [options]\n' "$0"; }
command=${1:-}; shift || true
case "$command" in
  preflight) [[ ${1:-} == --release-id && -n ${2:-} ]] || die 'preflight requires --release-id'; release=$2; shift 2; upload=; while (($#)); do [[ $1 == --upload && -n ${2:-} ]] || die 'preflight accepts --upload DIR'; upload=$2; shift 2; done; cmd_preflight "$release" "$upload";;
  backup) [[ ${1:-} == --release-id ]] || die 'backup requires --release-id'; cmd_backup "${2:-}";;
  stage) [[ ${1:-} == --release-id && ${3:-} == --upload ]] || die 'stage requires --release-id ID --upload DIR'; cmd_stage "$2" "$4";;
  migrate) cmd_migrate "$@";;
  switch) [[ ${1:-} == --release-id ]] || die 'switch requires --release-id'; cmd_switch "$2" "$(current_id)";;
  verify) cmd_verify;;
  rollback) if [[ ${1:-} == --restore-db ]]; then cmd_rollback true; elif [[ -z ${1:-} ]]; then cmd_rollback false; else die 'rollback accepts only --restore-db'; fi;;
  cleanup) cmd_cleanup "$@";;
  status) cmd_status;;
  *) usage; exit 2;;
esac
