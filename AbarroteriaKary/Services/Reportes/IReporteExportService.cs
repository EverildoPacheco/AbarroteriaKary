using AbarroteriaKary.ModelsPartial;
//using AbarroteriaKary.ModelsPartial.AreasViewModel

using System.Collections.Generic;


namespace AbarroteriaKary.Services.Reportes
{
    public interface IReporteExportService
    {
        byte[] GenerarExcelAreas(IEnumerable<AreasViewModel> datos);
        byte[] GenerarWordAreas(IEnumerable<AreasViewModel> datos);
        // En el futuro: agregue aquí métodos para otros módulos (Productos, Ventas, etc.)

        byte[] GenerarExcelRoles(IEnumerable<RolViewModel> datos);
        byte[] GenerarWordRoles(IEnumerable<RolViewModel> datos);


        byte[] GenerarExcelUsuarios(IEnumerable<UsuarioListItemViewModel> datos);
        byte[] GenerarWordUsuarios(IEnumerable<UsuarioListItemViewModel> datos);


        byte[] GenerarExcelPuestos(IEnumerable<PuestoViewModel> datos);
        byte[] GenerarWordPuestos(IEnumerable<PuestoViewModel> datos);

        byte[] GenerarExcelEmpleado(IEnumerable<EmpleadoListItemViewModel> datos);
        byte[] GenerarWordEmpleado(IEnumerable<EmpleadoListItemViewModel> datos);


        byte[] GenerarExcelClientes(IEnumerable<ClienteViewModel> datos);
        byte[] GenerarWordClientes(IEnumerable<ClienteViewModel> datos);

    }
}
