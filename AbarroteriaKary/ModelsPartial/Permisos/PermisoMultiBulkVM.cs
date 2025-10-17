using System.Collections.Generic;

namespace AbarroteriaKary.ModelsPartial
{
    public class PermisoMultiBulkVM
    {
        public string RolId { get; set; } = "";
        public List<PermisoModuleBulkVM> Modules { get; set; } = new();
    }

    public class PermisoModuleBulkVM
    {
        public string ModuloId { get; set; } = "";
        public List<PermisoBulkItem> Items { get; set; } = new();
    }
}
