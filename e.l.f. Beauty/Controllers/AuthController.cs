using e.l.f._Beauty.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly TokenValidator _validator;

    public AuthController(IConfiguration config,TokenValidator validator)
    {
        _config = config;
        _validator = validator;

    }

    [HttpPost("validate")]
    public IActionResult ValidateToken([FromBody] string token)
    {
        _validator.ValidateToken(token);
        return Ok("Check console logs for validation result");
    }

    [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel model)
        {
            // ✅ Replace with real user validation
            if (model.Username != "admin" || model.Password != "password")
                return Unauthorized();

        var keyBytes = Convert.FromBase64String(_config["Jwt:Key"]);
        //IssuerSigningKey = new SymmetricSecurityKey(keyBytes);

        //var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, model.Username),
            new Claim(ClaimTypes.Role, "User")
           };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials
            );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }
    }


