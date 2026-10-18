#!/usr/bin/env bash
# Kept as a compatibility entry point; runtime assertions replace the old text checks.
set -euo pipefail
exec python3 "$(dirname "$0")/run.py" "$@"
