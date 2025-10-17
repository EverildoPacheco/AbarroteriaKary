namespace AbarroteriaKary.ModelsPartial
{
    /// VM para Index (listado/consulta por Rol)
    public class PermisoListVM
    {
        //public string RolId { get; set; }                 // Filtro seleccionado
        //public string RolNombre { get; set; }
        //public List<RolItem> Roles { get; set; } = new(); // Dropdown roles
        //public List<PermisoItem> Permisos { get; set; } = new(); // Resultado agrupado
        public string PermisosId { get; set; }
        public string RolId { get; set; }
        public string RolNombre { get; set; }
        public string Estado { get; set; }    // para filtro/etiquetas si lo usas
        public List<PermisoItem> Permisos { get; set; } = new();

        public DateTime? FechaCreacion { get; set; }
    }

    public class RolItem { public string Id { get; set; } public string Nombre { get; set; } }

    public class PermisoItem
    {
        public string PermisosId { get; set; }
        public string ModuloId { get; set; }
        public string ModuloNombre { get; set; }
        public string SubmoduloId { get; set; }           // puede ser null (permiso sobre el módulo)
        public string SubmoduloNombre { get; set; }       // puede ser "-"
        public bool PuedeVer { get; set; }
        public bool PuedeCrear { get; set; }
        public bool PuedeEditar { get; set; }
        public bool PuedeEliminar { get; set; }
        public string Estado { get; set; }                // ACTIVO/INACTIVO
    }
}
