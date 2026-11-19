# Lecture 11 command log

Raw observed commands and outputs supporting [the short report](evidence.md).
Commands ran from `mobilityticketing-lecture-11-starter` after a successful build.
The services were initially started with `docker compose up -d postgres object-store` (exit 0).

```bash
source ./lab.sh
report() { dotnet run --no-build --project src/MobilityTicketing.Reports -- "$@"; }
```

`[exit N; expected M]` records the actual exit code and the scenario's prediction.
Docker image-download progress is omitted from initial setup; application output is retained.
This log records a completed run: publishing these IDs again is intentionally refused.

## Initial setup

```text

$ lab_compose ps
NAME                                  IMAGE                                     COMMAND                  SERVICE        CREATED              STATUS                        PORTS
mobility-storage-lab-object-store-1   pgsty/silo:RELEASE.2026-11-12T00-00-00Z   "/usr/bin/docker-ent…"   object-store   About a minute ago   Up About a minute             127.0.0.1:19011->9000/tcp
mobility-storage-lab-postgres-1       postgres:17                               "docker-entrypoint.s…"   postgres       About a minute ago   Up About a minute (healthy)   127.0.0.1:25411->5432/tcp
[exit 0]

$ s3 s3api list-buckets
 Container mobility-storage-lab-aws-run-2b8ec4f370ce Creating
 Container mobility-storage-lab-aws-run-2b8ec4f370ce Created
{
    "Buckets": [],
    "Owner": {
        "DisplayName": "minio",
        "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
    },
    "Prefix": null
}
[exit 0]

$ s3 s3api create-bucket --bucket mobility-archive-lab
 Container mobility-storage-lab-aws-run-78b0d09cd4db Creating
 Container mobility-storage-lab-aws-run-78b0d09cd4db Created
{
    "Location": "/mobility-archive-lab"
}
[exit 0]

$ s3 s3api put-bucket-versioning --bucket mobility-archive-lab --versioning-configuration Status=Enabled
 Container mobility-storage-lab-aws-run-8cb5da875f40 Creating
 Container mobility-storage-lab-aws-run-8cb5da875f40 Created
[exit 0]

$ s3 s3api get-bucket-versioning --bucket mobility-archive-lab
 Container mobility-storage-lab-aws-run-33b0ebbbd09d Creating
 Container mobility-storage-lab-aws-run-33b0ebbbd09d Created
{
    "Status": "Enabled"
}
[exit 0]

$ show_reports
 report_id | row_count | supersedes | version_id | sha256
-----------+-----------+------------+------------+--------
(0 rows)

[exit 0]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-d29322e31b03 Creating
 Container mobility-storage-lab-aws-run-d29322e31b03 Created
{
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0]
```

## Publish, recover, and conflicts

```text

$ report publish R-001 fixtures/validations-v1.jsonl
Saved R-001
[exit 0; expected 0]

$ report stage R-002 fixtures/validations-v2.jsonl
32730a29-7010-4eb7-8bc5-3032d7ef88ef
[exit 0; expected 0]

$ show_reports
 report_id | row_count | supersedes |              version_id              |                              sha256
-----------+-----------+------------+--------------------------------------+------------------------------------------------------------------
 R-001     |         3 |            | bd22c572-4fa6-4a13-97b0-988a8682eeb2 | 3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f
(1 row)

[exit 0; expected 0]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-9ff9ecab97c3 Creating
 Container mobility-storage-lab-aws-run-9ff9ecab97c3 Created
{
    "Versions": [
        {
            "ETag": "\"84cede09f4a5aa8ea99627ed9713a705\"",
            "Size": 404,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "32730a29-7010-4eb7-8bc5-3032d7ef88ef",
            "IsLatest": true,
            "LastModified": "2026-11-19T09:34:10.351000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        },
        {
            "ETag": "\"bb2ac15f906649c56b5372986e88cd92\"",
            "Size": 303,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "bd22c572-4fa6-4a13-97b0-988a8682eeb2",
            "IsLatest": false,
            "LastModified": "2026-11-19T09:34:09.572000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        }
    ],
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0; expected 0]

$ report verify R-002
UnknownReport
[exit 3; expected 3]

$ cp receipts/R-002.json /tmp/lecture11-R-002.json
[exit 0; expected 0]

$ python3 -c import\ json\;\ from\ pathlib\ import\ Path\;\ p=Path\(\"receipts/R-002.json\"\)\;\ r=json.loads\(p.read_text\(\)\)\;\ r\[\"Sha256\"\]=\"0\"\*64\;\ p.write_text\(json.dumps\(r\)\)
[exit 0; expected 0]

$ report recover R-002 R-001
Report R-002 verification failed: Mismatch.
[exit 1; expected 1]

$ show_reports
 report_id | row_count | supersedes |              version_id              |                              sha256
-----------+-----------+------------+--------------------------------------+------------------------------------------------------------------
 R-001     |         3 |            | bd22c572-4fa6-4a13-97b0-988a8682eeb2 | 3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f
(1 row)

[exit 0; expected 0]

$ cp /tmp/lecture11-R-002.json receipts/R-002.json
[exit 0; expected 0]

$ report recover R-002 R-NONEXISTENT
23503: insert or update on table "reports" violates foreign key constraint "reports_supersedes_fkey"

DETAIL: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information.
[exit 1; expected 1]

$ show_reports
 report_id | row_count | supersedes |              version_id              |                              sha256
-----------+-----------+------------+--------------------------------------+------------------------------------------------------------------
 R-001     |         3 |            | bd22c572-4fa6-4a13-97b0-988a8682eeb2 | 3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f
(1 row)

[exit 0; expected 0]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-abfe8bf94a49 Creating
 Container mobility-storage-lab-aws-run-abfe8bf94a49 Created
{
    "Versions": [
        {
            "ETag": "\"84cede09f4a5aa8ea99627ed9713a705\"",
            "Size": 404,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "32730a29-7010-4eb7-8bc5-3032d7ef88ef",
            "IsLatest": true,
            "LastModified": "2026-11-19T09:34:10.351000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        },
        {
            "ETag": "\"bb2ac15f906649c56b5372986e88cd92\"",
            "Size": 303,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "bd22c572-4fa6-4a13-97b0-988a8682eeb2",
            "IsLatest": false,
            "LastModified": "2026-11-19T09:34:09.572000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        }
    ],
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0; expected 0]

$ report recover R-002 R-001
Saved R-002
[exit 0; expected 0]

$ report recover R-002 R-001
Saved R-002
[exit 0; expected 0]

$ report verify R-001
Matches
[exit 0; expected 0]

$ report verify R-002
Matches
[exit 0; expected 0]

$ show_reports
 report_id | row_count | supersedes |              version_id              |                              sha256
-----------+-----------+------------+--------------------------------------+------------------------------------------------------------------
 R-001     |         3 |            | bd22c572-4fa6-4a13-97b0-988a8682eeb2 | 3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f
 R-002     |         4 | R-001      | 32730a29-7010-4eb7-8bc5-3032d7ef88ef | 545370b304ada29a502c2e9430fe20d36d62f5bf527729220e24bd7fb81c4641
(2 rows)

[exit 0; expected 0]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-ed66ecd56533 Creating
 Container mobility-storage-lab-aws-run-ed66ecd56533 Created
{
    "Versions": [
        {
            "ETag": "\"84cede09f4a5aa8ea99627ed9713a705\"",
            "Size": 404,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "32730a29-7010-4eb7-8bc5-3032d7ef88ef",
            "IsLatest": true,
            "LastModified": "2026-11-19T09:34:10.351000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        },
        {
            "ETag": "\"bb2ac15f906649c56b5372986e88cd92\"",
            "Size": 303,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "bd22c572-4fa6-4a13-97b0-988a8682eeb2",
            "IsLatest": false,
            "LastModified": "2026-11-19T09:34:09.572000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        }
    ],
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0; expected 0]

$ report recover R-002
ReportConflict
[exit 1; expected 1]

$ show_reports
 report_id | row_count | supersedes |              version_id              |                              sha256
-----------+-----------+------------+--------------------------------------+------------------------------------------------------------------
 R-001     |         3 |            | bd22c572-4fa6-4a13-97b0-988a8682eeb2 | 3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f
 R-002     |         4 | R-001      | 32730a29-7010-4eb7-8bc5-3032d7ef88ef | 545370b304ada29a502c2e9430fe20d36d62f5bf527729220e24bd7fb81c4641
(2 rows)

[exit 0; expected 0]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-1d86a889cc26 Creating
 Container mobility-storage-lab-aws-run-1d86a889cc26 Created
{
    "Versions": [
        {
            "ETag": "\"84cede09f4a5aa8ea99627ed9713a705\"",
            "Size": 404,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "32730a29-7010-4eb7-8bc5-3032d7ef88ef",
            "IsLatest": true,
            "LastModified": "2026-11-19T09:34:10.351000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        },
        {
            "ETag": "\"bb2ac15f906649c56b5372986e88cd92\"",
            "Size": 303,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "bd22c572-4fa6-4a13-97b0-988a8682eeb2",
            "IsLatest": false,
            "LastModified": "2026-11-19T09:34:09.572000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        }
    ],
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0; expected 0]

$ report stage R-002 fixtures/validations-v2.jsonl
The file '/home/jklh/studies/projects/mobility-ticketing/mobilityticketing-lecture-11-starter/receipts/R-002.json' already exists.
[exit 1; expected 1]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-1cfddb6e8108 Creating
 Container mobility-storage-lab-aws-run-1cfddb6e8108 Created
{
    "Versions": [
        {
            "ETag": "\"84cede09f4a5aa8ea99627ed9713a705\"",
            "Size": 404,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "32730a29-7010-4eb7-8bc5-3032d7ef88ef",
            "IsLatest": true,
            "LastModified": "2026-11-19T09:34:10.351000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        },
        {
            "ETag": "\"bb2ac15f906649c56b5372986e88cd92\"",
            "Size": 303,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "bd22c572-4fa6-4a13-97b0-988a8682eeb2",
            "IsLatest": false,
            "LastModified": "2026-11-19T09:34:09.572000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        }
    ],
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0; expected 0]

$ sha256sum fixtures/validations-v1.jsonl fixtures/validations-v2.jsonl
3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f  fixtures/validations-v1.jsonl
545370b304ada29a502c2e9430fe20d36d62f5bf527729220e24bd7fb81c4641  fixtures/validations-v2.jsonl
[exit 0; expected 0]
```

## Service outages and final state

```text

$ lab_compose stop object-store
 Container mobility-storage-lab-object-store-1 Stopping
 Container mobility-storage-lab-object-store-1 Stopped
[exit 0; expected 0]

$ lab_compose ps -a
NAME                                  IMAGE                                     COMMAND                  SERVICE        CREATED         STATUS                              PORTS
mobility-storage-lab-object-store-1   pgsty/silo:RELEASE.2026-11-12T00-00-00Z   "/usr/bin/docker-ent…"   object-store   2 minutes ago   Exited (0) Less than a second ago
mobility-storage-lab-postgres-1       postgres:17                               "docker-entrypoint.s…"   postgres       2 minutes ago   Up 2 minutes (healthy)              127.0.0.1:25411->5432/tcp
[exit 0; expected 0]

$ report verify R-001
ReadFailed
[exit 5; expected 5]

$ lab_compose start object-store
 Container mobility-storage-lab-object-store-1 Starting
 Container mobility-storage-lab-object-store-1 Started
[exit 0; expected 0]

$ s3 s3api list-buckets
 Container mobility-storage-lab-aws-run-f319a1ccf9fa Creating
 Container mobility-storage-lab-aws-run-f319a1ccf9fa Created
{
    "Buckets": [
        {
            "Name": "mobility-archive-lab",
            "CreationDate": "2026-11-19T09:33:54.626000+00:00"
        }
    ],
    "Owner": {
        "DisplayName": "minio",
        "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
    },
    "Prefix": null
}
[exit 0; expected 0]

$ report verify R-001
Matches
[exit 0; expected 0]

$ lab_compose stop postgres
 Container mobility-storage-lab-postgres-1 Stopping
 Container mobility-storage-lab-postgres-1 Stopped
[exit 0; expected 0]

$ lab_compose ps -a
NAME                                  IMAGE                                     COMMAND                  SERVICE        CREATED         STATUS                              PORTS
mobility-storage-lab-object-store-1   pgsty/silo:RELEASE.2026-11-12T00-00-00Z   "/usr/bin/docker-ent…"   object-store   2 minutes ago   Up 2 seconds                        127.0.0.1:19011->9000/tcp
mobility-storage-lab-postgres-1       postgres:17                               "docker-entrypoint.s…"   postgres       2 minutes ago   Exited (0) Less than a second ago
[exit 0; expected 0]

$ report verify R-001
CatalogueReadFailed
[exit 4; expected 4]

$ lab_compose start postgres object-store
 Container mobility-storage-lab-postgres-1 Starting
 Container mobility-storage-lab-postgres-1 Started
[exit 0; expected 0]

$ lab_compose exec -T postgres pg_isready -U mobility -d mobility_l11
/var/run/postgresql:5432 - accepting connections
[exit 0; expected 0]

$ s3 s3api list-buckets
 Container mobility-storage-lab-aws-run-b584bfaf6d7a Creating
 Container mobility-storage-lab-aws-run-b584bfaf6d7a Created
{
    "Buckets": [
        {
            "Name": "mobility-archive-lab",
            "CreationDate": "2026-11-19T09:33:54.626000+00:00"
        }
    ],
    "Owner": {
        "DisplayName": "minio",
        "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
    },
    "Prefix": null
}
[exit 0; expected 0]

$ report verify R-001
Matches
[exit 0; expected 0]

$ report verify R-002
Matches
[exit 0; expected 0]

$ show_reports
 report_id | row_count | supersedes |              version_id              |                              sha256
-----------+-----------+------------+--------------------------------------+------------------------------------------------------------------
 R-001     |         3 |            | bd22c572-4fa6-4a13-97b0-988a8682eeb2 | 3a41a3be76217883a3790a0c8889e53278575188dd5b61cf85226e6210ba4a2f
 R-002     |         4 | R-001      | 32730a29-7010-4eb7-8bc5-3032d7ef88ef | 545370b304ada29a502c2e9430fe20d36d62f5bf527729220e24bd7fb81c4641
(2 rows)

[exit 0; expected 0]

$ s3 s3api list-object-versions --bucket mobility-archive-lab --prefix validation-exports/2026-11-20/validations.jsonl
 Container mobility-storage-lab-aws-run-bc04d15ac30c Creating
 Container mobility-storage-lab-aws-run-bc04d15ac30c Created
{
    "Versions": [
        {
            "ETag": "\"84cede09f4a5aa8ea99627ed9713a705\"",
            "Size": 404,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "32730a29-7010-4eb7-8bc5-3032d7ef88ef",
            "IsLatest": true,
            "LastModified": "2026-11-19T09:34:10.351000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        },
        {
            "ETag": "\"bb2ac15f906649c56b5372986e88cd92\"",
            "Size": 303,
            "StorageClass": "STANDARD",
            "Key": "validation-exports/2026-11-20/validations.jsonl",
            "VersionId": "bd22c572-4fa6-4a13-97b0-988a8682eeb2",
            "IsLatest": false,
            "LastModified": "2026-11-19T09:34:09.572000+00:00",
            "Owner": {
                "DisplayName": "minio",
                "ID": "02d6176db174dc93cb1b899f7c6078f08654445fe8cf1b6ce98d8855f66bdbf4"
            }
        }
    ],
    "RequestCharged": null,
    "Prefix": "validation-exports/2026-11-20/validations.jsonl"
}
[exit 0; expected 0]

$ lab_compose ps
NAME                                  IMAGE                                     COMMAND                  SERVICE        CREATED         STATUS                   PORTS
mobility-storage-lab-object-store-1   pgsty/silo:RELEASE.2026-11-12T00-00-00Z   "/usr/bin/docker-ent…"   object-store   2 minutes ago   Up 7 seconds             127.0.0.1:19011->9000/tcp
mobility-storage-lab-postgres-1       postgres:17                               "docker-entrypoint.s…"   postgres       2 minutes ago   Up 4 seconds (healthy)   127.0.0.1:25411->5432/tcp
[exit 0; expected 0]
```
