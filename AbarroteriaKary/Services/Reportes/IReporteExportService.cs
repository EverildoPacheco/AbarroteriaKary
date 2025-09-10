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




    }
}
