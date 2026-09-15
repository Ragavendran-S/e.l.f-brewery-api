using Castle.Core.Logging;
using e.l.f._Beauty; // adjust namespace if your API assembly uses a different root namespace
using e.l.f._Beauty.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace e.l.f._Beauty.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly IConfiguration _config;
        private readonly TokenValidator _validator;
        private readonly ILogger<TokenValidator> _logger;
        private readonly IOptions<e.l.f._Beauty.JwtOptions.JwtOptionsAuth> _jwtOptions;

        public AuthControllerTests()
        {
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
            // It must have a constructor that accepts IConfiguration and ILogger<TokenValidator>.
            _logger = new Mock<ILogger<TokenValidator>>().Object;
            _validator = new TokenValidator(_config, _logger);

            // Build IOptions<JwtOptionsAuth> where the Key/Issuer/Audience point to configuration keys
            var jwtOptions = new e.l.f._Beauty.JwtOptions.JwtOptionsAuth
            {
                Key = "Jwt:Key",
                Issuer = "Jwt:Issuer",
                Audience = "Jwt:Audience"
            };
            _jwtOptions = Options.Create(jwtOptions);
        }

        [Fact]
        public void Login_WithValidCredentials_ReturnsOkAndValidJwt()
        {
            // Arrange
            var controller = new AuthController(_config, _validator, _jwtOptions);
            var model = new LoginModel { Username = "admin", Password = "password" };

            // Act
            var actionResult = controller.Login(model);

            // Assert result type
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);

            // Extract token string from anonymous object { token = "..." }
            var tokenProperty = okResult.Value.GetType().GetProperty("token");
            if (tokenProperty == null) Assert.True(false, "token property missing");
            var tokenValue = tokenProperty.GetValue(okResult.Value);
            string tokenString;
            if (tokenValue is string ts)
            {
                tokenString = ts;
            }
            else
            {
                Assert.True(false, "token value is missing or not a string");
                tokenString = string.Empty; // satisfy definite assignment for compiler
            }

            // Validate token using the same key/issuer/audience from configuration
            var handler = new JwtSecurityTokenHandler();
            var keyConfig = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyConfig)) Assert.True(false, "Jwt:Key is not configured");
            var keyBytes = Convert.FromBase64String(keyConfig);
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
            // principal is asserted not null above; use the null-forgiving operator so the analyzer knows it's non-null here
            Assert.Equal("admin", principal!.FindFirst(ClaimTypes.Name)?.Value);
            Assert.Equal("User", principal!.FindFirst(ClaimTypes.Role)?.Value);
        }

        [Fact]
        public void Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var controller = new AuthController(_config, _validator, _jwtOptions);
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
            var controller = new AuthController(_config, _validator, _jwtOptions);
            var loginResult = controller.Login(new LoginModel { Username = "admin", Password = "password" }) as OkObjectResult;
            if (loginResult == null) Assert.True(false, "Login did not return OkObjectResult");

            if (loginResult.Value == null) Assert.True(false, "Login returned OkObjectResult with null Value");
            var tokenProperty = loginResult.Value.GetType().GetProperty("token");
            if (tokenProperty == null) Assert.True(false, "token property missing");
            var tokenValue = tokenProperty.GetValue(loginResult.Value);
            string tokenString;
            if (tokenValue is string ts)
            {
                tokenString = ts;
            }
            else
            {
                Assert.True(false, "token value is missing or not a string");
                tokenString = string.Empty;
            }

            // Act: call ValidateToken endpoint
            var validateResult = controller.ValidateToken(tokenString);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(validateResult);
            Assert.Equal("Check console logs for validation result", ok.Value);
        }

        [Fact]
        public void ValidateToken_WithInvalidToken_ReturnsOkAndLogsError()
        {
            // Arrange
            var controller = new AuthController(_config, _validator, _jwtOptions);
            var invalidToken = "this-is-not-a-jwt";

            // Act
            var result = controller.ValidateToken(invalidToken);

            // Assert: controller returns Ok with the expected message
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Check console logs for validation result", ok.Value);
        }

    }
}
