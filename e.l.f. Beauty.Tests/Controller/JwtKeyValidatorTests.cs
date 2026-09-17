using System;
using Xunit;
using e.l.f._Beauty;

namespace e.l.f._Beauty.Tests
{
    public class JwtKeyValidatorTests
    {
        [Fact]
        public void ValidateAndGetKeyBytes_ValidBase6432Bytes_Returns32Bytes()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var base64 = Convert.ToBase64String(bytes);
            var result = JwtKeyValidator.ValidateAndGetKeyBytes(base64);
            Assert.Equal(32, result.Length);
        }

        [Fact]
        public void ValidateAndGetKeyBytes_Missing_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.ValidateAndGetKeyBytes(null));
            Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.ValidateAndGetKeyBytes(string.Empty));
            Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.ValidateAndGetKeyBytes("   "));
        }

        [Fact]
        public void ValidateAndGetKeyBytes_InvalidBase64_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.ValidateAndGetKeyBytes("not-base64!!"));
        }

        [Fact]
        public void ValidateAndGetKeyBytes_WrongLength_ThrowsInvalidOperationException()
        {
            var bytes = new byte[16];
            var base64 = Convert.ToBase64String(bytes);
            var ex = Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.ValidateAndGetKeyBytes(base64));
            Assert.Contains("must be 32 bytes", ex.Message);
        }
    }
}
