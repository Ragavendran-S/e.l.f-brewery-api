using e.l.f.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

public class TokenValidator
{
    private readonly IConfiguration _config;
    private readonly ILogger<TokenValidator> _logger;

    public TokenValidator(IConfiguration config, ILogger<TokenValidator> logger)
    {
        _config = config;
        _logger = logger;
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
        var handler = new JwtSecurityTokenHandler();
        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(jwtKey); // decode if stored as Base64
        }
        catch (FormatException)
        {
            // If key is not base64, treat as raw bytes (or log and rethrow depending on your policy)
            _logger.LogWarning(EventIds.JwtKeyWarning, "JWT key is not Base64 encoded; attempting to use raw string bytes");
            keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        }
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };

        try
        {
            var principal = handler.ValidateToken(token, parameters, out var validatedToken);
            _logger.LogInformation(EventIds.TokenValidation, "Token validated successfully for issuer {Issuer}", jwtIssuer);
            foreach (var claim in principal.Claims)
            {
                _logger.LogDebug(EventIds.TokenValidation, "Validating token for issuer {Issuer}", _config["Jwt:Issuer"]);
            }
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(EventIds.TokenValidationFailed, ex, "Token expired for issuer {Issuer}", jwtIssuer);
            throw;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(EventIds.TokenValidationFailed, ex, "Token validation failed for issuer {Issuer}", jwtIssuer);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(EventIds.TokenValidationFailed, ex, "Unexpected error validating token for issuer {Issuer}", jwtIssuer);
            throw;
        }
    }
}
