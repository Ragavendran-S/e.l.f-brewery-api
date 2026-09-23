using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services.Sorting;
using Microsoft.Extensions.Options;

public class DefaultSorter : IBrewerySorter
{
    
    public IEnumerable<Brewery> Sort(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
           return breweries;
    }
}







