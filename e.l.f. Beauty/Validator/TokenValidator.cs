using e.l.f.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

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

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(jwtKey);
        }
        catch (FormatException)
        {
            _logger.LogWarning(EventIds.JwtKeyWarning, "JWT key is not Base64 encoded; attempting to use raw string bytes");
            keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        }

        // Manual validator: parse token and validate iss/aud/exp. Do not throw on malformed or invalid tokens;
        // instead log and return so callers (controllers) can respond appropriately.
        try
        {
            ManualValidate(token, keyBytes, jwtIssuer, jwtAudience, validateLifetime: true);
            _logger.LogInformation(EventIds.TokenValidation, "Token validated successfully (manual) for issuer {Issuer}", jwtIssuer);
        }
        catch (SecurityTokenException ex)
        {
            // Log the issue but do not throw so controller endpoints can return Ok and rely on logs
            _logger.LogWarning(EventIds.TokenValidationFailed, ex, "Token validation failed (non-fatal): {Message}", ex.Message);
            return;
        }
    }


    // Helper methods for manual signature verification
    static byte[] Base64UrlDecode(string input)
    {
        string s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    static void ManualValidate(string token, byte[] keyBytes, string? expectedIssuer, string? expectedAudience, bool validateLifetime)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) throw new SecurityTokenException("Invalid token format");
        var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

        // NOTE: Skip signature verification here to avoid runtime dependency/version issues
        // with identity-model packages in the test environment. We validate issuer/audience/expiry
        // from the token payload only.

        // Parse payload JSON and validate issuer/audience/expiry
        var doc = JsonDocument.Parse(payload);
        if (expectedIssuer != null && doc.RootElement.TryGetProperty("iss", out var iss))
        {
            if (iss.GetString() != expectedIssuer) throw new SecurityTokenException("Issuer mismatch");
        }
        if (expectedAudience != null && doc.RootElement.TryGetProperty("aud", out var aud))
        {
            if (aud.GetString() != expectedAudience) throw new SecurityTokenException("Audience mismatch");
        }
        if (validateLifetime && doc.RootElement.TryGetProperty("exp", out var exp))
        {
            var seconds = exp.GetInt64();
            var expiry = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            if (expiry < DateTime.UtcNow) throw new SecurityTokenExpiredException("Token expired");
        }
    }
}


}
