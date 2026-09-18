
// Internal DTO
public class BreweryResponse
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
}

// External API model
public class ExternalBrewery
{
    public string? brewery_id { get; set; }
    public string? brewery_name { get; set; }
    public string? location_city { get; set; }
    public string? location_state { get; set; }
    public string? location_country { get; set; }
    // Open Brewery DB provides latitude/longitude as strings (nullable)
    public string? latitude { get; set; }
    public string? longitude { get; set; }
}
