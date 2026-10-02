const m = db.getSiblingDB('mobility');
const validator = {$jsonSchema: {
  bsonType: 'object',
  required: ['_id', 'cityId', 'routeId', 'fromStopId', 'toStopId',
             'serviceDate', 'schemaVersion', 'price', 'currency', 'departures'],
  properties: {
    _id: {bsonType: 'string'},
    cityId: {bsonType: 'string'},
    routeId: {bsonType: 'string'},
    fromStopId: {bsonType: 'string'},
    toStopId: {bsonType: 'string'},
    serviceDate: {bsonType: 'string'},
    schemaVersion: {bsonType: 'int', minimum: 1},
    price: {bsonType: 'decimal'},
    currency: {enum: ['DKK']},
    departures: {bsonType: 'array', items: {
      bsonType: 'object',
      required: ['tripId', 'departureUtc', 'arrivalUtc',
                 'availableSeats', 'status'],
      properties: {
        tripId: {bsonType: 'string'},
        departureUtc: {bsonType: 'date'},
        arrivalUtc: {bsonType: 'date'},
        availableSeats: {bsonType: 'int', minimum: 0},
        status: {enum: ['Scheduled', 'Cancelled']}
      }
    }}
  }
}};
const collections = m.getCollectionNames();
if (!collections.includes('journey_search')) {
  m.createCollection('journey_search', {
    validationLevel: 'strict',
    validationAction: 'error',
    validator
  });
} else {
  m.runCommand({
    collMod: 'journey_search',
    validationLevel: 'strict',
    validationAction: 'error',
    validator
  });
}
const dep = (tripId, at, status = 'Scheduled') => ({
  tripId,
  departureUtc: new Date(at),
  arrivalUtc: new Date(new Date(at).getTime() + 20 * 60000),
  availableSeats: NumberInt(10),
  status
});
const base = {
  cityId: 'CPH',
  routeId: 'LINE-M2',
  fromStopId: 'STOP-NORREPORT',
  toStopId: 'STOP-AIRPORT',
  serviceDate: '2026-10-02',
  schemaVersion: NumberInt(1),
  price: NumberDecimal('36.00'),
  currency: 'DKK'
};
const eligible = dep('LAB05-T-OK', '2026-10-02T06:20:00Z');
const documents = [
  {...base, _id: 'LAB05:A', departures: [
    dep('LAB05-T-EARLY', '2026-10-02T05:50:00Z'),
    dep('LAB05-T-CANCEL', '2026-10-02T06:10:00Z', 'Cancelled'),
    eligible,
    dep('LAB05-T-EDGE', '2026-10-02T07:00:00Z')
  ]},
  {...base, _id: 'LAB05:B', routeId: 'LAB-OTHER-ROUTE', departures: [
    dep('LAB05-T-B1', '2026-10-02T05:55:00Z'),
    dep('LAB05-T-B2', '2026-10-02T06:15:00Z', 'Cancelled')
  ]},
  {...base, _id: 'LAB05:C', toStopId: 'STOP-CENTRAL', departures: [eligible]},
  {...base, _id: 'LAB05:D', cityId: 'LAB-OTHER-CITY', departures: [
    dep('LAB05-T-D', '2026-10-02T06:25:00Z')
  ]},
  {...base, _id: 'LAB05:E', fromStopId: 'STOP-AIRPORT',
    toStopId: 'STOP-NORREPORT', departures: [
      dep('LAB05-T-E', '2026-10-02T06:30:00Z')
  ]},
  {...base, _id: 'LAB05:F', serviceDate: '2026-10-03', departures: [
    dep('LAB05-T-F', '2026-10-03T06:20:00Z')
  ]}
];
m.journey_search.deleteMany({_id: /^LAB05:/});
if (collections.includes('journey_search_by_trip')) {
  m.journey_search_by_trip.deleteMany({
    $or: [
      {_id: /^LAB05:/},
      {tripId: /^LAB05-/}
    ]
  });
}
m.journey_search.insertMany(documents);
printjson({
  fixtureDocuments: m.journey_search.countDocuments({_id: /^LAB05:/}),
  alternativeDocuments: m.journey_search_by_trip.countDocuments({
    tripId: /^LAB05-/
  })
});
