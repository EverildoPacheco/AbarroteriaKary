namespace AbarroteriaKary.Services.Pedidos
{
    public static class EstadosPedido
    {
        public const string BORRADOR = "BORRADOR";
        public const string PENDIENTE = "PENDIENTE";
        public const string ENVIADO = "ENVIADO";
        public const string RECIBIDO = "RECIBIDO";
        public const string CERRADO = "CERRADO";
        public const string ANULADO = "ANULADO";

        /// <summary>
        /// Normaliza el nombre del estado (usa CERRADO como sinónimo de FINALIZADO).
        /// </summary>
        public static string Normalizar(string? nombre)
        {
            var n = (nombre ?? "").Trim().ToUpper();
            if (n == "FINALIZADO") return CERRADO;
            return n;
        }
    }
}
