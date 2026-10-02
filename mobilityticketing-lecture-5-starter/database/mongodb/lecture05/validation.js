const m = db.getSiblingDB("mobility");
const source = m.journey_search.findOne({ _id: "LAB05:A" });

const badDate = {
  ...source,
  _id: "LAB05:INVALID-DATE",
  departures: source.departures.map((d) => ({ ...d })),
};
badDate.departures[0].departureUtc = "2026-10-02T06:20:00Z";

const badSeats = {
  ...source,
  _id: "LAB05:INVALID-SEATS",
  departures: source.departures.map((d) => ({ ...d })),
};
badSeats.departures[0].availableSeats = NumberInt(-1);

for (const doc of [badDate, badSeats]) {
  try {
    m.journey_search.insertOne(doc);
    print(doc._id, "was inserted");
  } catch (e) {
    print(doc._id, e.code, e.codeName);
  }
}