#Explanations

## route_stops has two possible primary keys

Right now, the primary key for route_stops means that a stop may only occur once per route. We're modelling routes without a loop in this slice, that's why this primary key is chosen.

The other option is (route_id, stop_sequence) which would allow more stops on the same routes, for example if it's a there and return trip (A -> B -> A).

