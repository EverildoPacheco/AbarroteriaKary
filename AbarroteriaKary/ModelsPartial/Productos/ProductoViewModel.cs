using Microsoft.AspNetCore.Http;                 // IFormFile para subir imagen
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    
    public class ProductoViewModel
    {
        // Datos principales

        [Display(Name = "Producto (ID)")]
        [StringLength(10, ErrorMessage = "Máximo {1} caracteres.")]
        public string? ProductoId { get; set; }   


        [StringLength(50, ErrorMessage = "Máximo {1} caracteres.")]
        [Display(Name = "Código interno / de barras")]
        public string? CodigoProducto
        {
            get => string.IsNullOrWhiteSpace(_codigoProducto) ? null : _codigoProducto;
            set => _codigoProducto = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _codigoProducto = string.Empty;



        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
        [Display(Name = "Nombre del producto")]
        public string ProductoNombre
        {
            get => _productoNombre;
            set => _productoNombre = (value ?? string.Empty).Trim();
        }
        private string _productoNombre = string.Empty;


        [StringLength(250, ErrorMessage = "Máximo {1} caracteres.")]
        [Display(Name = "Descripción")]
        public string? ProductoDescripcion { get; set; }


        // Relaciones (FK) + nombres (para Details)

        [Required(ErrorMessage = "Seleccione una Subcategoría.")]
        [StringLength(10)]
        [Display(Name = "Subcategoría")]
        public string SubCategoriaId { get; set; } = string.Empty;

        [ValidateNever]
        [Display(Name = "Subcategoría")]
        public string? NombreSubCategoria { get; set; }

        [Required(ErrorMessage = "Seleccione un Tipo de producto.")]
        [StringLength(10)]
        [Display(Name = "Tipo de producto")]
        public string TipoProductoId { get; set; } = string.Empty;

        [ValidateNever]
        [Display(Name = "Tipo de producto")]
        public string? NombreTipoProducto { get; set; }

        [Required(ErrorMessage = "Seleccione una Unidad de medida.")]
        [StringLength(10)]
        [Display(Name = "Unidad de medida")]
        public string UnidadMedidaId { get; set; } = string.Empty;

        [ValidateNever]
        [Display(Name = "Unidad de medida")]
        public string? NombreUnidadMedida { get; set; }

        [StringLength(10)]
        [Display(Name = "Material del envase")]
        public string? MaterialEnvaseId { get; set; }

        [ValidateNever]
        [Display(Name = "Material del envase")]
        public string? NombreMaterialEnvase { get; set; }

        [StringLength(10)]
        [Display(Name = "Tipo de empaque")]
        public string? TipoEmpaqueId { get; set; }

        [ValidateNever]
        [Display(Name = "Tipo de empaque")]
        public string? NombreTipoEmpaque { get; set; }

        [StringLength(10)]
        [Display(Name = "Marca")]
        public string? MarcaId { get; set; }

        [ValidateNever]
        [Display(Name = "Marca")]
        public string? NombreMarca { get; set; }


        // 
        // Imagen del producto

        [StringLength(500, ErrorMessage = "Máximo {1} caracteres.")]
        [Display(Name = "Imagen (ruta/URL)")]
        public string? ProductoImagen { get; set; }


        [ValidateNever]
        [Display(Name = "Archivo de imagen")]
        public IFormFile? ImagenArchivo { get; set; }


        // Impuestos

        //[Range(0, 100, ErrorMessage = "El IVA debe estar entre 0 y 100.")]
        //[Display(Name = "% IVA")]
        //public decimal? IvaPorcentaje { get; set; }


        // Auditoría (solo lectura en vistas)

        [ValidateNever]
        [Display(Name = "Creado por")]
        public string? CreadoPor { get; set; }

        [Display(Name = "Fecha de creación")]
        public DateTime? FechaCreacion { get; set; }

        [ValidateNever]
        [Display(Name = "Modificado por")]
        public string? ModificadoPor { get; set; }

        [Display(Name = "Fecha de modificación")]
        public DateTime? FechaModificacion { get; set; }

        [ValidateNever]
        public bool Eliminado { get; set; }

        [ValidateNever]
        [Display(Name = "Eliminado por")]
        public string? EliminadoPor { get; set; }

        [Display(Name = "Fecha de eliminación")]
        public DateTime? FechaEliminacion { get; set; }


        // Estado (checkbox ↔ cadena)

        [ValidateNever]
        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        [Display(Name = "Activo")]
        public bool EstadoActivo
        {
            get => string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
            set => ESTADO = value ? "ACTIVO" : "INACTIVO";
        }


        // Combos (para Create/Edit)

        [ValidateNever] public IEnumerable<SelectListItem> Subcategorias { get; set; } = Enumerable.Empty<SelectListItem>();
        [ValidateNever] public IEnumerable<SelectListItem> TiposProducto { get; set; } = Enumerable.Empty<SelectListItem>();
        [ValidateNever] public IEnumerable<SelectListItem> UnidadesMedida { get; set; } = Enumerable.Empty<SelectListItem>();
        [ValidateNever] public IEnumerable<SelectListItem> MaterialesEnvase { get; set; } = Enumerable.Empty<SelectListItem>();
        [ValidateNever] public IEnumerable<SelectListItem> TiposEmpaque { get; set; } = Enumerable.Empty<SelectListItem>();
        [ValidateNever] public IEnumerable<SelectListItem> Marcas { get; set; } = Enumerable.Empty<SelectListItem>();

        // Conveniente para listados/encabezados
        [ValidateNever]
        public string Display => string.IsNullOrWhiteSpace(CodigoProducto)
            ? ProductoNombre
            : $"{ProductoNombre} ({CodigoProducto})";
    }
}
