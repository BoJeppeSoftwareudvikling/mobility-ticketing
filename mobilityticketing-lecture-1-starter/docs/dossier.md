# Explanations

## route_stops primary key choice

`route_stops` is a linking table for the many-to-many relationship between `routes` and `stops`. We chose the composite primary key `(route_id, stop_id)`, so in this slice a stop can appear only once on a route.

The alternative is `(route_id, stop_sequence)`. That would allow the same stop to appear more than once on the same route, for example `A -> B -> A`, while still preserving stop order.
