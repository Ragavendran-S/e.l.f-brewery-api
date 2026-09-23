using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace e.l.f._Beauty.Tests.Controllers
{
    public class BreweriesRoutingTests
    {
        [Fact]
        public void No_Duplicate_DbContext_GetBreweries_Route()
        {
            var controllerType = typeof(e.l.f._Beauty.Controllers.BreweriesController);
            var methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            // Ensure no action is explicitly routed to "breweries" which would bypass the service pipeline
            var httpGetAttrs = methods.SelectMany(m => m.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>());
            Assert.DoesNotContain(httpGetAttrs, a => string.Equals(a.Template, "breweries", StringComparison.OrdinalIgnoreCase));

            // Ensure exactly one parameterless [HttpGet] exists (the primary listing endpoint)
            var noTemplateCount = methods.Count(m => m.GetCustomAttributes(typeof(HttpGetAttribute), false)
                .Cast<HttpGetAttribute>().Any(a => string.IsNullOrEmpty(a.Template)));
            Assert.Equal(1, noTemplateCount);
        }
    }
}
