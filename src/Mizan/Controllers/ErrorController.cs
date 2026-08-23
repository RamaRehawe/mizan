using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Mizan.ViewModels;

namespace Mizan.Controllers;

public class ErrorController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index() =>
        View("Error", new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
