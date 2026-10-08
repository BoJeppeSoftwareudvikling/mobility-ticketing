const m = db.getSiblingDB("mobility");

function search(cityId, fromStopId, toStopId, start, end) {
  return m.journey_search
    .aggregate([
      {
        $match: { cityId, fromStopId, toStopId },
      },

      {
        $set: {
          departures: {
            $filter: {
              input: "$departures",
              as: "departure",
              cond: {
                $and: [
                  { $eq: ["$$departure.status", "Scheduled"] },
                  { $gte: ["$$departure.departureUtc", start] },
                  { $lt: ["$$departure.departureUtc", end] },
                ],
              },
            },
          },
        },
      },
      {
        $match: { departures: { $ne: [] } },
      },
    ])
    .toArray();
}

const start = ISODate("2026-10-02T18:00:00Z");
const end = ISODate("2026-10-02T19:00:00Z");

printjson(search("CPH", "STOP-NORREPORT", "STOP-AIRPORT", start, end));

// End of lecture 5 script.
