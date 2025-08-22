using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.Data;
using AbarroteriaKary.Models;

namespace AbarroteriaKary.Services
{
    /// <summary>
    /// Lógica central de seguridad: hashing PBKDF2, validación de credenciales y creación de Claims.
    /// Mapea a columnas USUARIO_CONTRASENA (hash) y USUARIO_SALT (salt).
    /// </summary>
    public class LoginService : ILoginService
    {
        private readonly KaryDbContext _db;
        private const int Iteraciones = 100_000;   // fijo (DB no guarda iteraciones)
        private const int SaltSize = 16;           // 128 bits
        private const int HashSize = 32;           // 256 bits (SHA-256)

        public LoginService(KaryDbContext db) => _db = db;

        public async Task<USUARIO?> ObtenerUsuarioPorNombreAsync(string usuario)
        {
            // Asegúrese que la entidad USUARIO tiene la propiedad USUARIO_NOMBRE tal como vino del Scaffold
            return await _db.USUARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.USUARIO_NOMBRE == usuario && x.ELIMINADO == false && x.ESTADO == "ACTIVO");
        }

        public (byte[] hash, byte[] salt) GenerarHash(string passwordPlano)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password: passwordPlano,
                salt: salt,
                iterations: Iteraciones,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize
            );
            return (hash, salt);
        }

        public bool VerificarPassword(string passwordPlano, byte[] hashGuardado, byte[]? saltGuardado)
        {
            if (hashGuardado == null || saltGuardado == null) return false;

            var hashIngresado = Rfc2898DeriveBytes.Pbkdf2(
                password: passwordPlano,
                salt: saltGuardado,
                iterations: Iteraciones,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashSize
            );

            // Comparación “constant-time”
            return CryptographicOperations.FixedTimeEquals(hashIngresado, hashGuardado);
        }

        public ClaimsPrincipal ConstruirClaims(USUARIO u)
        {
            // Puede mapear el ROL_ID como ClaimTypes.Role si lo desea
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, u.USUARIO_ID),
                new Claim(ClaimTypes.Name, u.USUARIO_NOMBRE),
                new Claim(ClaimTypes.Email, u.USUARIO_CORREO ?? string.Empty),
                new Claim(ClaimTypes.Role, u.ROL_ID) // si su ROL_ID = "ADMIN" / "VENTAS", etc.
            };

            var identity = new ClaimsIdentity(claims, authenticationType: "Cookies");
            return new ClaimsPrincipal(identity);
        }
    }
}
