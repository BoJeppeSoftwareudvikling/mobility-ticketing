using Microsoft.EntityFrameworkCore.Diagnostics;
using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Diagnostics;

public sealed class EntityMaterializationCounter : IMaterializationInterceptor
{
    public static readonly EntityMaterializationCounter Instance = new();

    private EntityMaterializationCounter()
    {
    }
    public object InitializedInstance(MaterializationInterceptionData data, object entity)
    {
        if (data.Context is LectureDbContext { Measurement.Active: true } db)
        {
            if (entity is Route)
                db.Measurement.MaterializedRoutes++;
            if (entity is Trip)
                db.Measurement.MaterializedTrips++;
        }
        return entity;
    }
}
