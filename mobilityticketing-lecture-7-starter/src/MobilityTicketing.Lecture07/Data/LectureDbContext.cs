using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Diagnostics;

namespace MobilityTicketing.Lecture07.Data;

public partial class LectureDbContext(DbContextOptions<LectureDbContext> options, RunMeasurement measurement) : DbContext(options)
{
    public RunMeasurement Measurement { get; } = measurement;
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PublishedPlan> PublishedPlans => Set<PublishedPlan>();
    public DbSet<PublishedPlanRoute> PublishedPlanRoutes => Set<PublishedPlanRoute>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("lab07");
        b.Entity<Route>(e =>
        {
            e.ToTable("routes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("short_name");
            e.HasMany(x => x.Trips).WithOne(x => x.Route).HasForeignKey(x => x.RouteId);
        });
        b.Entity<Trip>(e =>
        {
            e.ToTable("trips");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RouteId).HasColumnName("route_id");
            e.Property(x => x.DepartureUtc).HasColumnName("scheduled_departure_utc");
            e.Property(x => x.Status).HasColumnName("status");
        });
        b.Entity<Booking>(e =>
        {
            e.ToTable("bookings");
            e.HasKey(x => x.RequestId);
            e.Property(x => x.RequestId).HasColumnName("request_id");
            e.Property(x => x.TripId).HasColumnName("trip_id");
            e.Property(x => x.BookedAtUtc).HasColumnName("booked_at_utc");
        });
        ConfigureReadModels(b);
    }

    partial void ConfigureReadModels(ModelBuilder b);

    public static LectureDbContext Create(string connection, bool lazy = false, RunMeasurement? measurement = null)
    {
        var m = measurement ?? new RunMeasurement();
        var options = new DbContextOptionsBuilder<LectureDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(new ReadCommandCounter(m), EntityMaterializationCounter.Instance);
        if (lazy) options.UseLazyLoadingProxies();
        return new LectureDbContext(options.Options, m);
    }
}
