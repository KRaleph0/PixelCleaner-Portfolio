#!/usr/bin/env bash
# 전체 검증. 실서버 DB는 건드리지 않고 임시 DB에 대해서만 돈다.
#   python -m venv .venv && .venv/bin/pip install -r requirements.txt httpx
#   tests/run_all.sh
set -u
cd "$(dirname "$0")/.."
PY=${PYTHON:-python3}
rc=0
for t in tests/smoke_test.py tests/migration_test.py tests/concurrency_test.py; do
  echo "=== $t ==="
  "$PY" "$t" 2>&1 | grep -E "^\s+(PASS|FAIL)|^\[|^결과" || true
  [ "${PIPESTATUS[0]}" -ne 0 ] && rc=1
done
exit $rc
