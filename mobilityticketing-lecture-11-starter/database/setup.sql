CREATE TABLE reports (
    report_id text PRIMARY KEY CHECK (report_id ~ '^[A-Za-z0-9][A-Za-z0-9_-]*$'),
    bucket_name text NOT NULL CHECK (btrim(bucket_name) <> ''),
    object_key text NOT NULL CHECK (btrim(object_key) <> ''),
    version_id text NOT NULL CHECK (btrim(version_id) <> '' AND version_id NOT IN ('null', 'None')),
    sha256 text NOT NULL CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    row_count integer NOT NULL CHECK (row_count >= 0),
    supersedes text REFERENCES reports(report_id),
    created_utc timestamptz NOT NULL DEFAULT now(),
    CHECK (supersedes IS NULL OR supersedes <> report_id),
    UNIQUE (bucket_name, object_key, version_id)
);
