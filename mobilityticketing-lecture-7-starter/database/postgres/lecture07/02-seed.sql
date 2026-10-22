insert into lab07.routes (id, short_name) values
('R1', 'City Loop'),
('R2', 'Campus Line'),
('R3', 'Airport Express'),
('R4', 'Harbour Shuttle');

insert into lab07.trips
(id, route_id, scheduled_departure_utc, status) values
('T101', 'R1', '2026-10-23 05:59:00+00', 'Scheduled'),
('T102', 'R1', '2026-10-23 06:00:00+00', 'Scheduled'),
('T103', 'R1', '2026-10-23 06:20:00+00', 'Scheduled'),
('T104', 'R1', '2026-10-23 06:30:00+00', 'Cancelled'),
('T105', 'R1', '2026-10-23 07:00:00+00', 'Scheduled'),
('T201', 'R2', '2026-10-23 06:10:00+00', 'Scheduled'),
('T202', 'R2', '2026-10-23 06:40:00+00', 'Scheduled'),
('T301', 'R3', '2026-10-23 05:50:00+00', 'Scheduled'),
('T302', 'R3', '2026-10-23 06:15:00+00', 'Cancelled');
