using System;

namespace AbarroteriaKary.Services.Security
{
    public static class IdGenerator
    {
        // Genera un ID de 10 caracteres alfanuméricos (coincide con VARCHAR(10))
        public static string NewId10()
        {
            // Base36 compacta
            var guid = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                        .Replace("+", "").Replace("/", "").Replace("=", "");
            return guid.Length >= 10 ? guid.Substring(0, 10).ToUpper() : guid.ToUpper().PadRight(10, 'X');
        }
    }
}
