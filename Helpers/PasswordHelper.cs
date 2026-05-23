using System.Security.Cryptography;

namespace YnclinoAMS.Helpers
{
    public static class PasswordHelper
    {
        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            byte[] combined = new byte[48];
            Buffer.BlockCopy(salt, 0, combined, 0, 16);
            Buffer.BlockCopy(hash, 0, combined, 16, 32);
            return Convert.ToBase64String(combined);
        }

        public static bool Verify(string password, string storedHash)
        {
            byte[] combined;
            try { combined = Convert.FromBase64String(storedHash); }
            catch { return false; }

            if (combined.Length != 48) return false;

            byte[] salt = new byte[16];
            byte[] storedHashBytes = new byte[32];
            Buffer.BlockCopy(combined, 0, salt, 0, 16);
            Buffer.BlockCopy(combined, 16, storedHashBytes, 0, 32);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            return CryptographicOperations.FixedTimeEquals(hash, storedHashBytes);
        }
    }
}
