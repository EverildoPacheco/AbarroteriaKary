using System;
using System.Security.Cryptography;

namespace AbarroteriaKary.Services.Security
{
    /// <summary>
    /// Hasher PBKDF2 (HMAC-SHA256) con SALT por usuario.
    /// Guarda/lee hash como byte[] para casarse con VARBINARY(MAX) en BD.
    /// </summary>
    public static class PasswordHasher
    {
        // Parámetros recomendados (puede ajustar iteraciones según hardware)
        private const int SaltSize = 32;           // 256-bit
        private const int KeySize = 32;            // 256-bit
        private const int Iterations = 100_000;    // seguridad vs rendimiento

        public static byte[] GenerateSalt()
        {
            var salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }

        public static byte[] Hash(string password, byte[] salt)
        {
            // Deriva una clave a partir del password + salt (PBKDF2)
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(KeySize);
        }

        public static bool Verify(string password, byte[] salt, byte[] storedHash)
        {
            if (salt == null || storedHash == null) return false;
            var computed = Hash(password, salt);
            return CryptographicOperations.FixedTimeEquals(computed, storedHash);
        }

        /// <summary>
        /// Compatibilidad: si hay usuarios viejos sin SALT (SALT null) y hash SHA256 plano,
        /// permite validar y luego migrar al nuevo esquema PBKDF2 en el siguiente cambio de contraseña.
        /// </summary>
        public static bool VerifyLegacySha256(string password, byte[] sha256BytesFromDb)
        {
            if (sha256BytesFromDb == null) return false;
            using var sha = SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var computed = sha.ComputeHash(bytes);
            return CryptographicOperations.FixedTimeEquals(computed, sha256BytesFromDb);
        }
    }
}
