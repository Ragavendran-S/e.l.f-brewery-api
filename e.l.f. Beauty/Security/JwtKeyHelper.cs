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

            // Try to decode a Base64 key first. If decoded key is long enough (>=128 bytes)
            // return it directly. Otherwise derive a deterministic 128-byte key by
            // repeated SHA-512 hashing and concatenation. A 128-byte key avoids
            // provider key-size validation issues across platforms for HMAC algorithms.
            try
            {
                var decoded = Convert.FromBase64String(key);
                if (decoded.Length >= 128)
                {
                    return decoded;
                }

                // If decoded key is shorter, fall through to deterministic expansion below
            }
            catch (FormatException)
            {
                // not Base64, fall through to hashing
            }

            // Deterministically expand to 128 bytes: compute SHA-512 of the key and
            // SHA-512 of the key with a constant suffix, then concatenate results.
            using var shaA = SHA512.Create();
            var partA = shaA.ComputeHash(Encoding.UTF8.GetBytes(key));
            using var shaB = SHA512.Create();
            var partB = shaB.ComputeHash(Encoding.UTF8.GetBytes(key + "::expand"));

            var combined = new byte[partA.Length + partB.Length];
            Buffer.BlockCopy(partA, 0, combined, 0, partA.Length);
            Buffer.BlockCopy(partB, 0, combined, partA.Length, partB.Length);
            return combined;
        }
    }
}
