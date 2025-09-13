using System;

namespace AbarroteriaKary.ModelsPartial
{
    public class ProductoDetailsViewModel
    {
        // Principales
        public string ProductoId { get; set; } = string.Empty;
        public string ProductoNombre { get; set; } = string.Empty;
        public string? CodigoProducto { get; set; }
        public string? ProductoDescripcion { get; set; }

        // Catálogos (IDs + nombres)
        public string SubCategoriaId { get; set; } = string.Empty;
        public string SubCategoriaNombre { get; set; } = string.Empty;

        public string TipoProductoId { get; set; } = string.Empty;
        public string TipoProductoNombre { get; set; } = string.Empty;

        public string UnidadMedidaId { get; set; } = string.Empty;
        public string UnidadMedidaNombre { get; set; } = string.Empty;

        public string? MaterialEnvaseId { get; set; }
        public string? MaterialEnvaseNombre { get; set; }

        public string? TipoEmpaqueId { get; set; }
        public string? TipoEmpaqueNombre { get; set; }

        public string? MarcaId { get; set; }
        public string? MarcaNombre { get; set; }

        // Imagen
        public string? ImagenUrl { get; set; }

        // Estado
        public string ESTADO { get; set; } = "ACTIVO";
        public bool EstadoActivo { get; set; }

        // Auditoría
        public string? CreadoPor { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? ModificadoPor { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public string? EliminadoPor { get; set; }
        public DateTime? FechaEliminacion { get; set; }
        public bool Eliminado { get; set; }
    }
}
