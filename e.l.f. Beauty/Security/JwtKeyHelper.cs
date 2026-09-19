using System;
using System.Text;
using System.Security.Cryptography;

namespace e.l.f._Beauty.Security
{
    public static class JwtKeyHelper
    {
        // Normalize a configured Jwt key into the raw byte[] used for HMAC signing.
        // Accepts either a Base64-encoded key or a raw string. Ensures minimum key length
        // by hashing short inputs to a 256-bit value so signing/validation remain consistent.
        public static byte[] GetKeyBytes(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(key);
            }
            catch (FormatException)
            {
                keyBytes = Encoding.UTF8.GetBytes(key);
            }

            // Ensure minimum key size for HMAC-SHA256 (128 bits). If the provided key is shorter,
            // derive a 256-bit key deterministically by hashing the input so signing/validation remain consistent.
            if (keyBytes.Length < 16)
            {
                using var sha = SHA256.Create();
                keyBytes = sha.ComputeHash(keyBytes);
            }

            return keyBytes;
        }
    }
}
