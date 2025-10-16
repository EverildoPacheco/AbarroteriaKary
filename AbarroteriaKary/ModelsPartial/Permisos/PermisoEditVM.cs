using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbarroteriaKary.ModelsPartial
{
    /// VM para Create/Edit
    public class PermisoEditVM
    {
        public string PermisosId { get; set; } // Edit
        public string RolId { get; set; }
        public string ModuloId { get; set; }
        public string SubmoduloId { get; set; } // opcional (null => permiso a nivel de módulo)

        public bool PuedeVer { get; set; }
        public bool PuedeCrear { get; set; }
        public bool PuedeEditar { get; set; }
        public bool PuedeEliminar { get; set; }

        // Combos
        public IEnumerable<SelectListItem> Roles { get; set; }
        public IEnumerable<SelectListItem> Modulos { get; set; }
        public IEnumerable<SelectListItem> Submodulos { get; set; } // dinámico por módulo

        // Auditoría mínima (si desea mostrar)
        public string Estado { get; set; } = "ACTIVO";


    }
}

