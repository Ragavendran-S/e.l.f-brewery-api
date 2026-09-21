using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace e.l.f._Beauty.Tests.Integration
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
#pragma warning disable CS0618 // ISystemClock is obsolete in some target frameworks; tests may still reference it
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, Microsoft.AspNetCore.Authentication.ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }
#pragma warning restore CS0618

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "User") };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
