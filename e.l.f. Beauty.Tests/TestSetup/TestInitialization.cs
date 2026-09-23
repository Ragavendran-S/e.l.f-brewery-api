using System;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace e.l.f._Beauty.Tests.TestSetup
{
    internal static class TestInitialization
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            try
            {
                using var sha512 = SHA512.Create();
                var keyBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes("e2e-integration-key"));
                var base64Key = Convert.ToBase64String(keyBytes);

                // Export both colon and double-underscore forms so different
                // configuration providers pick them up during Program startup.
                Environment.SetEnvironmentVariable("Jwt:Key", base64Key);
                Environment.SetEnvironmentVariable("Jwt__Key", base64Key);
                Environment.SetEnvironmentVariable("Jwt:Issuer", "brewery-api");
                Environment.SetEnvironmentVariable("Jwt__Issuer", "brewery-api");
                Environment.SetEnvironmentVariable("Jwt:Audience", "brewery-api");
                Environment.SetEnvironmentVariable("Jwt__Audience", "brewery-api");
            }
            catch
            {
                // Best-effort; tests will surface configuration problems if this fails.
            }
        }
    }
}
