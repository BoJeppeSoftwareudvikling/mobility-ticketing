const m = db.getSiblingDB("mobility");

printjson(
  m.journey_search.updateMany(
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
  ),
);
