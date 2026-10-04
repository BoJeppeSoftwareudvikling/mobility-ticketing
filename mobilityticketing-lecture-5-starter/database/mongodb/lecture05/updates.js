const m = db.getSiblingDB("mobility");

const fixtureDocuments = m.journey_search
  .find({ _id: /^LAB05:[A-F]$/ })
  .sort({ _id: 1 })
  .toArray();

if (fixtureDocuments.length !== 6) {
  throw new Error(
    `Expected six fixture documents, got ${fixtureDocuments.length}`,
  );
}

function restoreFixture() {
  m.journey_search.deleteMany({ _id: /^LAB05:/ });
  m.journey_search.insertMany(fixtureDocuments);
}

function searchTripIds(fromStopId, toStopId, start, end) {
  return m.journey_search
    .aggregate([
      {
        $match: {
          cityId: "CPH",
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
        $sort: {
          "departures.departureUtc": 1,
        },
      },
      {
        $project: {
          _id: 0,
          tripId: "$departures.tripId",
        },
      },
    ])
    .toArray()
    .map((result) => result.tripId);
}

function assertTripIds(label, actual, expected) {
  if (actual.join("|") !== expected.join("|")) {
    throw new Error(
      `${label}: expected [${expected}], got [${actual}]`,
    );
  }

  print(`${label}: PASS`);
}

function cancelEveryCopy() {
  return m.journey_search.updateMany(
    {
      _id: /^LAB05:/,
      "departures.tripId": "LAB05-T-OK",
    },
    {
      $set: {
        "departures.$[trip].status": "Cancelled",
      },
    },
    {
      arrayFilters: [
        {
          "trip.tripId": "LAB05-T-OK",
        },
      ],
    },
  );
}

const at0600 = ISODate("2026-10-02T06:00:00Z");
const at0700 = ISODate("2026-10-02T07:00:00Z");
const at0800 = ISODate("2026-10-02T08:00:00Z");

// Experiment 1: cancel every copy.
print("Experiment 1: cancel every copy");

const firstCancellation = cancelEveryCopy();
printjson(firstCancellation);

if (
  firstCancellation.matchedCount !== 2 ||
  firstCancellation.modifiedCount !== 2
) {
  throw new Error(
    "First cancellation should match and modify two documents",
  );
}

assertTripIds(
  "Cancelled trip is absent",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    at0600,
    at0700,
  ),
  [],
);

// Repeat the same update.
const repeatedCancellation = cancelEveryCopy();
printjson(repeatedCancellation);

if (
  repeatedCancellation.matchedCount !== 2 ||
  repeatedCancellation.modifiedCount !== 0
) {
  throw new Error(
    "Repeated cancellation should match two and modify zero",
  );
}

print(
  "The documents still match because they retain the trip ID. " +
  "modifiedCount is zero because status is already Cancelled.",
);

// Experiment 2: create inconsistency and repair it.
restoreFixture();
print("Experiment 2: create and repair inconsistent copies");

printjson(
  m.journey_search.updateOne(
    {
      _id: "LAB05:A",
      "departures.tripId": "LAB05-T-OK",
    },
    {
      $set: {
        "departures.$[trip].status": "Cancelled",
      },
    },
    {
      arrayFilters: [
        {
          "trip.tripId": "LAB05-T-OK",
        },
      ],
    },
  ),
);

assertTripIds(
  "Nørreport to Airport after cancelling A",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    at0600,
    at0700,
  ),
  [],
);

assertTripIds(
  "Nørreport to Central still sees the other copy",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-CENTRAL",
    at0600,
    at0700,
  ),
  ["LAB05-T-OK"],
);

const repairResult = m.journey_search.updateMany(
  {
    _id: { $ne: "LAB05:A" },
    departures: {
      $elemMatch: {
        tripId: "LAB05-T-OK",
        status: { $ne: "Cancelled" },
      },
    },
  },
  {
    $set: {
      "departures.$[trip].status": "Cancelled",
    },
  },
  {
    arrayFilters: [
      {
        "trip.tripId": "LAB05-T-OK",
      },
    ],
  },
);

print("Repair result:");
printjson(repairResult);

assertTripIds(
  "Airport search after repair",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    at0600,
    at0700,
  ),
  [],
);

assertTripIds(
  "Central search after repair",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-CENTRAL",
    at0600,
    at0700,
  ),
  [],
);

// Experiment 3: move every copy.
restoreFixture();
print("Experiment 3: move every copy to 07:05–07:25");

const moveResult = m.journey_search.updateMany(
  {
    "departures.tripId": "LAB05-T-OK",
  },
  {
    $set: {
      "departures.$[trip].departureUtc":
        ISODate("2026-10-02T07:05:00Z"),
      "departures.$[trip].arrivalUtc":
        ISODate("2026-10-02T07:25:00Z"),
    },
  },
  {
    arrayFilters: [
      {
        "trip.tripId": "LAB05-T-OK",
      },
    ],
  },
);

printjson(moveResult);

assertTripIds(
  "Moved trip is absent from 06:00–07:00",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    at0600,
    at0700,
  ),
  [],
);

assertTripIds(
  "Moved trip appears from 07:00–08:00",
  searchTripIds(
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    at0700,
    at0800,
  ),
  ["LAB05-T-EDGE", "LAB05-T-OK"],
);

// Experiment 4: change displayed price.
restoreFixture();
print("Experiment 4: change displayed search price");

const priceResult = m.journey_search.updateMany(
  {
    cityId: "CPH",
    routeId: "LINE-M2",
  },
  {
    $set: {
      price: NumberDecimal("40.00"),
    },
  },
);

printjson(priceResult);

const prices = m.journey_search
  .find(
    { _id: /^LAB05:[A-F]$/ },
    {
      _id: 1,
      cityId: 1,
      routeId: 1,
      price: 1,
    },
  )
  .sort({ _id: 1 })
  .toArray();

printjson(prices);

const changedIds = prices
  .filter((document) => document.price.toString() === "40.00")
  .map((document) => document._id);

const expectedChangedIds = [
  "LAB05:A",
  "LAB05:C",
  "LAB05:E",
  "LAB05:F",
];

if (changedIds.join("|") !== expectedChangedIds.join("|")) {
  throw new Error(
    `Expected changed IDs [${expectedChangedIds}], ` +
    `got [${changedIds}]`,
  );
}

const documentD = prices.find(
  (document) => document._id === "LAB05:D",
);

if (documentD.price.toString() !== "36.00") {
  throw new Error("Document D should retain its original price");
}

print(
  "Document D retained 36.00 because its cityId is " +
  `${documentD.cityId}, not CPH.`,
);
