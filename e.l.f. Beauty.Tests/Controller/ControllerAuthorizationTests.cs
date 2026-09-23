using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace e.l.f._Beauty.Tests.Controllers
{
    public class ControllerAuthorizationTests
    {
        [Fact]
        public void BreweriesControllers_AreDecoratedWithAuthorize()
        {
            var v1 = typeof(e.l.f._Beauty.Controllers.BreweriesController);
            var v2 = typeof(e.l.f._Beauty.Controllers.BreweriesControllerV2);

            var v1Has = v1.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Any();
            var v2Has = v2.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Any();

            Assert.True(v1Has, "BreweriesController (v1) must be decorated with [Authorize]");
            Assert.True(v2Has, "BreweriesControllerV2 (v2) must be decorated with [Authorize]");
        }
    }
}
