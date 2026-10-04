const m = db.getSiblingDB("mobility");

const start = ISODate("2026-10-02T06:00:00Z");
const end = ISODate("2026-10-02T07:00:00Z");

const brokenQuery = {
  cityId: "CPH",
  fromStopId: "STOP-NORREPORT",
  toStopId: "STOP-AIRPORT",
  "departures.status": "Scheduled",
  "departures.departureUtc": {
    $gte: start,
    $lt: end,
  },
};

const brokenResults = m.journey_search
  .find(brokenQuery, { _id: 1 })
  .sort({ _id: 1 })
  .toArray();

print("Broken query results:");
printjson(brokenResults);

if (!brokenResults.some((document) => document._id === "LAB05:B")) {
  throw new Error("The broken query should match LAB05:B");
}

print("Departures in LAB05:B:");
printjson(
  m.journey_search.findOne(
    { _id: "LAB05:B" },
    { _id: 1, departures: 1 },
  ),
);

const fixedQuery = {
  cityId: "CPH",
  fromStopId: "STOP-NORREPORT",
  toStopId: "STOP-AIRPORT",
  departures: {
    $elemMatch: {
      status: "Scheduled",
      departureUtc: {
        $gte: start,
        $lt: end,
      },
    },
  },
};

const fixedResults = m.journey_search
  .find(fixedQuery, { _id: 1 })
  .sort({ _id: 1 })
  .toArray();

print("Fixed query results:");
printjson(fixedResults);

if (fixedResults.some((document) => document._id === "LAB05:B")) {
  throw new Error("LAB05:B must not match the fixed query");
}

print(
  "PASS: $elemMatch requires status and time to match " +
  "the same array element.",
);
