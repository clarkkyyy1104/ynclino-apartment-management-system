using System.Security.Cryptography;

namespace YnclinoApartmentManagementSystem.Helpers
{
    public static class PasswordHelper
    {
        // A temporary password issued by the administrator during a reset. It is
        // generated here, never chosen by the admin and never derived from anything
        // about the tenant, so it cannot be guessed from a name or a phone number.
        // The shape always satisfies the change-password rules (upper, lower, digit,
        // symbol, 8+) so the account is never left with a password the change page
        // would refuse.
        public static string GenerateTemporary()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I or O
            const string lower = "abcdefghijkmnopqrstuvwxyz";  // no l
            const string digit = "23456789";                   // no 0 or 1
            const string symbol = "!@#$%*?";
            string all = upper + lower + digit + symbol;

            var chars = new List<char>
            {
                Pick(upper), Pick(lower), Pick(digit), Pick(symbol)
            };
            while (chars.Count < 12) chars.Add(Pick(all));

            // shuffle so the guaranteed characters are not always in the same slots
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars.ToArray());
        }

        private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];

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
