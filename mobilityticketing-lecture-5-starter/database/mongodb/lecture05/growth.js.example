const m = db.getSiblingDB("mobility");

const original = m.journey_search.findOne({
  _id: "LAB05:A",
});

const growth = {
  ...original,
  _id: "LAB05:GROWTH",
};

growth.departures = Array.from({ length: 100 }, (_, i) => ({
  ...original.departures[0],
  tripId: `LAB05-GROWTH-${i}`,
}));

m.journey_search.deleteOne({ _id: "LAB05:GROWTH" });

// TODO: insert growth into journey_search.

printjson(
  m.journey_search.aggregate([
    {
      $match: {
        _id: "LAB05:GROWTH",
      },
    },
    {
      $project: {
        _id: 0,
        bytes: {
          $bsonSize: "$$ROOT",
        },
        departures: {
          $size: "$departures",
        },
      },
    },
  ]).toArray(),
);
