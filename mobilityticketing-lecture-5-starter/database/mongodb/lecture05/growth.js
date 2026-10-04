const m = db.getSiblingDB("mobility");

const original = m.journey_search.findOne({
  _id: "LAB05:A",
});

if (original === null) {
  throw new Error("LAB05:A was not found");
}

function replaceAndMeasure(departureCount) {
  const growth = {
    ...original,
    _id: "LAB05:GROWTH",
    departures: Array.from(
      { length: departureCount },
      (_, index) => ({
        ...original.departures[0],
        tripId: `LAB05-GROWTH-${index}`,
      }),
    ),
  };

  m.journey_search.replaceOne(
    { _id: growth._id },
    growth,
    { upsert: true },
  );

  return m.journey_search
    .aggregate([
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
    ])
    .toArray()[0];
}

const hundred = replaceAndMeasure(100);
const thousand = replaceAndMeasure(1000);

print("100 departures:");
printjson(hundred);

print("1,000 departures:");
printjson(thousand);

print("Comparison:");
printjson({
  additionalBytes: thousand.bytes - hundred.bytes,
  sizeRatio: thousand.bytes / hundred.bytes,
});

if (
  hundred.departures !== 100 ||
  thousand.departures !== 1000 ||
  thousand.bytes <= hundred.bytes
) {
  throw new Error("Unexpected document-growth result");
}

print(
  "Conclusion: departures must have a bounded document boundary, " +
  "for example a service-date bucket, or be stored separately.",
);
