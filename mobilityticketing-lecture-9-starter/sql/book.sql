-- The function runs in the caller's transaction. It must not COMMIT internally.
CREATE OR REPLACE FUNCTION lab09.book(
    p_request_id text,
    p_passenger_id text,
    p_trip_id text
)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    v_existing_id bigint;
    v_existing_passenger text;
    v_existing_trip text;
    v_booking_id bigint;
    v_status text;
    v_capacity integer;
    v_reserved integer;
BEGIN
    IF p_request_id IS NULL OR length(p_request_id) = 0
       OR p_passenger_id IS NULL OR length(p_passenger_id) = 0
       OR p_trip_id IS NULL OR length(p_trip_id) = 0 THEN
        RAISE EXCEPTION USING
            ERRCODE = '22023',
            MESSAGE = 'Request, passenger and trip IDs must be non-empty';
    END IF;

    -- Serialise identical requests across concurrent transactions.
    PERFORM pg_advisory_xact_lock(hashtextextended(p_request_id, 9009));

    -- A successful replay returns its original ID, even if the trip is now
    -- full or cancelled. Reusing the request ID for other input is rejected.
    SELECT id, passenger_id, trip_id
    INTO v_existing_id, v_existing_passenger, v_existing_trip
    FROM lab09.bookings
    WHERE request_id = p_request_id;

    IF FOUND THEN
        IF v_existing_passenger = p_passenger_id
           AND v_existing_trip = p_trip_id THEN
            RETURN v_existing_id;
        END IF;

        RAISE EXCEPTION USING
            ERRCODE = '22023',
            MESSAGE = format(
                'Request ID %s belongs to passenger %s and trip %s',
                p_request_id,
                v_existing_passenger,
                v_existing_trip
            );
    END IF;

    -- PostgreSQL locks the matching trip row. A concurrent waiter rechecks
    -- the predicate against the committed row version before updating it.
    UPDATE lab09.trips
    SET reserved_seats = reserved_seats + 1
    WHERE id = p_trip_id
      AND status = 'Scheduled'
      AND reserved_seats < capacity
    RETURNING status, capacity, reserved_seats
    INTO v_status, v_capacity, v_reserved;

    IF NOT FOUND THEN
        SELECT status, capacity, reserved_seats
        INTO v_status, v_capacity, v_reserved
        FROM lab09.trips
        WHERE id = p_trip_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION USING
                ERRCODE = 'P0002',
                MESSAGE = format('Trip %s does not exist', p_trip_id);
        ELSIF v_status <> 'Scheduled' THEN
            RAISE EXCEPTION USING
                ERRCODE = 'P0001',
                MESSAGE = format('Trip %s is cancelled', p_trip_id);
        ELSE
            RAISE EXCEPTION USING
                ERRCODE = 'P0001',
                MESSAGE = format('Trip %s is full', p_trip_id);
        END IF;
    END IF;

    INSERT INTO lab09.bookings (request_id, passenger_id, trip_id)
    VALUES (p_request_id, p_passenger_id, p_trip_id)
    RETURNING id INTO v_booking_id;

    RETURN v_booking_id;
END;
$$;
