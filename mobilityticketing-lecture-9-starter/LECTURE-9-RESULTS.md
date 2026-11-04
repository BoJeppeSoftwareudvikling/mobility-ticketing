# Lecture 9: transactions and the last seat

Testdato: 4. november 2026

Miljøet var starter-repoets PostgreSQL 17-container. Testene brugte schemaet i [`database/setup.sql`](database/setup.sql), kontrolscriptet i [`sql/check.sql`](sql/check.sql) og inspektionsqueryen i [`sql/inspect.sql`](sql/inspect.sql).

Den endelige funktion er gemt i [`sql/book.sql`](sql/book.sql). `reset` genskaber derfor schemaet, fixture-dataene og den implementerede funktion.

## Samlet resultat

- Den usikre read-then-write-sekvens gav to bookingrækker og `reserved_seats = 1`.
- En increment uden capacity-condition ramte SQLSTATE `23514`. PostgreSQL rullede den anden booking tilbage sammen med counterændringen.
- Conditional update, row lock, table lock, advisory lock og optimistic concurrency blev kørt med samtidige forbindelser.
- Deadlocktesten gav SQLSTATE `40P01`.
- Både `REPEATABLE READ` og `SERIALIZABLE` gav SQLSTATE `40001` i det konkrete concurrent-update-forløb.
- Starter-repoets single-session-checks bestod.
- Den endelige funktion bestod capacity-, rollback-, replay- og same-request-testene.

## 1. Reproduktion af double booking

Begge transactions læste `reserved_seats = 0` ved `READ COMMITTED`. A skrev den stale værdi `1` og committede. B skrev derefter den samme stale værdi og committede.

Sluttilstanden var:

```text
T-LAST: capacity=1, reserved_seats=1, booking_count=2, counts_agree=false
unsafe-A -> passenger-A -> T-LAST
unsafe-B -> passenger-B -> T-LAST
```

`CHECK (reserved_seats <= capacity)` opdagede ikke fejlen, fordi counteren stadig var `1`. Constrainten sammenligner ikke counteren med antal bookingrækker.

### Increment og rollback

A indsatte en booking og ændrede counteren fra `0` til `1`. B indsatte først sin booking og forsøgte derefter at ændre counteren fra `1` til `2`.

PostgreSQL afviste Bs update:

```text
SQLSTATE 23514
new row for relation "trips" violates check constraint "trips_check"
Failing row contains (T-LAST, Scheduled, 1, 2)
```

Bs transaction blev rullet tilbage. Sluttilstanden havde én booking og `reserved_seats = 1`.

## 2. Conditional update

Den afgørende write brugte denne betingelse:

```sql
UPDATE lab09.trips
SET reserved_seats = reserved_seats + 1
WHERE id = 'T-LAST'
  AND status = 'Scheduled'
  AND reserved_seats < capacity
RETURNING id, reserved_seats;
```

Da A committede, ventede B på trip-rækken. B genvurderede derefter `WHERE` mod den committede række og fik `UPDATE 0`. Sluttilstanden havde én booking og `reserved_seats = 1`.

Da A i stedet rullede tilbage, fortsatte B og fik `UPDATE 1`. B indsatte booking `conditional-rollback-B` og committede. Sluttilstanden havde igen én booking og `reserved_seats = 1`.

Testen viser, at conditional update ikke fjerner ventetid. Den samler availability-beslutningen og counterændringen i ét statement.

## 3. Explicit locks

### Row lock

A tog `FOR UPDATE` på `T-LAST`. B kunne straks tage `FOR UPDATE` på `T-OTHER`. Da B forsøgte at låse `T-LAST`, ventede B indtil A committede.

Row locken blokerede derfor kun den konkrete trip-række.

### Table lock

A kørte:

```sql
LOCK TABLE lab09.trips IN EXCLUSIVE MODE;
```

B ventede, selv om B forsøgte `SELECT ... FOR UPDATE` på `T-OTHER`. Table locken reducerede concurrency mellem uafhængige trips.

### Advisory lock

A og B brugte begge:

```sql
SELECT pg_advisory_xact_lock(9009, 1);
```

B ventede indtil As transaction sluttede. I den næste test beholdt A advisory locken, mens B ignorerede konventionen og opdaterede `T-LAST`. Bs update gennemførte uden at vente og blev bagefter rullet tilbage.

Advisory locks beskytter kun kodeveje, som bruger samme nøglekonvention.

### Deadlock

A låste først `T-LAST`. B låste først `T-OTHER`. Derefter forsøgte hver transaction at låse den anden række.

PostgreSQL afbrød en transaction med:

```text
SQLSTATE 40P01
deadlock detected
Process 656 waits for ShareLock on transaction 819; blocked by process 655.
Process 655 waits for ShareLock on transaction 818; blocked by process 656.
```

Da begge transactions tog locks i rækkefølgen `T-LAST`, `T-OTHER`, ventede B på A uden deadlock. Begge forløb kunne afsluttes.

## 4. Optimistic concurrency

A og B læste begge `reserved_seats = 0` på `T-TWO`. A opdaterede med et versionslignende check:

```sql
UPDATE lab09.trips
SET reserved_seats = reserved_seats + 1
WHERE id = 'T-TWO'
  AND status = 'Scheduled'
  AND reserved_seats < capacity
  AND reserved_seats = 0;
```

A fik `UPDATE 1` og committede. B ventede på rækken og fik derefter `UPDATE 0`, fordi Bs observation var stale. B rullede hele forsøget tilbage, startede en ny transaction, læste `reserved_seats = 1` og gennemførte den anden gyldige booking.

Sluttilstanden var to bookingrækker og `reserved_seats = 2`.

### Counterens ABA-problem

En transaction ændrede `T-OTHER` fra `0` til `1` og tilbage til `0`. Før- og efterværdien var derfor ens:

```text
before_value=0
after_value=0
```

Et check, der kun sammenligner `reserved_seats`, opdager ikke dette forløb. En versionkolonne, som incrementeres ved hver ændring, ville have ændret værdi to gange.

## 5. Isolation levels

### `REPEATABLE READ`

A og B læste begge `reserved_seats = 0`. A opdaterede og committede. Bs update blev afvist:

```text
SQLSTATE 40001
could not serialize access due to concurrent update
```

En ny transaction læste `reserved_seats = 1`, `capacity = 1` og `can_book = false`. Retry skal derfor begynde med den første availability-læsning.

### `SERIALIZABLE`

Det samme konkrete forløb gav også:

```text
SQLSTATE 40001
could not serialize access due to concurrent update
```

En ny transaction så, at trippen var full. De to tests fik samme synlige resultat, men `SERIALIZABLE` beskytter også mod dependency-forløb, som ikke kan forklares med en seriel rækkefølge.

En application-retry bør starte en ny transaction, gentage alle reads og writes, begrænse antal forsøg og returnere en kontrolleret midlertidig fejl, hvis alle forsøg fejler.

## 6. Implementeret bookingfunktion

Funktionen i [`sql/book.sql`](sql/book.sql) bruger en transaction-level advisory lock på `request_id` til at serialisere samme logiske request. Den tjekker et eksisterende request før availability. Kapaciteten beskyttes af en conditional update på trip-rækken.

```sql
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

    PERFORM pg_advisory_xact_lock(hashtextextended(p_request_id, 9009));

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
```

### Leverede checks

Kommandoen var:

```bash
docker compose exec -T postgres bash /lab/run.sh check
```

Resultatet var:

```text
Single-session checks passed. Existing experiment data was restored.
```

### To forskellige requests om sidste sæde

A fik booking-ID `1` og committede. B ventede på trip-rækken og blev derefter afvist:

```text
SQLSTATE P0001
Trip T-LAST is full
```

Sluttilstanden havde booking `final-A`, `reserved_seats = 1` og `booking_count = 1`.

### Replay og ugyldig genbrug

Replay af `final-A` returnerede booking-ID `1`. Efter at `T-LAST` blev cancelled, returnerede samme replay stadig booking-ID `1`.

Samme request-ID med en anden passenger eller trip blev afvist med SQLSTATE `22023`. Et nyt request mod `T-CANCELLED` blev afvist med `P0001`. Et request mod `T-MISSING` blev afvist med `P0002`. Ingen af fejlforløbene ændrede tabellerne.

### Samme nye request samtidigt

Da A committede, returnerede både A og B booking-ID `1`. Sluttilstanden havde én booking og `reserved_seats = 1`.

Da A rullede tilbage, havde A først fået det ikke-committede booking-ID `1`. B fortsatte efter rollback og committede booking-ID `2`. PostgreSQL-sequences rulles ikke tilbage, hvilket forklarer hullet. Sluttilstanden havde stadig kun én booking og `reserved_seats = 1`.

### Caller rollback

Et kald returnerede midlertidigt booking-ID `3` for `returned-but-rollback`. Calleren rullede derefter transactionen tilbage. En efterfølgende kontrol viste:

```text
surviving_booking_rows=0
other_reserved=0
```

Returværdien var derfor først endelig efter callerens commit.

## 7. AI-review

Model: Codex, GPT-5

Prompt:

```text
Review denne PostgreSQL 17-bookingfunktion med fokus på concurrency,
capacity-invarianten, rollback og idempotency via request_id.

Foreslå én præcis A/B-test med isolation level, statement-rækkefølge,
forventet ventetid, forventet commit eller rollback, forventet SQLSTATE
og forventet slutstate i trips og bookings.
```

Relevant review:

- Replay skal kontrolleres før tripstatus og kapacitet, ellers fejler replay efter full eller cancellation.
- Samme request skal serialiseres, så B venter på As commit eller rollback. Funktionen i [`sql/book.sql`](sql/book.sql) bruger en transaction-level advisory lock afledt af `request_id`.
- Kapacitet og idempotency er separate problemer. Conditional update beskytter trippen, mens request-locken og den unikke constraint beskytter det logiske request.
- Calleren ejer transactionen. Funktionen må ikke committe internt.

AI-reviewets foreslåede A/B-test brugte samme nye request i begge sessions. Testen blev kørt én gang med A-commit og én gang med A-rollback.

Prediction:

- Ved A-commit venter B og returnerer samme booking-ID. Der består én booking og ét reserveret sæde.
- Ved A-rollback venter B, opretter bookingen og committer. Identity-sequencen kan have et hul, selv om As booking blev rullet tilbage.

Faktisk output:

- A-commit gav booking-ID `1` til både A og B. Slutstate var én booking og `reserved_seats = 1`.
- A-rollback gav A det midlertidige ID `1` og B det committede ID `2`. Slutstate var én booking og `reserved_seats = 1`.

Forslaget blev accepteret, fordi output, ventetid og slutstate matchede predictionen.

Testene dækkede ikke høj contention med mange samtidige sessions, procescrash under en åben transaction eller performanceeffekten af hashkollisioner mellem advisory-lock-nøgler. En hashkollision vil serialisere to uafhængige requests, men den bryder ikke correctness.

## Kilder

- [`README.md`](README.md)
- [`database/setup.sql`](database/setup.sql)
- [`sql/check.sql`](sql/check.sql)
- [`sql/unsafe-read.sql`](sql/unsafe-read.sql)
- [`sql/unsafe-write.sql`](sql/unsafe-write.sql)
- [`sql/inspect.sql`](sql/inspect.sql)
- W45-noten `W45 - Implementation lab - The last seat.md` i Obsidian-vaultet.
