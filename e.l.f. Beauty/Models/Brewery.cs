using System;
namespace e.l.f._Beauty.Models
{
    public class Brewery
    {
        //public int Id { get; set; }
        //public string Name { get; set; } = string.Empty;
        //public string City { get; set; } = string.Empty;
        //public string State { get; set; } = string.Empty;
        //public string Country { get; set; } = string.Empty;
        //public string Phone { get; set; } = string.Empty;
        //public double? Latitude { get; set; }
        //public double? Longitude { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brewery_Type { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public double? Latitude { get; set; } 
        public double? Longitude { get; set; } 
    }

    public class BreweryQueryOptions
    {
        public string? Search { get; set; }
        public string? City { get; set; }
        public string SortBy { get; set; } = "Name"; // Name, City, Distance
        public bool Ascending { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public double? UserLat { get; set; }
        public double? UserLng { get; set; }
    }
}

