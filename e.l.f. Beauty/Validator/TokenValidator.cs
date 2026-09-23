using e.l.f.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System;

namespace e.l.f._Beauty
{
    public class TokenValidator
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TokenValidator> _logger;

        public TokenValidator(IConfiguration config, ILogger<TokenValidator> logger)
        {
            _config = config;
            _logger = logger;
        }
        // Tests create TokenValidator by passing only IConfiguration; provide a convenience ctor
        public TokenValidator(IConfiguration config) : this(config, NullLogger<TokenValidator>.Instance)
        {
        }

    public void ValidateToken(string token)
    {
        var jwtKey = _config["Jwt:Key"];
        var jwtIssuer = _config["Jwt:Issuer"];
        var jwtAudience = _config["Jwt:Audience"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            _logger.LogWarning(EventIds.JwtKeyWarning, "JWT signing key is not configured");
            throw new InvalidOperationException("JWT signing key is not configured");
        }

        // Use JwtKeyHelper so validation uses the same normalization as issuance/runtime
        byte[] keyBytes;
        try
        {
            keyBytes = e.l.f._Beauty.Security.JwtKeyHelper.GetKeyBytes(jwtKey!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(EventIds.JwtKeyWarning, ex, "Failed to normalize JWT key: {Message}", ex.Message);
            throw;
        }

        // Use JwtSecurityTokenHandler to validate signature, issuer, audience and lifetime.
        var tokenHandler = new JwtSecurityTokenHandler();
        var signingKey = new SymmetricSecurityKey(keyBytes);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer),
            ValidIssuer = jwtIssuer,
            ValidateAudience = !string.IsNullOrEmpty(jwtAudience),
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            _logger.LogInformation(EventIds.TokenValidation, "Token validated successfully for issuer {Issuer}", jwtIssuer);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(EventIds.TokenValidationFailed, ex, "Token expired: {Message}", ex.Message);
            return;
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(EventIds.TokenValidationFailed, ex, "Signature validation failed: {Message}", ex.Message);
            return;
        }
        catch (SecurityTokenException ex)
        {
            // includes malformed token and other security token exceptions
            _logger.LogWarning(EventIds.TokenValidationFailed, ex, "Token validation failed: {Message}", ex.Message);
            // For diagnostics, attempt to log token header/payload (safe-truncated)
            try
            {
                var parts = token?.Split('.');
                if (parts != null && parts.Length >= 2)
                {
                    string header = parts[0];
                    string payload = parts[1];
                    string safeHeader = header.Length > 64 ? header.Substring(0, 64) + "..." : header;
                    string safePayload = payload.Length > 128 ? payload.Substring(0, 128) + "..." : payload;
                    _logger.LogDebug(EventIds.TokenValidationFailed, "Token header (truncated): {Header}", safeHeader);
                    _logger.LogDebug(EventIds.TokenValidationFailed, "Token payload (truncated): {Payload}", safePayload);
                }
            }
            catch { }
            return;
        }
    }
    }
}
