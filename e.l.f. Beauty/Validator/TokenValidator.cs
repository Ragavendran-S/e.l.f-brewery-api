using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

public class TokenValidator
{
    private readonly IConfiguration _config;

    public TokenValidator(IConfiguration config)
    {
        _config = config;
    }

    public void ValidateToken(string token)
    {
        var jwtKey = _config["Jwt:Key"];
        var jwtIssuer = _config["Jwt:Issuer"];
        var jwtAudience = _config["Jwt:Audience"];

        var handler = new JwtSecurityTokenHandler();
        var keyBytes = Convert.FromBase64String(jwtKey); // decode if stored as Base64
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
            Console.WriteLine("Token is valid");
            foreach (var claim in principal.Claims)
            {
                Console.WriteLine($"{claim.Type}: {claim.Value}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token validation failed: {ex.Message}");
        }
    }
}
