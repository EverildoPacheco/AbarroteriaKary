using System;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial.Commons
{
    /// <summary>
    /// VM genérico para mostrar auditoría (tarjeta/Toggle).
    /// </summary>
    /// 

    public class AuditoriaViewModel
    {
        public AbarroteriaKary.ModelsPartial.AuditoriaViewModel Auditoria { get; set; }
    = new AbarroteriaKary.ModelsPartial.AuditoriaViewModel();


        [Display(Name = "Creado por")]
        public string? CreadoPor { get; set; }

        [Display(Name = "Fecha de creación")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime? FechaCreacion { get; set; }

        [Display(Name = "Modificado por")]
        public string? ModificadoPor { get; set; }

        [Display(Name = "Fecha de modificación")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime? FechaModificacion { get; set; }

        [Display(Name = "Eliminado por")]
        public string? EliminadoPor { get; set; }

        [Display(Name = "Fecha de eliminación")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime? FechaEliminacion { get; set; }

        [Display(Name = "Eliminado")]
        public bool Eliminado { get; set; }
    }
}
