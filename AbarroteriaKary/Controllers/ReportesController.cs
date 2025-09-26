// Controllers/ReportesController.cs
using Microsoft.AspNetCore.Mvc;

namespace AbarroteriaKary.Controllers
{
    public class ReportesController : Controller
    {
        // GET: /Reportes
        public IActionResult Index()
        {
            return View();
        }
    }
}
