const m = db.getSiblingDB("mobility");

const query = {
    cityId: "CPH",
    fromStopId: "STOP-NORREPORT",
    toStopId: "STOP-AIRPORT",
    "departures.status": "Scheduled",
    "departures.departureUtc": {
      $gte: ISODate("2026-10-02T06:00:00Z"),
      $lt: ISODate("2026-10-02T07:00:00Z"),
    },
  };

printjson(m.journey_search.find(query).toArray());