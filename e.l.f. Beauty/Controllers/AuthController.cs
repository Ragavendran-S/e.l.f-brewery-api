using e.l.f._Beauty.JwtOptions;
using e.l.f._Beauty.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    private readonly JwtOptionsAuth _jwtOptions;

    public AuthController(IConfiguration config,TokenValidator validator,IOptions<JwtOptionsAuth> jwtOptions)
    {
        _config = config;
        _validator = validator;
        _jwtOptions = jwtOptions.Value;

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
        var key = _config[_jwtOptions.Key];
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("JWT Key is not configured.");
        }
        //var keyBytes = Convert.FromBase64String(_config["Jwt:Key"]);
        var keyBytes = Encoding.UTF8.GetBytes(key);
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


