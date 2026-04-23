namespace FuzzPhyte.Utility.Video
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    public static class FPVideoHashUtility
    {
        public static string ComputeSHA256(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        public static bool HashMatches(string expectedHash, string filePath)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return false;
            }

            string actualHash = ComputeSHA256(filePath);
            return string.Equals(expectedHash.Trim(), actualHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
