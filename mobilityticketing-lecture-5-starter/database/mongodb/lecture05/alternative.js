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

const journeys = m.journey_search.find({ _id: /^LAB05:/ }).toArray();

for (const journey of journeys) {
  for (const departure of journey.departures) {
    const { _id, departures, ...journeyFields } = journey;
    m.journey_search_by_trip.insertOne({
    ...journeyFields,
    ...departure,
    _id: `${_id}:${departure.tripId}`,
    });
  }
}
