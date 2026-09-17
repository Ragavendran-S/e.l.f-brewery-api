using e.l.f._Beauty.Models;

public class DistanceSorter : IBrewerySorter
{
    public IEnumerable<Brewery> Sort(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
        if (options.UserLat == null || options.UserLng == null)
            return breweries;

        return options.Ascending
            ? breweries.OrderBy(b => CalculateDistance(options.UserLat.Value, options.UserLng.Value, b.Latitude, b.Longitude))
            : breweries.OrderByDescending(b => CalculateDistance(options.UserLat.Value, options.UserLng.Value, b.Latitude, b.Longitude));
    }

    private double CalculateDistance(double lat1, double lon1, double? lat2, double? lon2)
    {
        if (lat2 == null || lon2 == null) return double.MaxValue;

        var dLat = (lat2.Value - lat1) * Math.PI / 180.0;
        var dLon = (lon2.Value - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2.Value * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return 6371 * c; // Earth radius in km
    }
}
