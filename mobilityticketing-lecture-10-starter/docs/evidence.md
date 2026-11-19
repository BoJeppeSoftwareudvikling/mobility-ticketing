# Lecture 10 evidence

For the process, findings and reasons behind the application choices, read the [cancellation lab report](report.md).

## Scope and settings

Completed the cancellation lab against the supplied isolated MongoDB replica set. The implementation is in `scripts/verify.js`; the other supplied scripts and Compose configuration are unchanged. PostgreSQL remains authoritative for the real timetable and booking decisions. These synthetic notices do not cancel a real trip or sell a ticket.

The approach follows the week 46 notes on replica-set elections, read preference, write concern, acknowledgement timeouts, and verification of uncertain writes. Predictions below were recorded before each command, followed by its actual output. This is an AI-assisted implementation and experiment record, not evidence of independent student mastery.

- Three voting, data-bearing members using `mongo:8.0`, replica set `rs0`, database `mobility_lab10`, collection `trip_changes`.
- Expected notice: `{_id: requestId, tripId: 'T102', status: 'Cancelled', sourceVersion: 2}`.
- `w: 1` requests primary acknowledgement; `majority` requires two members in this configuration; `w: 3` requires all three. `wtimeout` is 2,000 ms.
- Ordinary reads use the requested `primary` or `secondary` preference with `local` concern. Verification uses `primary` with `majority` concern. Reads have `maxTimeMS: 2000`.
- Client retries are disabled. Server-selection, connection and socket timeouts are 3,000, 2,000 and 6,000 ms respectively. JSON `durationMs` is distinct from complete shell-command time.
- Startup created fresh lab volumes. Each separate write used a distinct example ID; `NEVER-SENT` was never written. No data was deleted.

The [full experiment log](evidence/experiments.jsonl) contains a prediction record followed by a result record for every command: exact arguments, exit code, shell duration, complete stdout and stderr. MongoDB stdout retains its original JSON, timestamps, settings, documents and errors. Labels in the tables identify records in that log. The extra Node check ran from a temporary file; its complete reproducible source is included below.

---

## Replica-set transitions

| Log label | Command | Observed roles |
| --- | --- | --- |
| `baseline-status` | `bash run.sh status` | mongo1: PRIMARY (health 1); mongo2: SECONDARY (health 1); mongo3: SECONDARY (health 1) |
| `failover-status` | `bash run.sh status mongo2` | mongo1: (not reachable/healthy) (health 0); mongo2: PRIMARY (health 1); mongo3: SECONDARY (health 1) |
| `restored-status` | `bash run.sh status mongo2` | mongo1: SECONDARY (health 1); mongo2: PRIMARY (health 1); mongo3: SECONDARY (health 1) |
| `no-majority-status` | `bash run.sh status mongo2` | mongo1: (not reachable/healthy) (health 0); mongo2: PRIMARY (health 1); mongo3: (not reachable/healthy) (health 0) |
| `no-majority-confirmed-status` | `bash run.sh status mongo2` | mongo1: (not reachable/healthy) (health 0); mongo2: SECONDARY (health 1); mongo3: (not reachable/healthy) (health 0) |
| `recovery-status` | `bash run.sh status` | mongo1: SECONDARY (health 1); mongo2: PRIMARY (health 1); mongo3: SECONDARY (health 1) |

`mongo1` was the original primary. After `docker compose stop mongo1`, the next secondary read and `FAILOVER-01` write both succeeded. The stop command itself took 10.328 seconds; the first write reported 532 ms. This observed graceful handover does not establish uninterrupted availability during every election.

After restoring all members, `mongo2` was primary. Only `mongo1` and `mongo3` were stopped for the majority-loss experiment. The first status still showed `mongo2` as primary; a later status confirmed it had become secondary. All no-majority reads, writes and verification were performed **after** that confirmed step-down. Recovery restored `mongo2` as primary and the other two as secondaries.

---

## Experiments

Every successful ordinary read below returned the expected four-field notice. Full returned documents and errors are retained in the log. Settings are included in the command and original JSON.

| Log label / command | Prediction recorded before execution | Actual outcome | JSON duration / shell duration | Exit |
| --- | --- | --- | --- | --- |
| `baseline-majority`: `bash run.sh write BASE-MAJ majority` | Two of three data-bearing voters must acknowledge; write should succeed. | Acknowledged | 109 ms / 0.499 s | 0 |
| `baseline-one`: `bash run.sh write BASE-ONE 1` | Only the primary must acknowledge; write should succeed. | Acknowledged | 46 ms / 0.378 s | 0 |
| `baseline-read-primary`: `bash run.sh read BASE-MAJ primary` | Primary/local read should return the BASE-MAJ notice. | Present | 50 ms / 0.39 s | 0 |
| `baseline-read-secondary`: `bash run.sh read BASE-MAJ secondary` | Secondary/local read will likely return the same notice, but replication lag could produce absence. | Present | 41 ms / 0.364 s | 0 |
| `failover-read`: `bash run.sh read BASE-MAJ secondary` | A remaining secondary can return BASE-MAJ; election timing may cause a temporary failure. | Present | 41 ms / 0.401 s | 0 |
| `failover-first-write`: `bash run.sh write FAILOVER-01 majority` | Majority write may wait/fail during election or succeed if graceful shutdown already handed over. | Acknowledged | 532 ms / 0.877 s | 0 |
| `failover-second-write`: `bash run.sh write FAILOVER-02 majority` | With mongo2 primary and mongo3 secondary, two acknowledgements should succeed. | Acknowledged | 56 ms / 0.378 s | 0 |
| `ack-all-write`: `bash run.sh write ACK-ALL 3` | Two live members cannot acknowledge w=3: expect write concern timeout after about 2000 ms, possibly with n=1. | UnknownWriteOutcome: waiting for replication timed out | 2052 ms / 2.376 s | 1 |
| `ack-all-read`: `bash run.sh read ACK-ALL primary` | The timed-out insert likely exists on primary despite incomplete acknowledgements. | Present | 45 ms / 0.364 s | 0 |
| `no-majority-read-primary`: `bash run.sh read BASE-MAJ primary` | No primary is available: expect UnknownReadFailure from server selection, not absence. | UnknownReadFailure: getaddrinfo EAI_AGAIN mongo1 | 3034 ms / 3.369 s | 1 |
| `no-majority-read-secondary`: `bash run.sh read BASE-MAJ secondary` | The surviving secondary should return the previously replicated BASE-MAJ with local concern. | Present | 46 ms / 3.375 s | 0 |
| `no-majority-write-one`: `bash run.sh write NO-MAJ-ONE 1` | w=1 cannot bypass primary selection: expect UnknownWriteOutcome, not acknowledgement. | UnknownWriteOutcome: getaddrinfo EAI_AGAIN mongo1 | 3033 ms / 3.349 s | 1 |
| `no-majority-write-majority`: `bash run.sh write NO-MAJ-MAJ majority` | No writable primary exists: expect UnknownWriteOutcome before acknowledgement is relevant. | UnknownWriteOutcome: getaddrinfo EAI_AGAIN mongo1 | 3035 ms / 3.39 s | 1 |
| `no-majority-verify`: `bash run.sh verify BASE-MAJ` | Primary/majority verification cannot read: expect UnknownReadFailure, never AbsentAtVerification. | UnknownReadFailure: getaddrinfo EAI_AGAIN mongo1 | 3033 ms / 3.358 s | 1 |
| `recovered-write`: `bash run.sh write RECOVERED-01 majority` | The restored majority should acknowledge a new RECOVERED-01 notice. | Acknowledged | 54 ms / 0.374 s | 0 |

### Interpretation

- **Baseline:** both reads returned identical values for `BASE-MAJ`. This establishes agreement for those observations, not that secondary reads are always current. No stale read was deliberately manufactured or observed.
- **Failover:** two surviving voters elected a replacement and acknowledged both majority writes. Service names stayed the same while roles changed.
- **Acknowledgement timeout:** `ACK-ALL` returned `UnknownWriteOutcome`, code 64 (`WriteConcernFailed`), with `wtimeout: true` and `n: 1`. Its following primary/local read returned the exact notice. The timeout describes missing acknowledgement; it is not a rollback or proof that nothing was stored.
- **No majority:** changing read preference for the same `BASE-MAJ` changed the outcome: primary failed, secondary returned the notice. Both writes failed regardless of `w`. A write still needs a writable primary before its acknowledgement policy can help.
- **Actual error detail:** the primary-only requests reported `getaddrinfo EAI_AGAIN mongo1`, with null code and code name, after approximately 3 seconds. This is the driver's exposed connection/discovery error for a stopped seed, not a returned `NotWritablePrimary` error or a write-concern timeout. The separate status evidence establishes that no primary was available. The trials do not isolate DNS behavior from server selection.
- **Failed verification:** `verify BASE-MAJ` during majority loss returned `UnknownReadFailure` and exit 1, without an `actual` document. It did not incorrectly classify this known-existing notice as absent.

### Original ACK-ALL output

Write, exit 1:

```json
{"action":"write","operationId":"ACK-ALL","startedAt":"2026-11-19T08:58:06.925Z","readPreference":"primary","readConcern":null,"writeConcern":{"w":3,"wtimeout":2000},"retryWrites":false,"retryReads":false,"outcome":"UnknownWriteOutcome","error":{"code":64,"codeName":"WriteConcernFailed","message":"waiting for replication timed out","errorLabels":[],"response":{"code":64,"codeName":"WriteConcernFailed","errmsg":"waiting for replication timed out","errInfo":{"wtimeout":true,"writeConcern":{"w":3,"wtimeout":2000,"provenance":"clientSupplied"}},"n":1,"electionId":{"$oid":"7fffffff0000000000000002"},"opTime":{"ts":{"$timestamp":{"t":1795078686,"i":2}},"t":2},"writeConcernError":{"code":64,"codeName":"WriteConcernFailed","errmsg":"waiting for replication timed out","errInfo":{"wtimeout":true,"writeConcern":{"w":3,"wtimeout":2000,"provenance":"clientSupplied"}}},"ok":1,"$clusterTime":{"clusterTime":{"$timestamp":{"t":1795078686,"i":2}},"signature":{"hash":{"$binary":{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAA=","subType":"00"}},"keyId":0}},"operationTime":{"$timestamp":{"t":1795078686,"i":2}}}},"finishedAt":"2026-11-19T08:58:08.977Z","durationMs":2052}
```

Following primary/local read, exit 0:

```json
{"action":"read","operationId":"ACK-ALL","startedAt":"2026-11-19T08:58:09.319Z","readPreference":"primary","readConcern":"local","writeConcern":null,"retryWrites":false,"retryReads":false,"actual":{"_id":"ACK-ALL","tripId":"T102","status":"Cancelled","sourceVersion":2},"outcome":"Present","finishedAt":"2026-11-19T08:58:09.364Z","durationMs":45}
```

---

## Read-only verification

Before the change, `compareChange` always threw `Error('TODO: compare the successful read with the expected change.')`. It now handles a successful empty query first, then compares the request identity and all three business fields using strict equality:

```js
// Your implementation. This function receives only successful query results.
// actual is a document or null; expected contains the four fields below.
// Return exactly: PresentMatches, PresentConflicts, or AbsentAtVerification.
// Compare _id, tripId, status and sourceVersion. Do not query or write here.
function compareChange(actual, expected) {
  if (actual === null) return 'AbsentAtVerification';

  if (actual._id !== expected._id ||
      actual.tripId !== expected.tripId ||
      actual.status !== expected.status ||
      actual.sourceVersion !== expected.sourceVersion) {
    return 'PresentConflicts';
  }

  return 'PresentMatches';
}
```

The function owns only comparison of a successful read. It does not connect, mutate, retry or classify database failures. Missing required fields conflict with the supplied complete expected notice; strict equality also rejects type mismatches. Additional unrelated fields are not part of this contract. The existing wrapper owns read preference, read concern, error handling and output labels.

An ID-only comparison was rejected because a notice for the right request with the wrong trip, status or source version would falsely match. Retrying or catching read failures inside this function was rejected because that would mix comparison with the wrapper's responsibilities. No abstraction or change to the supplied runtime was needed.

`bash run.sh check` exited 0:

```json
{"test":"no document","outcome":"Pass"}
{"test":"matching document","outcome":"Pass"}
{"test":"different ID","outcome":"Pass"}
{"test":"different trip","outcome":"Pass"}
{"test":"different status","outcome":"Pass"}
{"test":"different version","outcome":"Pass"}
{"test":"missing version","outcome":"Pass"}
{"passed":7,"failed":0}
```

After recovery, a new `RECOVERED-01` majority write succeeded. Then:

```sh
bash run.sh verify BASE-MAJ BASE-ONE FAILOVER-01 FAILOVER-02 \
  ACK-ALL NO-MAJ-ONE NO-MAJ-MAJ RECOVERED-01 NEVER-SENT
```

| Operation ID | Earlier write outcome | Verification result | What this establishes / leaves uncertain |
| --- | --- | --- | --- |
| `BASE-MAJ` | Acknowledged | `PresentMatches` | All four expected fields match in this primary/majority read. |
| `BASE-ONE` | Acknowledged | `PresentMatches` | All four expected fields match in this primary/majority read. |
| `FAILOVER-01` | Acknowledged | `PresentMatches` | All four expected fields match in this primary/majority read. |
| `FAILOVER-02` | Acknowledged | `PresentMatches` | All four expected fields match in this primary/majority read. |
| `ACK-ALL` | UnknownWriteOutcome | `PresentMatches` | All four expected fields match in this primary/majority read. The earlier timeout did not mean absence. |
| `NO-MAJ-ONE` | UnknownWriteOutcome | `AbsentAtVerification` | Successful primary/majority read returned null at this observation. |
| `NO-MAJ-MAJ` | UnknownWriteOutcome | `AbsentAtVerification` | Successful primary/majority read returned null at this observation. |
| `RECOVERED-01` | Acknowledged | `PresentMatches` | All four expected fields match in this primary/majority read. |
| `NEVER-SENT` | Not attempted (control) | `AbsentAtVerification` | Successful primary/majority read returned null at this observation. Deliberately unwritten control. |

Verification exited 0: six matching notices and three absent IDs. `ACK-ALL` matched, whereas `NEVER-SENT` returned null. The two no-majority writes were also absent at verification. Neither an earlier error nor a later absence is a universal guarantee that an uncertain write could never appear; retain the same logical request ID for reconciliation.

The recovered majority read establishes majority-committed visibility of `ACK-ALL` at that observation. It does not retroactively turn its original `w: 3` timeout into a successful three-acknowledgement response.

---

## Application policy

These are design decisions informed by this experiment, not implemented production features. The lab did not stop PostgreSQL or execute real purchases or reporting jobs.

| Operation | Freshness/consistency needed | During an outage | What the user sees | After recovery | Reason / evidence |
| --- | --- | --- | --- | --- | --- |
| Journey search | A copied timetable can support advisory search, but cannot guarantee a departure is still running. | If the copy is reachable, allow informational search with explicit staleness status; otherwise show unavailable. Do not infer that an absent cancellation proves a trip is running. | Last-known timetable with a warning that current service status cannot be confirmed. Known cancellations remain visible. | Resume synchronization from the authoritative timetable, apply source versions and refresh results before clearing the warning. | The lone secondary still returned `BASE-MAJ`; copied information can remain readable without a primary. This favors availability for search while accepting stale information, not for booking decisions. |
| Ticket purchase | Authoritative timetable, capacity and transactional booking/payment decisions are required. | Pause confirmation while PostgreSQL is unavailable. Do not sell from the MongoDB search copy. Keep any already-submitted uncertain operation identifiable for reconciliation. | Purchase unavailable before submission, or pending/unknown after an uncertain submission; never show a confirmed ticket without authoritative confirmation. | Query the authoritative booking/payment outcome under the original logical request ID, then confirm or safely retry under the application's idempotency rules. | `ACK-ALL` existed despite an error, so blindly creating another business request could duplicate work. Lowering `w` also did not restore writes without a primary. These observations motivate the policy but do not test the booking implementation. |
| Reporting | Historical, explicitly dated snapshots can tolerate lag; current/final totals require authoritative completeness. | Show the last completed snapshot with its data cutoff; defer fresh reports and final settlement totals. | A stale/as-of report, or a pending report request, without claiming current totals. | Re-run or resume from the last completed checkpoint and reconcile against authoritative records before publishing a new complete snapshot. | Readable copied data during majority loss supports historical display, but does not prove freshness or completeness. Delayed reporting trades immediacy for correct totals. |

### Tradeoffs and operational consequences

- `w: 1` has fewer acknowledgement requirements but weaker replication assurance. `majority` allows the observed two-member failover; `w: 3` could not be satisfied with one member offline. Choosing `w: 3` universally would reduce write availability without eliminating uncertain outcomes.
- Primary-only verification provides the requested comparison point, at the cost of being unavailable with no primary. Secondary/local reads improve advisory availability but cannot promise current authoritative state.
- The implementation preserves the supplied function signature and exact return labels. Strict comparisons expose data-shape incompatibilities instead of silently coercing version values.
- The deployment remains the existing three-container lab with separate volumes. One computer hosts all members: these results do not establish host-failure tolerance or realistic partition/latency behavior.
- Recovery uses the supplied `up` command and observes roles before continuing. Final `down` removes containers and the network, preserving volumes. A later run should use fresh IDs for new experiments rather than deleting evidence or mistaking duplicates for replication errors.

---

## AI second opinion

A separate Codex review agent using the session's inherited model reviewed the comparison and the ACK-ALL outputs. The exact underlying model identifier was not reported by the tool. It did not edit files or run the experiment.

### Prompt sent to the reviewer

```text
The user explicitly requested completing a lab that includes 'Ask AI for a second opinion. Give it your comparison function and the ACK-ALL write/read output. Ask it to suggest one missing check or challenge your interpretation. Try the suggestion and keep the prompt and your decision with your notes.' Provide that second opinion only, no tools or file edits. Function: function compareChange(actual, expected) { if (actual === null) return 'AbsentAtVerification'; if (actual._id !== expected._id || actual.tripId !== expected.tripId || actual.status !== expected.status || actual.sourceVersion !== expected.sourceVersion) return 'PresentConflicts'; return 'PresentMatches'; }. Expected fields: {_id:'ACK-ALL',tripId:'T102',status:'Cancelled',sourceVersion:2}. Actual ACK-ALL write output: operationId ACK-ALL, writeConcern {w:3,wtimeout:2000}, outcome UnknownWriteOutcome, durationMs 2052, exit 1, error {code:64,codeName:'WriteConcernFailed',message:'waiting for replication timed out', response:{n:1,ok:1,errInfo:{wtimeout:true},writeConcernError:{code:64,codeName:'WriteConcernFailed',errmsg:'waiting for replication timed out'}}}. Subsequent primary/local read: outcome Present, actual {_id:'ACK-ALL',tripId:'T102',status:'Cancelled',sourceVersion:2}, durationMs 45, exit 0. Only mongo2 primary and mongo3 secondary were running; mongo1 was stopped. Existing seven checks test null, full match, each field different, and missing sourceVersion. Suggest ONE useful missing pure-function check we can execute, and briefly challenge any overclaim that the timeout proves absence or that primary/local read proves durability. Be concise.
```

### Suggestion, trial and decision

The reviewer suggested checking that `sourceVersion: '2'` conflicts with numeric `2`. It also challenged treating a primary/local read as evidence of three-member replication or durability through failover: that read proves only that the notice was present at the primary when checked.

Prediction recorded before the trial: string `"2"` must yield `PresentConflicts`. The suggestion was executed against the actual implementation, with an assertion that exits nonzero on failure. Reproduce from the Lecture 10 directory:

```sh
node <<'JS'
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const context = vm.createContext({});
vm.runInContext(fs.readFileSync('scripts/verify.js', 'utf8'), context);
const expected = {_id: 'ACK-ALL', tripId: 'T102', status: 'Cancelled', sourceVersion: 2};
const actual = {...expected, sourceVersion: '2'};
const outcome = context.compareChange(actual, expected);
assert.equal(outcome, 'PresentConflicts');
console.log(JSON.stringify({test: 'string sourceVersion conflicts with numeric version', actual, expected, outcome, result: 'Pass'}));
JS
```

Actual output, exit 0:

```json
{"test":"string sourceVersion conflicts with numeric version","actual":{"_id":"ACK-ALL","tripId":"T102","status":"Cancelled","sourceVersion":"2"},"expected":{"_id":"ACK-ALL","tripId":"T102","status":"Cancelled","sourceVersion":2},"outcome":"PresentConflicts","result":"Pass"}
```

Decision: accept the extra type check and retain strict comparison. No implementation change was necessary because `!==` already rejects numeric strings. Accept the interpretation challenge: the immediate local read establishes presence, and the separate recovered majority read establishes majority-committed visibility later. Neither proves the original three-member acknowledgement succeeded.

---

## Reflection

The experiments demonstrate that read availability, primary election, acknowledgement and later verification are distinct. A failed acknowledgement can coexist with a stored notice, and a failed read must remain unknown rather than becoming absence. All supplied comparison checks and the additional type check passed; all attempted IDs were reconciled after recovery.

These local graceful-stop trials do not establish behavior under every network partition, abrupt host failure, rollback, concurrent update or production retry policy. Before using the policy in a real product, test those conditions and the PostgreSQL booking/payment reconciliation path. The report's application choices are proposals for the student to assess and explain, not measured production guarantees.

---

## Resources

- [Starter README and contracts](../README.md)
- [Verifier implementation](../scripts/verify.js)
- [Original predictions and complete experiment output](evidence/experiments.jsonl)
- Week 46 course notes: `01 - Databaser for udviklere/03 - Opgaver/Uge 46/00 - Oversigt.md` in the SoftwareudviklingNotes vault, and its linked distributed-database concept notes.
