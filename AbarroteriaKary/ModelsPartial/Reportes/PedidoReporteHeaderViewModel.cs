namespace AbarroteriaKary.ModelsPartial
{
    public class PedidoReporteHeaderViewModel
    {
        public string PedidoId { get; set; } = "";
        public string? Empresa { get; set; }          // PROVEEDOR.EMPRESA
        public string? ProveedorNit { get; set; }     // PERSONA.PERSONA_NIT
        public string? ProveedorDireccion { get; set; }
        public string? ProveedorTelefono { get; set; }
        public string? ProveedorCorreo { get; set; }

        public string FechaGenerada { get; set; } = "";        // dd/MM/yyyy HH:mm
        public string? FechaPedido { get; set; }               // dd/MM/yyyy
        public string? FechaEntregaEstimada { get; set; }      // dd/MM/yyyy
        public string? EstadoNombre { get; set; }              // ESTADO_PEDIDO_NOMBRE
        public string Usuario { get; set; } = "Admin";
    }
}
