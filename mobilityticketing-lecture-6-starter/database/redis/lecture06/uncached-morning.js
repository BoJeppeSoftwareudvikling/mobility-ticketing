const mobility = db.getSiblingDB("mobility");

const results = mobility.journey_search
  .aggregate([
    {
      $match: {
        _id: /^LAB05:/,
        cityId: "CPH",
        fromStopId: "STOP-NORREPORT",
        toStopId: "STOP-AIRPORT",
      },
    },
    { $unwind: "$departures" },
    {
      $match: {
        "departures.status": "Scheduled",
        "departures.departureUtc": {
          $gte: ISODate("2026-10-02T06:00:00Z"),
          $lt: ISODate("2026-10-02T07:00:00Z"),
        },
      },
    },
    {
      $project: {
        _id: 0,
        tripId: "$departures.tripId",
      },
    },
  ])
  .toArray();

printjson(results);
