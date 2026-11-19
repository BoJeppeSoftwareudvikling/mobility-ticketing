# Mobility Ticketing cancellation lab report

A cancellation notice can be stored even when the client receives an error. That was the central result of this lab: MongoDB timed out while waiting for three acknowledgements, but the cancellation was readable immediately afterwards and still matched after recovery.

The experiments also showed that a database can remain readable while it cannot accept new writes. These findings lead to different choices for Mobility Ticketing: journey search can use an explicitly marked timetable copy, ticket purchases must wait for authoritative confirmation, and reporting can show a previous snapshot while fresh reports are delayed.

---

## Purpose and setup

Mobility Ticketing uses copied timetable data to support journey search. If a cancellation fails to reach that copy, a passenger may see a departure that will not run. The lab investigated what could still be read, written and verified as MongoDB members became unavailable.

The Lecture 10 starter supplied three MongoDB 8.0 members: `mongo1`, `mongo2` and `mongo3`. Together they formed a **replica set**, a group of database processes that maintain copies of the same data. One member acted as the **primary**, accepting writes, while the other two acted as **secondaries**, copying those changes. A new primary could be elected while at least two of the three voting members could communicate.

Each experiment attempted to store the same cancellation under a different request ID:

```json
{
  "_id": "BASE-MAJ",
  "tripId": "T102",
  "status": "Cancelled",
  "sourceVersion": 2
}
```

The request ID kept separate experiments distinguishable. The other fields identified the trip, the intended cancellation and its source version. These were synthetic notices: PostgreSQL continued to own the real timetable and booking decisions.

Before each command, an expected result was recorded. The actual response was then saved, including errors. Automatic client retries were disabled so that one command represented one application attempt. The member roles were checked at each stage because a service name such as `mongo2` does not determine whether it is currently primary or secondary.

---

## Experiment process and results

### Establishing normal operation

Initially, `mongo1` was primary and `mongo2` and `mongo3` were secondaries. Two notices were written using different **write concerns**, which specify how much acknowledgement the client requests.

`BASE-MAJ` used `w: majority`, requiring acknowledgement from two members in this configuration. `BASE-ONE` used `w: 1`, requiring acknowledgement from the primary. Both writes succeeded, as predicted.

The next step read `BASE-MAJ` first with a primary read preference and then with a secondary read preference. Both returned the expected cancellation. This demonstrated that the notice was available through both read paths at those moments. It did not establish that secondaries are always current: replication takes time, so another read could occur before a secondary has applied a change.

### Stopping the primary

The original primary, `mongo1`, was stopped. The immediate secondary read still returned `BASE-MAJ`, and the first new majority write, `FAILOVER-01`, succeeded. A status check then showed `mongo2` as the replacement primary and `mongo3` as its remaining secondary. A second majority write, `FAILOVER-02`, also succeeded.

The prediction allowed either a temporary failure during election or a successful request after handover. In this run, the shutdown command took about 10.3 seconds and the first write reported 532 ms. The successful request therefore shows that the remaining members could resume work; it does not prove that every request would succeed throughout an election.

The important observation was that two members were enough to elect a primary and satisfy majority acknowledgement. The stopped member was still part of the configured set, but its absence did not prevent those operations.

### Requesting an acknowledgement that could not arrive

With `mongo1` still stopped, the next write used request ID `ACK-ALL` and `w: 3`. This required all three members to acknowledge, even though only two were running. The expected result was a timeout, with the possibility that the notice had already been stored.

The write returned `UnknownWriteOutcome` after 2,052 ms. MongoDB reported `WriteConcernFailed` with the message “waiting for replication timed out.” The response also contained `n: 1`, indicating that one document had been inserted.

A subsequent read from the primary returned the complete `ACK-ALL` notice. The error and the stored document were both real: the write had happened, but the requested acknowledgement had not been obtained within the deadline. The timeout did not undo the insert.

This distinction matters for retry decisions. Treating the error as proof that nothing happened could cause an application to repeat a business operation unnecessarily. The safer next step is to retain the logical request ID and inspect its outcome. The immediate read established presence on the primary; the later recovery check supplied the stronger majority-read observation.

### Losing the majority

All three members were restored before the next experiment. `mongo2` was then primary, so `mongo1` and `mongo3` were stopped while `mongo2` remained running.

The first status check still described `mongo2` as primary. The experiment waited until a later check showed it had stepped down to secondary. This was necessary to test the state after recognised majority loss, rather than the short transition before it.

The following operations were then run one at a time:

| Operation | Expected behaviour | Observed result |
| --- | --- | --- |
| Read `BASE-MAJ` from a primary | Fail because no primary is available | `UnknownReadFailure` |
| Read the same notice from a secondary | Return the existing copy if it remains readable | Complete cancellation returned |
| Write `NO-MAJ-ONE` with `w: 1` | Fail because a write still needs a primary | `UnknownWriteOutcome` |
| Write `NO-MAJ-MAJ` with `w: majority` | Fail for the same reason | `UnknownWriteOutcome` |
| Verify `BASE-MAJ` using a primary and majority read concern | Remain unknown if the read cannot complete | `UnknownReadFailure` |

Changing the read preference changed what could be read. Changing the write concern did not restore the ability to write. `w: 1` reduces the acknowledgement requirement, but it does not authorise a secondary to accept application writes.

The failed requests exposed the error `getaddrinfo EAI_AGAIN mongo1` after roughly three seconds. This was a connection or discovery error involving a stopped member, rather than a returned write-concern error. The separate status check established that the surviving member was secondary. These observations should be read together; the error text alone does not establish why no primary was available.

The failed verification was particularly useful. `BASE-MAJ` was known to exist and was still readable from the secondary, yet the verifier could not complete its required primary read. It correctly reported an unknown result instead of claiming the notice was absent.

### Restoring the set and checking every request

The stopped members were restarted, and the setup command waited for one primary and two secondaries. A new majority write, `RECOVERED-01`, succeeded. Every attempted request ID was then verified, together with `NEVER-SENT`, an ID deliberately left unwritten as a control.

Verification read from the primary with **majority read concern**, which restricts the result to majority-committed data. It then compared the returned notice with the expected values.

| Request IDs | Result after recovery |
| --- | --- |
| `BASE-MAJ`, `BASE-ONE` | Present and matching |
| `FAILOVER-01`, `FAILOVER-02` | Present and matching |
| `ACK-ALL` | Present and matching, despite the original timeout |
| `NO-MAJ-ONE`, `NO-MAJ-MAJ` | Absent at verification |
| `RECOVERED-01` | Present and matching |
| `NEVER-SENT` | Absent at verification, as expected |

There were six matching notices and three absent IDs. The comparison between `ACK-ALL` and `NEVER-SENT` made the main finding concrete: an error response did not mean the same thing as an unwritten request.

Absence describes the successful read at that point in time. It is not a general guarantee that an uncertain operation could never appear later. After the experiments, the lab containers and network were stopped and removed, while the database volumes were preserved.

---

## Implementation and verification choices

The only application code changed was `compareChange(actual, expected)` in `scripts/verify.js`. The starter implementation always threw a TODO error. The completed function first handles `actual === null`, then compares `_id`, `tripId`, `status` and `sourceVersion` using strict equality.

It returns `AbsentAtVerification` when a successful query found no document, `PresentMatches` when all four fields match, and `PresentConflicts` when a document exists but a required field differs or is missing.

Checking only the request ID was rejected because the correct ID could still refer to the wrong trip, status or version. Comparing all four fields answers the actual business question: whether the intended cancellation notice is present. Strict equality also prevents the string `"2"` from silently matching numeric version `2`.

Database access and error handling remain in the supplied wrapper. The comparison function receives only successful query results, so it cannot accidentally turn a connection failure into “not found.” Keeping those responsibilities separate also preserves the starter's interface and avoids adding retries or writes to a verification operation.

All seven supplied checks passed. They covered an absent document, a complete match, a difference in each of the four fields, and a missing version. A separate AI second opinion suggested testing a string version against the expected numeric version. That additional check passed with `PresentConflicts`; the existing strict comparison already handled it correctly.

The review also challenged an overly strong interpretation of the first `ACK-ALL` read. A primary read with local concern establishes that the document was present there when read. It does not establish three-member acknowledgement. The recovered majority read strengthens the observation without changing the fact that the original write timed out.

---

## Choices for Mobility Ticketing

The application choices below concern what should happen when authoritative data is unavailable. They are informed by the MongoDB experiments; the lab itself did not interrupt PostgreSQL or run real booking and payment transactions.

### Journey search can show a clearly marked copy

Journey search should remain available when a timetable copy can still be read. The user should see a warning such as “Showing the last known timetable; current departure status cannot be confirmed.” Known cancellations should remain visible. If no copy can be read, the search should report that it is unavailable.

This choice follows from the successful secondary read during majority loss. The copied information still had value even though writes were unavailable. The cost is possible staleness: a missing cancellation in the copy must not be presented as proof that a departure is running.

The alternative is to disable all search whenever authoritative data cannot be reached. That avoids showing potentially outdated results, but also removes information that may still help passengers. Allowing clearly qualified search is the chosen balance. After recovery, synchronization should resume from the authoritative timetable and refresh the results before the warning is removed.

### Ticket purchase requires authoritative confirmation

A purchase should not be confirmed from the timetable copy alone. The real timetable, capacity and booking rules must be checked through the authoritative system. If that system is unavailable before submission, the user should see that purchasing is temporarily unavailable. If a previously submitted purchase has an uncertain result, it should remain pending rather than being shown as failed or confirmed without evidence.

`ACK-ALL` explains why this distinction matters: an error can be returned after an operation has taken effect. After recovery, the application should look up the original booking and payment request before deciding whether to confirm it or retry. A retry must follow the application's rules for preventing duplicate processing.

The alternative of accepting purchases solely from the search copy would improve apparent availability, but could confirm a cancelled trip or rely on outdated capacity. The chosen policy accepts temporary purchase unavailability to protect the correctness of confirmed tickets. Implementing and testing that policy in the booking system remains separate from this lab.

### Reporting can use a previous completed snapshot

Historical reporting can show the last completed snapshot while authoritative data is unavailable, provided the report clearly states the time through which its data is complete. Requests for fresh reports should wait. Final totals that depend on current, complete data should not be presented as settled during the outage.

The successful secondary read supports retaining access to previously copied information. It does not establish that a report contains every recent transaction. Showing a dated snapshot offers useful continuity without claiming current totals.

The alternative is to block every report, including historical views. That is unnecessarily restrictive where older figures remain useful. After recovery, reporting should resume from its last completed checkpoint or run again, reconcile with authoritative records, and publish a new completed snapshot.

---

## Reflection

The lab demonstrates why storing a change, acknowledging it and later verifying it are separate events. `ACK-ALL` was stored despite an acknowledgement timeout. With only one member left, existing data remained readable through a secondary while new writes and primary-based verification failed. Recovery made it possible to distinguish matching notices from requests absent at verification.

For Mobility Ticketing, these results support different availability choices for different operations. Advisory search and historical reports can tolerate an explicitly described older copy. Confirmed purchases require authoritative decisions, and uncertain outcomes require reconciliation under the original logical request ID.

The experiments used graceful container shutdowns on one computer. They do not establish behaviour under host failure, arbitrary network partitions or concurrent production traffic. The next useful investigation is to test the real booking and payment path with delayed responses and retries, ensuring that an uncertain response cannot create a duplicate purchase.

---

## Resources

- [Detailed evidence, predictions and AI review](evidence.md)
- [Full command output](evidence/experiments.jsonl)
- [Comparison function](../scripts/verify.js)
- [Lecture 10 setup and contracts](../README.md)
