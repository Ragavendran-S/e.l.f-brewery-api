public class BrewerySorterFactory : IBrewerySorterFactory
{
    // Consolidated single method to return the correct sorter. Both GetSorter and Create were
    // providing overlapping functionality with slightly different semantics which caused confusion.
    public IBrewerySorter GetSorter(string sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy)) return new NameSorter();

        switch (sortBy.Trim().ToLowerInvariant())
        {
            case "city":
            case "City":
                return new CitySorter();
            case "distance":
            case "Distance":
                return new DistanceSorter();
            case "name":
            case "Name":
                return new NameSorter();
            default:
                return new NameSorter();
        }
    }

    // Create was removed from the factory interface to avoid duplicated API surface.
    // If callers still rely on Create, update them to call GetSorter instead.
}
