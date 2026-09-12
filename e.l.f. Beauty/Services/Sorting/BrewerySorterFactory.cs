public class BrewerySorterFactory : IBrewerySorterFactory
{
    public IBrewerySorter GetSorter(string sortBy) =>
        sortBy switch
        {
            "City" => new CitySorter(),
            "Distance" => new DistanceSorter(),
            _ => new NameSorter()
        };
}
