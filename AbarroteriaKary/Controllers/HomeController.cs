using System.Diagnostics;
using AbarroteriaKary.Models;
using Microsoft.AspNetCore.Mvc;

namespace AbarroteriaKary.Controllers
{
    public class HomeController : Controller
    {
        // GET: /Home/Inicio
        // Acción que pinta la página principal ("Inicio").
        [HttpGet]
        public IActionResult Inicio()
        {
            return View(); // Busca Views/Home/Inicio.cshtml
        }
    }
}














//using System.Diagnostics;
//using AbarroteriaKary.Models;
//using Microsoft.AspNetCore.Mvc;

//namespace AbarroteriaKary.Controllers
//{
//    public class HomeController : Controller
//    {
//        private readonly ILogger<HomeController> _logger;

//        public HomeController(ILogger<HomeController> logger)
//        {
//            _logger = logger;
//        }

//        public IActionResult Index()
//        {
//            return View();
//        }

//        public IActionResult Privacy()
//        {
//            return View();
//        }

//        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
//        public IActionResult Error()
//        {
//            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
//        }
//    }
//}
