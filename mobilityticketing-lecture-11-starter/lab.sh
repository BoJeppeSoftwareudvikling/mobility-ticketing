#!/usr/bin/env bash
# Source this file: source ./lab.sh

LAB_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
LAB_BUCKET=mobility-archive-lab
LAB_KEY=validation-exports/2026-11-20/validations.jsonl

lab_compose() {
    docker compose --project-directory "$LAB_ROOT" -f "$LAB_ROOT/compose.yaml" "$@"
}

s3() {
    lab_compose run --rm -T aws "$@"
}

# SQL is read from stdin; additional arguments are forwarded to psql.
sql() {
    lab_compose exec -T postgres psql -X -U mobility -d mobility_l11 \
        -v ON_ERROR_STOP=1 "$@"
}

show_reports() {
    sql -c 'SELECT report_id, row_count, supersedes, version_id, sha256 FROM reports ORDER BY report_id;'
}
