using e.l.f._Beauty.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace e.l.f._Beauty.Repository
{
    public class BreweryDbContext : DbContext
    {
        public BreweryDbContext(DbContextOptions<BreweryDbContext> options)
     : base(options) { }


        public DbSet<Brewery> Breweries { get; set; } = null!;
    }

}