const m = db.getSiblingDB("mobility");

function requireNonEmptyString(value, name) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`${name} must be a non-empty string`);
  }
}

function requireDate(value, name) {
  if (!(value instanceof Date) || Number.isNaN(value.valueOf())) {
    throw new Error(`${name} must be a valid Date`);
  }
}

function search(cityId, fromStopId, toStopId, start, end) {
  requireNonEmptyString(cityId, "cityId");
  requireNonEmptyString(fromStopId, "fromStopId");
  requireNonEmptyString(toStopId, "toStopId");
  requireDate(start, "start");
  requireDate(end, "end");

  if (end <= start) {
    throw new Error("end must be later than start");
  }

  return m.journey_search
    .aggregate([
      {
        $match: {
          cityId,
          fromStopId,
          toStopId,
        },
      },
      {
        $unwind: "$departures",
      },
      {
        $match: {
          "departures.status": "Scheduled",
          "departures.departureUtc": {
            $gte: start,
            $lt: end,
          },
        },
      },
      {
        $project: {
          _id: 0,
          cityId: 1,
          routeId: 1,
          fromStopId: 1,
          toStopId: 1,
          tripId: "$departures.tripId",
          departureUtc: "$departures.departureUtc",
          arrivalUtc: "$departures.arrivalUtc",
          availableSeats: "$departures.availableSeats",
          price: 1,
          currency: 1,
        },
      },
      {
        $sort: {
          departureUtc: 1,
        },
      },
    ])
    .toArray();
}

function assertTripIds(label, results, expectedTripIds) {
  const actualTripIds = results.map((result) => result.tripId);

  if (actualTripIds.join("|") !== expectedTripIds.join("|")) {
    throw new Error(
      `${label}: expected [${expectedTripIds}], got [${actualTripIds}]`,
    );
  }

  print(`${label}: PASS`);
  printjson(results);
}

function expectInputError(label, operation) {
  try {
    operation();
  } catch (error) {
    print(`${label}: PASS — ${error.message}`);
    return;
  }

  throw new Error(`${label}: invalid input was accepted`);
}

const october2At0600 = ISODate("2026-10-02T06:00:00Z");
const october2At0620 = ISODate("2026-10-02T06:20:00Z");
const october2At0700 = ISODate("2026-10-02T07:00:00Z");
const october3At0600 = ISODate("2026-10-03T06:00:00Z");
const october3At0700 = ISODate("2026-10-03T07:00:00Z");

assertTripIds(
  "Nørreport to Airport, 06:00–07:00",
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0700,
  ),
  ["LAB05-T-OK"],
);

assertTripIds(
  "End at 06:20 is exclusive",
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0620,
  ),
  [],
);

assertTripIds(
  "Start at 06:20 is inclusive",
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0620,
    october2At0700,
  ),
  ["LAB05-T-OK"],
);

assertTripIds(
  "Airport to Nørreport",
  search(
    "CPH",
    "STOP-AIRPORT",
    "STOP-NORREPORT",
    october2At0600,
    october2At0700,
  ),
  ["LAB05-T-E"],
);

assertTripIds(
  "Nørreport to Airport on 3 October",
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october3At0600,
    october3At0700,
  ),
  ["LAB05-T-F"],
);

assertTripIds(
  "Unknown destination",
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-NO-MATCH",
    october2At0600,
    october2At0700,
  ),
  [],
);

expectInputError("Empty origin", () =>
  search(
    "CPH",
    "",
    "STOP-AIRPORT",
    october2At0600,
    october2At0700,
  ),
);

expectInputError("End equals start", () =>
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0600,
  ),
);

expectInputError("End before start", () =>
  search(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0700,
    october2At0600,
  ),
);
