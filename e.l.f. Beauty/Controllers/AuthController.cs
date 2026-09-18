using Castle.Core.Logging;
using e.l.f._Beauty.JwtOptions;
using e.l.f._Beauty.Models;
using e.l.f.GlobalException;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace e.l.f._Beauty
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly TokenValidator _validator;
        private readonly JwtOptionsAuth _jwtOptions;

        // Convenience ctor used by unit tests which build IConfiguration in-memory
        public AuthController(IConfiguration config, TokenValidator validator)
        {
            _config = config;
            _validator = validator;
            _jwtOptions = new JwtOptionsAuth
            {
                Key = _config["Jwt:Key"],
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"]
            };

            if (string.IsNullOrEmpty(_jwtOptions.Key))
            {
                throw new InvalidOperationException("Jwt option 'Key' must be configured.");
            }
        }

        [ActivatorUtilitiesConstructor]
        public AuthController(IConfiguration config,TokenValidator validator,IOptions<JwtOptionsAuth> jwtOptions)
        {
            _config = config;
            _validator = validator;
            _jwtOptions = jwtOptions.Value;

            if (string.IsNullOrEmpty(_jwtOptions.Key))
            {
                throw new InvalidOperationException("Jwt option 'Key' must be configured.");
            }
        }

    [HttpPost("validate")]
    public IActionResult ValidateToken([FromBody] string token)
    {
        try
        {
            _validator.ValidateToken(token);
            return Ok("Check console logs for validation result");
        }
        catch (SecurityTokenMalformedException)
        {
            // log error here if you have ILogger
            //_logger.LogWarning(ex, "Error fetching breweries from external API.");
            return BadRequest("Invalid token format");
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            return Unauthorized("Signature validation failed");
        }
    }

    [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel model)
        {
            // Read expected credentials from explicit environment variables (try process, then user, then machine)
            static string? ReadEnv(string name)
            {
                // 1) process-level (inherited at process start or set at runtime)
                var v = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(v)) return v;

                // 2) user-level
                try
                {
                    v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
                catch { /* ignore access issues */ }

                // 3) machine-level
                try
                {
                    v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
                catch { /* ignore access issues */ }

                return null;
            }

            // Prefer IConfiguration values (including user-secrets), then environment variables, then test defaults.
            string? expectedUsername = _config["Auth:Username"]
                ?? _config["AUTH_USERNAME"]
                ?? _config["Username"]
                ?? ReadEnv("AUTH_USERNAME")
                ?? ReadEnv("Username");

            string? expectedPassword = _config["Auth:Password"]
                ?? _config["AUTH_PASSWORD"]
                ?? _config["Password"]
                ?? ReadEnv("AUTH_PASSWORD")
                ?? ReadEnv("Password");

            // If no credentials are configured, keep test defaults.
            //if (string.IsNullOrEmpty(expectedUsername)) expectedUsername = "admin";
            //if (string.IsNullOrEmpty(expectedPassword)) expectedPassword = "password";

             // Simple in-memory credential check for tests using environment-provided values
            if (model?.Username != expectedUsername || model?.Password != expectedPassword)
            {
                return Unauthorized();
            }
        var keyByte = _jwtOptions.Key ?? throw new InvalidOperationException("JWT Key is not configured.");
         if (string.IsNullOrEmpty(keyByte))
        {
            throw new InvalidOperationException("JWT Key is not configured.");
        }
        byte[] keyBytes;
        try
        {
            // First try Base64 decode (tests provide a Base64 key)
            keyBytes = Convert.FromBase64String(keyByte!);
        }
        catch (FormatException)
        {
            // Fallback to raw UTF8 bytes if not Base64
            keyBytes = Encoding.UTF8.GetBytes(keyByte!);
        }
        // Ensure minimum key size for HMAC-SHA256 (128 bits). If the provided key is shorter,
        // derive a 256-bit key deterministically by hashing the input so signing/validation remain consistent.
        if (keyBytes.Length < 16)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            keyBytes = sha.ComputeHash(keyBytes);
        }
        var symmetricKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256);

            if (model == null) throw new ArgumentNullException(nameof(model));
            var username = model.Username ?? throw new ArgumentNullException(nameof(model.Username));
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "User")
           };
        var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Issuer not configured");
        var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Audience not configured");
        var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials
            );

            var handler = new JwtSecurityTokenHandler();
            var tokenString = handler.WriteToken(token);

            // For debugging: expose signature and computed HMAC so we can compare when running tests.
            // Keep the original 'token' property so existing tests still work.
            string signature = string.Empty;
            string computedSignature = string.Empty;
            try
            {
                var parts = tokenString.Split('.');
                if (parts.Length == 3)
                {
                    signature = parts[2];
                    using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
                    var computed = hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
                    computedSignature = Base64UrlEncode(computed);
                }
            }
            catch
            {
                // ignore debug extraction failures
            }

            return Ok(new { token = tokenString, signature, computedSignature });
        }

    private static string Base64UrlEncode(byte[] input)
    {
        var base64 = Convert.ToBase64String(input);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
}


