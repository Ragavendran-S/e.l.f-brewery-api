using System;

namespace e.l.f._Beauty
{
    public static class JwtKeyValidator
    {
        public static byte[] ValidateAndGetKeyBytes(string jwtKey)
        {
            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new InvalidOperationException("Configuration 'jwtKey' is missing. Generate a 32-byte Base64 key (openssl rand -base64 32) and set the configuration value.");

            byte[] keyBytes;
            try
            {
                // Normalize possible Base64Url input and padding issues before decoding.
                // Normalize possible Base64Url input and padding issues before decoding.
                var normalized = jwtKey.Trim()
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Replace(" ", string.Empty)
                    .Replace('-', '+')
                    .Replace('_', '/');

                var mod = normalized.Length % 4;
                if (mod == 1)
                    throw new InvalidOperationException("Configuration 'jwtKey' is not a valid Base64/Base64Url string (length mod 4 == 1). Regenerate a 32-byte Base64 key.");
                if (mod == 2)
                    normalized += "==";
                else if (mod == 3)
                    normalized += "=";

                keyBytes = Convert.FromBase64String(normalized);
                if (keyBytes.Length != 32)
                    throw new InvalidOperationException($"The decoded 'jwtKey' must be 32 bytes (256 bits); found {keyBytes.Length} bytes.");
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Configuration 'jwtKey' is not a valid Base64 string. Generate a 32-byte Base64 key (openssl rand -base64 32) and set the configuration value.", ex);
            }

            if (keyBytes.Length != 32)
                throw new InvalidOperationException($"The decoded 'jwtKey' must be 32 bytes (256 bits); found {keyBytes.Length} bytes.");

            return keyBytes;
        }
    }
}
