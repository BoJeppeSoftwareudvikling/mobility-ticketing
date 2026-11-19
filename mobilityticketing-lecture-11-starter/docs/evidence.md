# Cloud Storage: implementation and results

**Both reports reopen by ID. Interrupted publication resumes without another upload.** All experiments passed; the final state contains two catalogue rows and two object versions.

## What was implemented — and why

Replaced five unfinished methods in [ReportWorkflow.cs](../src/MobilityTicketing.Reports.Application/ReportWorkflow.cs):

| Method | What it does | Why |
| --- | --- | --- |
| `StageAsync` | Measures the supplied bytes, reserves the ID, uploads, validates the version ID, and saves a receipt. | Prevents an accidental second PUT and records exactly what was uploaded. |
| `CheckAsync` | Reads the recorded bucket/key/version and compares SHA-256 **and** newline count. | A later upload to the same key must not replace the report's original input. |
| `VerifyAsync` | Resolves the report ID through the catalogue, then checks its version. | Distinguishes unknown reports, database failures, read failures, and mismatches. |
| `RecoverAsync` | Reads the receipt, verifies it, and saves the catalogue row only on `Matches`. | Resumes after upload without creating another version. |
| `PublishAsync` | Awaits staging, then recovery. | Normal publication and recovery use the same rules. |

`Measure` counts LF bytes, including only newline-terminated rows. Blank, actual null, `"null"`, and `"None"` version IDs are rejected.

Application owns coordination; placing it in the CLI would duplicate recovery rules. References remain `CLI → Application` and `CLI → Infrastructure → Application`. Fixtures, schema, adapters, and CLI syntax are unchanged.

---

## Calls and ownership

[Ports.cs](../src/MobilityTicketing.Reports.Application/Ports.cs) defines Application-owned interfaces. Infrastructure implements them:

```text
Program.cs reads bytes → ReportWorkflow
  → IUploadReceipts.Reserve       → UploadReceipts → local empty reservation
  → IReportObjectStore.UploadAsync → S3ReportObjectStore → versioned object
       [crash gap: object may exist, but its version is not yet in a receipt]
  → IUploadReceipts.WriteAsync    → UploadReceipts → completed receipt
  → IReportObjectStore.ReadVersionAsync → S3ReportObjectStore → exact bytes
  → compare checksum + newline count
  → IReportCatalogue.SaveAsync   → ReportCatalogue → PostgreSQL COMMIT
       [report becomes published here]
```

| Data | Owner |
| --- | --- |
| Original operational validation records | PostgreSQL in the wider system. This starter supplies export fixtures and creates only the `reports` table. |
| Export bytes | Versioned object storage. |
| Published ID, predecessor, version reference, hash and count | PostgreSQL report catalogue. |
| Reserved ID and pending upload information | Local `receipts/` files, retained for recovery. |

`VerifyAsync("R-001")` → catalogue lookup → original version → three-row export, even when the four-row correction is current.

---

## Results we got

Observed in Docker. [Full command log and snapshots](experiment-log.md); `report` runs the CLI with `--no-build`.

| Experiment | Result | Exit | Effect |
| --- | --- | --- | --- |
| `publish R-001 fixtures/validations-v1.jsonl` | `Saved R-001` | 0 | Original published. |
| `stage R-002 fixtures/validations-v2.jsonl` | Version ID returned | 0 | Two objects, but only R-001 in the catalogue. |
| `verify R-002` before recovery | `UnknownReport` | 3 | Staging alone does not publish. |
| Recover after replacing receipt hash with 64 zeros | `Mismatch` in the error | 1 | No catalogue insertion; receipt then restored. |
| `recover R-002 R-NONEXISTENT` | Foreign-key violation `23503` | 1 | Receipt and both versions preserved. |
| `recover R-002 R-001`, twice | `Saved R-002` both times | 0 / 0 | Two catalogue rows; still only two versions. |
| `verify R-001` and `verify R-002` | `Matches` for both | 0 / 0 | Original and correction independently verified. |
| `recover R-002` without its saved predecessor | `ReportConflict` | 1 | Existing row still supersedes R-001. |
| Stage R-002 again | Receipt already exists | 1 | No third version created. |
| Stop only object storage; `verify R-001` | `ReadFailed` | 5 | Catalogue readable; bytes unavailable. |
| Restart object storage; verify again | `Matches` | 0 | Original still intact. |
| Stop only PostgreSQL; `verify R-001` | `CatalogueReadFailed` | 4 | Cannot resolve the report's version. |
| Restart services; verify both | `Matches` for both | 0 / 0 | Both services running; PostgreSQL healthy. |

A failed GET provides no bytes to compare. It cannot tell us whether the content changed.

### Exact versions retained

Both use bucket `mobility-archive-lab` and key `validation-exports/2026-11-20/validations.jsonl`.

| Report | Version ID | Rows | Bytes | Predecessor |
| --- | --- | --- | --- | --- |
| R-001 | `bd22c572-4fa6-4a13-97b0-988a8682eeb2` | 3 | 303 | None |
| R-002 | `32730a29-7010-4eb7-8bc5-3032d7ef88ef` | 4 | 404 | R-001 |

Recovery and failures changed neither version. Fixture hashes matched their original values; full hashes are in the log.

---

## When is retrying safe?

| Failure | What may already exist / what the caller sees | Safe next step |
| --- | --- | --- |
| PUT fails or its response is lost | Reservation exists; an object may exist despite the error. | Investigate the upload outcome. Do not blindly PUT again. |
| Catalogue write fails with a completed receipt | Version and expectations are known; caller gets an error. An uncertain commit may already have succeeded. | Repeat recovery with the same predecessor. It rechecks the same version. |
| Process crashes before receipt completion | Empty or partial receipt; possibly an uploaded object. Caller gets no success; recovery rejects invalid receipt data. | Reconcile the upload and repair the receipt before continuing. |

The existing `ReportCatalogue.InsertAsync` accepts a repeated ID only when **all fields, including predecessor, match**. That makes identical recovery idempotent. Different data produces `ReportConflict`.

---

## Access, retention, and tradeoffs

**Future API:** authorize report access before resolving its object reference or fetching bytes. If ownership is in the catalogue, use a scoped metadata lookup. The CLI has no user authorization.

**Permissions:** the app needs `s3:PutObject` and `s3:GetObjectVersion` for its prefix. Separate bucket setup, listing, and deletion permissions. The lab uses administrator credentials; least-privilege access was not tested.

**Cleanup:** preserve versions referenced by published rows or pending receipts. Investigate empty/partial reservations. Coordinate deletion with uploads/publication and recheck references; age or “not current” alone does not make deletion safe.

**Tradeoff:** local receipts simplify deployment but depend on preserving the disk. They leave a crash gap between PUT and receipt writing. Shared durable pending state is a future improvement.

---

## Verification and reflection

- `dotnet build src/MobilityTicketing.Reports`: **passed, 0 warnings, 0 errors**.
- `dotnet test tests/MobilityTicketing.Reports.Tests`: **21 passed, 0 failed**.
- Unit tests cover measurement, invalid versions, upload/receipt failures, exact-version reads, error outcomes, and recovery. Docker experiments exercise the real adapters.

Version-specific reads and recovery are demonstrated. Actual process crashes and lost PUT responses were not injected; related errors were tested with doubles. Authorization, shared durable receipts, and cleanup need separate production work.
