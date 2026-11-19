# Lecture 11 recap: can we reopen the original report?

**Can we reopen the original report after a corrected file is uploaded to the same storage location?**

The original report had **3 rows**. The correction had **4 rows**. Both used the same S3 key, but versioning preserved each file separately.

## How the solution works

- **Object storage** keeps the file bytes and their versions.
- **PostgreSQL** links each report ID to its exact version, checksum, and row count.
- A **local receipt** records an upload so publication can resume after an interruption.

A report becomes **published when its database row commits**. Uploading the file alone is not enough.

## What we found

| Experiment | Finding |
| --- | --- |
| Open the original after uploading the correction | Both reports verified against their own versions. |
| Upload the correction without publishing it | The file existed, but its report ID returned `UnknownReport`. |
| Change the receipt's expected checksum | Verification detected `Mismatch` and blocked publication. |
| Publish with a nonexistent predecessor | PostgreSQL rejected it; the upload remained available for recovery. |
| Recover the same report twice | Both attempts succeeded, with no extra upload. |
| Recover with a different predecessor | `ReportConflict`; the saved report stayed unchanged. |
| Stop object storage | `ReadFailed` — the file could not be checked. |
| Stop PostgreSQL | `CatalogueReadFailed` — the file's version could not be resolved. |

## The key lessons

An exact version reference lets an old report remain reproducible even when a newer file is current.

A receipt lets us **retry publication without repeating the upload**. However, a crash between uploading and saving the receipt still requires investigation.

**“Couldn't read the file” does not mean “the file changed.”** A mismatch requires actually reading and comparing the bytes.

The recorded run finished with **two published reports, exactly two stored versions, and 21 passing tests**. Both reports verified again after restarting the services.

## More detail

- [Implementation, design decisions, and verification limits](evidence.md)
- [Commands, outputs, and exit codes](experiment-log.md)
