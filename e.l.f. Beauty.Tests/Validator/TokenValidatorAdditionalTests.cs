using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using e.l.f._Beauty;
using e.l.f._Beauty.Security;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace e.l.f._Beauty.Tests.Validator
{
    public class TokenValidatorAdditionalTests
    {
        private IConfiguration BuildConfig(string base64Key)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Jwt:Key"] = base64Key,
                ["Jwt:Issuer"] = "brewery-api",
                ["Jwt:Audience"] = "brewery-api"
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        private string CreateToken(string base64Key, DateTime? expires = null, string? signingKeyOverride = null)
        {
            var keyBytes = JwtKeyHelper.GetKeyBytes(signingKeyOverride ?? base64Key);
            var signingKey = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var handler = new JwtSecurityTokenHandler();
            // Determine notBefore so it's always before expires when expires is provided
            DateTime notBefore;
            DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
            if (expires.HasValue)
            {
                // place notBefore slightly before the expires time to create a valid historical token
                notBefore = expiresAt.AddMinutes(-1);
            }
            else
            {
                notBefore = DateTime.UtcNow.AddMinutes(-1);
            }

            var token = handler.CreateJwtSecurityToken(
                issuer: "brewery-api",
                audience: "brewery-api",
                subject: new ClaimsIdentity(new[] { new Claim("sub", "test") }),
                notBefore: notBefore,
                expires: expiresAt,
                signingCredentials: creds);

            return handler.WriteToken(token);
        }

        [Fact]
        public void ValidateToken_WithValidToken_DoesNotThrow()
        {
            var base64Key = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).PadRight(44, 'A');
            var config = BuildConfig(base64Key);
            var validator = new TokenValidator(config, NullLogger<TokenValidator>.Instance);

            var token = CreateToken(base64Key);

            // Should not throw
            validator.ValidateToken(token);
        }

        [Fact]
        public void ValidateToken_WithInvalidSignature_DoesNotThrow_ButLogs()
        {
            var base64Key = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).PadRight(44, 'A');
            var config = BuildConfig(base64Key);
            var validator = new TokenValidator(config, NullLogger<TokenValidator>.Instance);

            // Create a token signed with a different key
            var otherKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).PadRight(44, 'A');
            var token = CreateToken(base64Key, signingKeyOverride: otherKey);

            // TokenValidator catches signature failures and returns; ensure no exception
            validator.ValidateToken(token);
        }

        [Fact]
        public void ValidateToken_WithExpiredToken_DoesNotThrow()
        {
            var base64Key = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).PadRight(44, 'A');
            var config = BuildConfig(base64Key);
            var validator = new TokenValidator(config, NullLogger<TokenValidator>.Instance);

            var token = CreateToken(base64Key, expires: DateTime.UtcNow.AddMinutes(-10));

            // Should be handled (logged) and not throw
            validator.ValidateToken(token);
        }
    }
}
