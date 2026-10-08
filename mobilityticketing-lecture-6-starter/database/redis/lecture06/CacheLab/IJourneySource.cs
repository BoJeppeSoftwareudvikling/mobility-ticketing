public interface IJourneySource
{
    Task<List<Journey>> Fetch(SearchRequest request);
}
