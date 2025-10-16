// ModelsPartial/Seguridad/PermisoBulkVM.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;


namespace AbarroteriaKary.ModelsPartial
{
    // Cabecera del formulario
    public class PermisoBulkVM
    {
        // Selección
        public string RolId { get; set; }
        public string ModuloId { get; set; }

        // Combos
        public List<SelectListItem> Roles { get; set; } = new();
        public List<SelectListItem> Modulos { get; set; } = new();

        // Tabla de submódulos a asignar
        [Required(ErrorMessage = "Se debe agregar al menos un permiso.")]
        public List<PermisoBulkItem> Items { get; set; } = new();

    }

    // Fila de la tabla (un submódulo o “todo el módulo”)
    public class PermisoBulkItem
    {
        public string SubmoduloId { get; set; }    // null/"" = permiso a todo el módulo
        public string SubmoduloNombre { get; set; }

        // Acciones
        public bool Ver { get; set; }
        public bool Crear { get; set; }
        public bool Editar { get; set; }
        public bool Eliminar { get; set; }

        // Para marcar visual/UX si ya existe en BD (se deshabilita la fila)
        public bool YaAsignado { get; set; }

        // ★ NUEVO: indica si el usuario cambió algo en esta fila
        public bool Touched { get; set; }

        // ★ Opcional (informativo): esta fila representa el permiso a nivel MÓDULO
        public bool EsModulo { get; set; }

    }
}
