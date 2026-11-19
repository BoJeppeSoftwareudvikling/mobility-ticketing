# Lecture 11: Cloud Storage

This starter runs independently of earlier labs.

## Requirements

- Docker with Docker Compose
- .NET 8 SDK, Bash and Git. On Windows, use WSL with Docker integration.

The services use a local S3-compatible object store and PostgreSQL. The console app connects on loopback ports 19011 (object storage) and 25411 (PostgreSQL). No cloud account is required. The included data and credentials are for this local lab.

## Get started

Clone the repository and open a Bash terminal in its folder:

```bash
git clone https://github.com/patrickstolc/mobilityticketing-lecture-11-starter.git
cd mobilityticketing-lecture-11-starter
source ./lab.sh
lab_compose up -d postgres object-store
lab_compose ps
```

Wait until PostgreSQL is healthy, then check that the object store responds:

```bash
s3 s3api list-buckets
```

If the service is still starting, wait briefly and try again. Create the bucket once and enable versioning:

```bash
s3 s3api create-bucket --bucket "$LAB_BUCKET"
s3 s3api put-bucket-versioning --bucket "$LAB_BUCKET" --versioning-configuration Status=Enabled
s3 s3api get-bucket-versioning --bucket "$LAB_BUCKET"
```

The last command should report `"Status": "Enabled"`. If you are resuming an existing lab, check the bucket with `s3 s3api head-bucket --bucket "$LAB_BUCKET"` instead of creating it again. In each new terminal, run `source ./lab.sh` from the repository folder.

## Commands

| Command | Purpose |
| --- | --- |
| `lab_compose ...` | Runs Docker Compose for this project. |
| `s3 s3api ...` | Runs the AWS CLI against the local object store. |
| `sql` | Runs SQL through PostgreSQL; accepts `psql` options. |
| `show_reports` | Lists report metadata. |
| `dotnet run --project src/MobilityTicketing.Reports -- --help` | Shows the console app commands. |
| `dotnet build src/MobilityTicketing.Reports` | Restores packages and builds the app. |

Run the app from the repository root so it can find the fixtures and receipts. The workflow methods in the starter report that they are unfinished until implemented.

## Projects

| Project under `src/` | Role |
| --- | --- |
| `MobilityTicketing.Reports` | Console entry point |
| `MobilityTicketing.Reports.Application` | Report types, workflow and interfaces |
| `MobilityTicketing.Reports.Infrastructure` | PostgreSQL, S3 and receipt adapters |

## Stop and resume

```bash
lab_compose stop
```

To resume, run `source ./lab.sh` in a Bash terminal in this folder, then:

```bash
lab_compose up -d postgres object-store
```

Named volumes retain the database and objects. Keep the local `receipts` folder when resuming. Uploading again creates another object version.
