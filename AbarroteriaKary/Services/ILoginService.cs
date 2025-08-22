using System.Security.Claims;
using AbarroteriaKary.Models;

namespace AbarroteriaKary.Services
{
    public interface ILoginService
    {
        Task<USUARIO?> ObtenerUsuarioPorNombreAsync(string usuario);
        bool VerificarPassword(string passwordPlano, byte[] hashGuardado, byte[]? saltGuardado);
        (byte[] hash, byte[] salt) GenerarHash(string passwordPlano);

        ClaimsPrincipal ConstruirClaims(USUARIO u); // para el SignIn
    }
}
