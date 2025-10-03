using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AbarroteriaKary.ModelsPartial
{
    public class CajaViewModels
    {
        [Display(Name = "Id Caja")]
        public string cajaID { get; set; }

        [Display(Name = "Caja No.")]
        [Required(ErrorMessage = "El campo Caja es obligatorio.")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string NombreCaja
        {
            get => _nombreCaja;
            set => _nombreCaja = (value ?? string.Empty).Trim();
        }
        private string _nombreCaja = string.Empty;





        // Estado (string) <-> Checkbox

        [Display(Name = "Estado")]
        public string ESTADO
        {
            get => EstadoActivo ? "ACTIVO" : "INACTIVO";
            set => EstadoActivo = string.Equals(value, "ACTIVO", StringComparison.OrdinalIgnoreCase);
        }

        /// Checkbox en Create/Edit. Se mapea con ESTADO.
        /// </summary>
        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;


        // Auditoría (para la tarjeta)

        [Display(Name = "Fecha de creación")]
        //[Column(TypeName = "datetime")]

        public DateTime? FechaCreacion { get; set; }

        // Utilidades (opcional)

        /// <summary>
        /// Clase CSS auxiliar para mostrar el badge de estado en listados/detalles.
        /// </summary>
        public string EstadoBadgeCss => EstadoActivo ? "badge-estado--activo" : "badge-estado--inactivo";
    }
}
