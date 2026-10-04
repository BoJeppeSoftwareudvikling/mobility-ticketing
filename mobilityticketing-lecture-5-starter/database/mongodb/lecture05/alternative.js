const m = db.getSiblingDB("mobility");

if (!m.getCollectionNames().includes("journey_search_by_trip")) {
  m.createCollection("journey_search_by_trip");
} else {
  m.journey_search_by_trip.deleteMany({
    $or: [
      { _id: /^LAB05:/ },
      { tripId: /^LAB05-/ },
    ],
  });
}

const journeys = m.journey_search
  .find({ _id: /^LAB05:[A-F]$/ })
  .sort({ _id: 1 })
  .toArray();

const tripDocuments = [];

for (const journey of journeys) {
  for (const departure of journey.departures) {
    tripDocuments.push({
      _id: [
        journey.cityId,
        journey.routeId,
        journey.fromStopId,
        journey.toStopId,
        departure.tripId,
      ].join(":"),

      cityId: journey.cityId,
      routeId: journey.routeId,
      fromStopId: journey.fromStopId,
      toStopId: journey.toStopId,

      tripId: departure.tripId,
      departureUtc: departure.departureUtc,
      arrivalUtc: departure.arrivalUtc,
      status: departure.status,
      availableSeats: departure.availableSeats,

      price: journey.price,
      currency: journey.currency,
      schemaVersion: journey.schemaVersion,
    });
  }
}

m.journey_search_by_trip.insertMany(tripDocuments);

const documentCount =
  m.journey_search_by_trip.countDocuments({
    tripId: /^LAB05-/,
  });

if (documentCount !== 10) {
  throw new Error(
    `Expected 10 alternative documents, got ${documentCount}`,
  );
}

printjson({
  alternativeDocuments: documentCount,
});

function requireNonEmptyString(value, name) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`${name} must be a non-empty string`);
  }
}

function searchByTrip(
  cityId,
  fromStopId,
  toStopId,
  start,
  end,
) {
  requireNonEmptyString(cityId, "cityId");
  requireNonEmptyString(fromStopId, "fromStopId");
  requireNonEmptyString(toStopId, "toStopId");

  if (
    !(start instanceof Date) ||
    !(end instanceof Date) ||
    Number.isNaN(start.valueOf()) ||
    Number.isNaN(end.valueOf())
  ) {
    throw new Error("start and end must be valid dates");
  }

  if (end <= start) {
    throw new Error("end must be later than start");
  }

  return m.journey_search_by_trip
    .find(
      {
        cityId,
        fromStopId,
        toStopId,
        status: "Scheduled",
        departureUtc: {
          $gte: start,
          $lt: end,
        },
      },
      {
        _id: 0,
        cityId: 1,
        routeId: 1,
        fromStopId: 1,
        toStopId: 1,
        tripId: 1,
        departureUtc: 1,
        arrivalUtc: 1,
        availableSeats: 1,
        price: 1,
        currency: 1,
      },
    )
    .sort({ departureUtc: 1 })
    .toArray();
}

function assertTripIds(label, results, expectedTripIds) {
  const actualTripIds = results.map((result) => result.tripId);

  if (actualTripIds.join("|") !== expectedTripIds.join("|")) {
    throw new Error(
      `${label}: expected [${expectedTripIds}], ` +
      `got [${actualTripIds}]`,
    );
  }

  print(`${label}: PASS`);
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
  "Nørreport to Airport",
  searchByTrip(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0700,
  ),
  ["LAB05-T-OK"],
);

assertTripIds(
  "End at 06:20",
  searchByTrip(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0620,
  ),
  [],
);

assertTripIds(
  "Start at 06:20",
  searchByTrip(
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
  searchByTrip(
    "CPH",
    "STOP-AIRPORT",
    "STOP-NORREPORT",
    october2At0600,
    october2At0700,
  ),
  ["LAB05-T-E"],
);

assertTripIds(
  "Search on 3 October",
  searchByTrip(
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
  searchByTrip(
    "CPH",
    "STOP-NORREPORT",
    "STOP-NO-MATCH",
    october2At0600,
    october2At0700,
  ),
  [],
);

expectInputError("Empty origin", () =>
  searchByTrip(
    "CPH",
    "",
    "STOP-AIRPORT",
    october2At0600,
    october2At0700,
  ),
);

expectInputError("End equals start", () =>
  searchByTrip(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0600,
  ),
);

expectInputError("End before start", () =>
  searchByTrip(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0700,
    october2At0600,
  ),
);

const cancellation =
  m.journey_search_by_trip.updateMany(
    {
      tripId: "LAB05-T-OK",
    },
    {
      $set: {
        status: "Cancelled",
      },
    },
  );

print("Cancellation in alternative model:");
printjson(cancellation);

if (
  cancellation.matchedCount !== 2 ||
  cancellation.modifiedCount !== 2
) {
  throw new Error(
    "Alternative cancellation should change two documents",
  );
}

assertTripIds(
  "Cancelled trip is absent",
  searchByTrip(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    october2At0600,
    october2At0700,
  ),
  [],
);
