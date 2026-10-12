#!/usr/bin/env bash
# Checks a freshly deployed app the way a reader would: it runs the deployed commit, and the pages answer.
# Usage: tools/smoke-test.sh <base url> <expected version (the commit's first 7 characters)>
set -euo pipefail

base=${1%/}
version=$2

fail() {
  echo "::error title=Smoke test::$1"
  exit 1
}

# The app restarts after a deploy and may start cold: give the new version three minutes to answer, by the clock,
# however long each try takes.
health=""
deadline=$((SECONDS + 180))
while (( SECONDS < deadline )); do
  health=$(curl -s -m 15 "$base/healthz" || true)
  [[ $health == *"\"version\":\"$version\""* ]] && break
  sleep 5
done
[[ $health == *"\"status\":\"healthy\""* && $health == *"\"version\":\"$version\""* ]] \
  || fail "/healthz didn't report version $version: ${health:-no answer}"
echo "ok: /healthz reports $version"

page=$(mktemp)
trap 'rm -f "$page"' EXIT

# A path, the status it must answer, and text its page must contain. A 403 or no answer at all (000) is tried again a
# few times: an access rule just added for the runner (staging) takes a moment to reach every front end.
check() {
  local status
  for _ in 1 2 3 4; do
    status=$(curl -s -m 30 -o "$page" -w '%{http_code}' "$base$1" || true)
    [[ $status == 403 || $status == 000 || -z $status ]] || break
    sleep 5
  done
  [[ $status == "$2" ]] || fail "$1 answered ${status:-nothing}, not $2"
  grep -qF -- "$3" "$page" || fail "$1 doesn't contain \"$3\""
  echo "ok: $1 ($status)"
}

check / 200 "<title>The Daily Machine Spirit</title>"
# The rite Today shows, if there's one yet (a new environment has none until its first midnight). Not from the
# archive, which leaves out today's.
rite=$(grep -oE 'data-rite="[0-9]+"' "$page" | head -n 1 | grep -oE '[0-9]+' || true)
check /archive 200 "<title>The Archive · The Daily Machine Spirit</title>"
if [[ -n $rite ]]; then
  check "/r/$rite" 200 " · The Daily Machine Spirit</title>"
else
  echo "skipped: no rite published yet"
fi
check /smoke-test-no-such-page 404 "<title>No such rite exists · The Daily Machine Spirit</title>"
check /fonts/cinzel.woff2 200 "wOF2"
