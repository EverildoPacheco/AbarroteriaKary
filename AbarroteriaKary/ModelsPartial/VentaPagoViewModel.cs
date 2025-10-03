using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AbarroteriaKary.ModelsPartial
{
    public class VentaPagoViewModel
    {
        [Required(ErrorMessage = "Seleccione un método de pago.")]
        [Display(Name = "Método de pago")]
        public string MetodoPagoId { get; set; } = string.Empty;

        [Display(Name = "Efectivo recibido")]
        [Range(0, 9999999)]
        public decimal? EfectivoRecibido { get; set; } // útil para calcular cambio en UI

        [ValidateNever]
        public decimal? CambioCalculado { get; set; }
    }
}
