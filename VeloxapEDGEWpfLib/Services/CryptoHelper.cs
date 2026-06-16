using System;
using System.Security.Cryptography;
using System.Text;

namespace VeloxapEDGEWpfLib.Services
{
    public static class CryptoHelper
    {
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("VeloxapReportingServiceKey");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                OptionalEntropy,
                DataProtectionScope.LocalMachine);

            return Convert.ToBase64String(encryptedBytes);
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
                return string.Empty;

            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                OptionalEntropy,
                DataProtectionScope.LocalMachine);

            return Encoding.UTF8.GetString(plainBytes);
        }

        public static bool TryDecrypt(string encryptedText, out string plainText)
        {
            plainText = string.Empty;

            if (string.IsNullOrWhiteSpace(encryptedText))
                return true;

            try
            {
                plainText = Decrypt(encryptedText);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
