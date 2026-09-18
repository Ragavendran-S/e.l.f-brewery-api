using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using e.l.f._Beauty; // adjust namespace if your API assembly uses a different root namespace
using e.l.f._Beauty.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace e.l.f._Beauty.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly IConfiguration _config;
        private readonly TokenValidator _validator;

        public AuthControllerTests()
        {
            // Ensure controller reads deterministic credentials during tests
            Environment.SetEnvironmentVariable("AUTH_USERNAME", "admin");
            Environment.SetEnvironmentVariable("AUTH_PASSWORD", "password");

            // Build in-memory configuration with a Base64 256-bit key
            var keyBytes = RandomNumberGenerator.GetBytes(32);
            var base64Key = Convert.ToBase64String(keyBytes);

            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Jwt:Key"] = base64Key,
                ["Jwt:Issuer"] = "brewery-api",
                ["Jwt:Audience"] = "brewery-api"
            };

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            // TokenValidator is the concrete validator used by the controller.
            // It must have a constructor that accepts IConfiguration (as in your controller).
            _validator = new TokenValidator(_config);
        }

        [Fact]
        public void Login_WithValidCredentials_ReturnsOkAndValidJwt()
        {
            // Arrange
            var controller = new AuthController(_config, _validator);
            var model = new LoginModel { Username = "admin", Password = "password" };

            // Act
            var actionResult = controller.Login(model);

            // Assert result type
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);

            // Extract token string from anonymous object { token = "..." }
            var tokenProperty = okResult.Value.GetType().GetProperty("token");
            Assert.NotNull(tokenProperty);
            var tokenString = tokenProperty!.GetValue(okResult.Value) as string;
            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            // Validate token using the same key/issuer/audience from configuration
            var handler = new JwtSecurityTokenHandler();
            var keyBytes = Convert.FromBase64String(_config["Jwt:Key"]!);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false, // ignore lifetime for unit test simplicity
                ValidIssuer = _config["Jwt:Issuer"],
                ValidAudience = _config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
            };

            var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);
            Assert.NotNull(principal);
            Assert.Equal("admin", principal.FindFirst(ClaimTypes.Name)?.Value);
            Assert.Equal("User", principal.FindFirst(ClaimTypes.Role)?.Value);
        }

        [Fact]
        public void Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var controller = new AuthController(_config, _validator);
            var model = new LoginModel { Username = "bad", Password = "creds" };

            // Act
            var actionResult = controller.Login(model);

            // Assert
            Assert.IsType<UnauthorizedResult>(actionResult);
        }

        [Fact]
        public void ValidateToken_WithValidToken_ReturnsOk()
        {
            // Arrange: obtain a token by calling Login
            var controller = new AuthController(_config, _validator);
            var loginResult = controller.Login(new LoginModel { Username = "admin", Password = "password" }) as OkObjectResult;
            Assert.NotNull(loginResult);

            var tokenProperty = loginResult!.Value!.GetType().GetProperty("token");
            Assert.NotNull(tokenProperty);
            var tokenString = tokenProperty!.GetValue(loginResult.Value!) as string;
            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            // Act: call ValidateToken endpoint
            var validateResult = controller.ValidateToken(tokenString!);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(validateResult);
            Assert.Equal("Check console logs for validation result", ok.Value);
        }

        [Fact]
        public void ValidateToken_WithInvalidToken_ReturnsOkAndLogsError()
        {
            // Arrange
            var controller = new AuthController(_config, _validator);
            var invalidToken = "this-is-not-a-jwt";

            // Act
            var result = controller.ValidateToken(invalidToken);

            // Assert: controller returns Ok with the expected message
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Check console logs for validation result", ok.Value);
        }

    }
}
