// IMPORTANTE: use el mismo namespace que la entidad USUARIO en /Models
using Microsoft.AspNetCore.Mvc;                  // [ModelMetadataType]
using System.ComponentModel.DataAnnotations.Schema;
using AbarroteriaKary.ModelsMetadata;

namespace AbarroteriaKary.Models  // <-- debe coincidir con Models/USUARIO.cs
{
    [ModelMetadataType(typeof(UsuarioMetadata))]
    public partial class USUARIO
    {
        // Si su columna se llama USUARIO_CAMBIOINICIAL (BIT NOT NULL):
        [NotMapped]
        public bool RequiereCambioInicial => this.USUARIO_CAMBIOINICIAL;
    }
}
