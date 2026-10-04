# Lecture 05 — MongoDB journey search

## Scope

PostgreSQL remains the source of truth for routes, trips, products and
tickets. MongoDB contains a separate read-oriented journey-search model.

The search supports direct journeys between an exact origin and destination.
All timestamps are UTC. The start of the interval is inclusive and the end is
exclusive.

## Schema validation

The validator rejected:

- `departureUtc` stored as a string;
- `availableSeats` stored as `-1`.

Both writes failed with validation error `121`.

## Journey search

The embedded model requires the departures array to be unwound before returning
one result per departure. Only Scheduled departures inside the requested time
interval are returned.

The boundary tests confirmed that 06:20 is excluded when it is the end of the
interval and included when it is the start.

## Array-query bug

The broken query used independent dotted conditions for status and departure
time. Document B matched because one array element was Scheduled while another
array element was inside the requested interval.

`$elemMatch` fixed the query by requiring both conditions to be true for the
same departure.

## Duplicated data

`LAB05-T-OK` is stored in documents A and C.

Cancelling only A made the Airport and Central searches disagree. Updating the
remaining copy repaired the inconsistency.

Repeating the complete cancellation still matched two documents because both
retained the trip ID, but `modifiedCount` was zero because their status was
already Cancelled.

MongoDB's single-document atomicity does not automatically synchronize copies
stored in separate documents.

## Displayed price

The price was updated to 40.00 DKK for A, C, E and F.

B retained its original price because it has another route ID. D retained its
original price because it belongs to another city even though it has the same
route ID.

The MongoDB price belongs to the search model. Historical ticket prices in
PostgreSQL were not changed.

## Document growth

See `growth.txt` for the exact BSON sizes measured with 100 and 1,000
departures.

The departures array must have an explicit bound. A service-date bucket is
possible when the maximum number of departures is realistically bounded.
Otherwise, individual departure documents avoid unbounded document growth.

## Model comparison

| Concern | Embedded departures | One departure per document |
|---|---|---|
| Search | Requires `$unwind` or `$filter` | Direct field query |
| Returned shape | Must be flattened | Already matches one result |
| Status update | Requires an array filter | Direct `$set` |
| Destination duplication | Present | Still present |
| Growth | One document becomes larger | Collection gains documents |
| Atomic boundary | Journey document | One departure and stop pair |
| Main risk | Array-query mistakes and unbounded growth | More documents and duplicated outer fields |

## Decision

I would continue with `journey_search_by_trip`.

The passenger-facing result is already one record per departure, so the flat
model matches the read result. Search and status updates are simpler, and
adding departures grows the collection instead of one document.

A bounded document per origin, destination and service date is a credible
alternative when a whole day's departures are usually read together. It was
not selected because it retains array-query complexity and requires a
defensible maximum bucket size.
