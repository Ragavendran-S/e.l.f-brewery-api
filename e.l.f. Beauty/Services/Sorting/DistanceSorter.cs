using System.Linq;
using e.l.f._Beauty.Models;

public class DistanceSorter : IBrewerySorter
{
    public IEnumerable<Brewery> Sort(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
        if (breweries == null) throw new ArgumentNullException(nameof(breweries), "Breweries collection cannot be null.");
        if (options == null) throw new ArgumentNullException(nameof(options), "BreweryQueryOptions cannot be null.");

        if (options.UserLat == null || options.UserLng == null)
            throw new ArgumentException("User latitude and longitude are required for distance-based sorting.", nameof(options));

        // Compute nullable distances so we can treat missing coordinates consistently
        var withDistance = breweries.Select(b => new
        {
            Item = b,
            Distance = CalculateDistanceNullable(options.UserLat.Value, options.UserLng.Value, b.Latitude, b.Longitude)
        });

        if (options.Ascending)
        {
            // Closest first; missing coordinates should appear last
            return withDistance.OrderBy(x => x.Distance ?? double.MaxValue).Select(x => x.Item);
        }

        // Farthest first; missing coordinates should also appear last
        return withDistance.OrderByDescending(x => x.Distance ?? double.MinValue).Select(x => x.Item);
    }

    private double? CalculateDistanceNullable(double lat1, double lon1, double? lat2, double? lon2)
    {
        if (lat2 == null || lon2 == null) return null;

        var dLat = (lat2.Value - lat1) * Math.PI / 180.0;
        var dLon = (lon2.Value - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2.Value * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return 6371 * c; // Earth radius in km
    }
}
