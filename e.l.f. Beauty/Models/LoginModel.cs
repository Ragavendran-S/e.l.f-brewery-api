using System;
namespace e.l.f._Beauty.Models
{
	public class LoginModel
	{
        public string? Username { get; set; }
        // Password is required by authentication tests and login flow
        public string? Password { get; set; }
        public string? FirstName { get; set; }
    }
}

