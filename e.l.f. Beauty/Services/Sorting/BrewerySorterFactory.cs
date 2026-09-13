public class BrewerySorterFactory : IBrewerySorterFactory
{
    public IBrewerySorter GetSorter(string sortBy) =>
        sortBy switch
        {
            "City" => new CitySorter(),
            "Distance" => new DistanceSorter(),
            _ => new NameSorter()
        };
    public IBrewerySorter Create(string sortType)
    {
        return sortType?.ToLowerInvariant() switch
        {
            "city" => new CitySorter(),
            "name" => new NameSorter(),
            "distance" => new DistanceSorter(),
            _ => new DefaultSorter()
        };
    }
}
