using System;
using System.Net;

namespace AbarroteriaKary.Services.Mail
{
    public static class EmailTemplates
    {
        /// <summary>
        /// Email “Recuperación de contraseña” con estilos inline (compatibles con la mayoría de clientes).
        /// - nombre: nombre visible del destinatario (puede ser vacío).
        /// - url: enlace absoluto para restablecer.
        /// - expira: fecha/hora de expiración (se muestra en pie).
        /// - logoUrl: URL absoluta del logo (https://tu-dominio/img/LOGO.png). Si es null, se omite.
        /// </summary>
        public static string BuildRecoveryEmailHtml(string nombre, string url, DateTime expira, string? logoUrl = null)
        {
            // Escapar por seguridad
            string safeNombre = WebUtility.HtmlEncode(nombre ?? "");
            string safeUrl = WebUtility.HtmlEncode(url ?? "");
            string vence = expira.ToString("dd/MM/yyyy HH:mm");

            // Colores (puedes ajustar a tu paleta)
            const string brandDark = "#c5a3cf";  // verde fuerte (botón / acentos)
            const string brandLight = "#95b5c0";  // lima (degradado header)
            const string textMain = "#333333";
            const string textMuted = "#6c757d";
            const string bgPage = "#f5f7fb";
            const string cardBg = "#ffffff";
            const string borderColor = "#e9ecef";
            const string linkBlue = "#0d6efd"; // azul para enlaces



            // Si no llega logo, se omite el bloque <img>
            string logoBlock = string.IsNullOrWhiteSpace(logoUrl)
                ? ""
                : $@"<td align=""left"" valign=""middle"" style=""padding:18px 24px;"">
                        <img src=""{WebUtility.HtmlEncode(logoUrl)}"" alt=""Abarrotería Kary"" width=""40"" height=""40"" style=""display:block;border:0;border-radius:8px;"" />
                    </td>";

            return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <meta http-equiv=""x-ua-compatible"" content=""ie=edge"">
  <title>Abarrotería Kary - Recuperación de contraseña</title>
</head>
<body style=""margin:0;padding:0;background:{bgPage};"">
  <center style=""width:100%;background:{bgPage};"">
    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background:{bgPage};"">
      <tr>
  <td align=""center"" style=""padding:32px 12px;"">

    <!-- Card principal -->
    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""max-width:600px;background:{cardBg};border-radius:12px;overflow:hidden;border:1px solid {borderColor};"">
      <!-- Header color sólido -->
      <tr>
        <td style=""background:{brandLight};"">
          <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
            <tr>
              {logoBlock}
              <td align=""left"" valign=""middle"" style=""padding:18px 24px;color:#080808;font-family:Segoe UI,Arial,sans-serif;"">
                <div style=""font-size:20px;font-weight:700;letter-spacing:.3px;"">Abarrotería Kary</div>
                <div style=""font-size:12px;opacity:.9;"">Seguridad de cuenta</div>
              </td>
            </tr>
          </table>
        </td>
      </tr>

            <!-- Contenido -->
            <tr>
              <td style=""padding:28px 24px 8px 24px;font-family:Segoe UI,Arial,sans-serif;color:{textMain};"">
                <h1 style=""margin:0 0 6px 0;font-size:22px;line-height:1.3;color:{textMain};"">Restablecer tu contraseña</h1>
                <p style=""margin:0 0 12px 0;font-size:15px;line-height:1.6;color:{textMain};"">
                  {(string.IsNullOrWhiteSpace(safeNombre) ? "Hola," : $"Hola <strong>{safeNombre}</strong>,")}
                </p>
                <p style=""margin:0 0 16px 0;font-size:15px;line-height:1.6;color:{textMain};"">
                  Recibimos una solicitud para restablecer tu contraseña. Si no fuiste tú, puedes ignorar este mensaje.
                </p>
              </td>
            </tr>

            <!-- Botón -->
            <tr>
              <td align=""center"" style=""padding:4px 24px 18px 24px;"">
                <a href=""{safeUrl}"" target=""_blank""
                   style=""display:inline-block;padding:12px 18px;border-radius:8px;background:{brandDark};color:#080808;text-decoration:none;font-family:Segoe UI,Arial,sans-serif;font-size:15px;line-height:1;"">
                  <span style=""font-size:16px;margin-right:6px;"">🔐</span>
                  <span><strong>Restablecer contraseña</strong></span>
                </a>
              </td>
            </tr>

            <!-- Enlace de texto y aviso -->
            <tr>
              <td style=""padding:0 24px 24px 24px;font-family:Segoe UI,Arial,sans-serif;color:{textMuted};"">
                <p style=""margin:0 0 8px 0;font-size:13px;line-height:1.6;"">
                  Si el botón no funciona, copia y pega este enlace en tu navegador:
                </p>
                <p style=""margin:0 0 16px 0;font-size:13px;word-break:break-all;"">
                  <a href=""{safeUrl}"" style=""color:{linkBlue};text-decoration:none;"">{safeUrl}</a>
                </p>
                <p style=""margin:0;font-size:12px;color:{textMuted}"">
                  Este enlace vence el <strong>{vence}</strong>.
                </p>
              </td>
            </tr>


            <!-- Footer -->
            <tr>
              <td style=""background:#fafafa;border-top:1px solid {borderColor};padding:16px 24px;font-family:Segoe UI,Arial,sans-serif;color:{textMuted};"">
                <p style=""margin:0;font-size:12px;line-height:1.6;"">
                  Este es un mensaje automático. Por favor, no respondas a este correo.
                </p>
              </td>
            </tr>
          </table>
          <!-- /Card principal -->

          <!-- Marca secundaria -->
          <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:11px;color:{textMuted};margin-top:12px;"">
            © {DateTime.Now.Year} Abarrotería Kary
          </div>

        </td>
      </tr>
    </table>
  </center>
</body>
</html>";
        }
    }
}
