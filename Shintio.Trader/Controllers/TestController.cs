using Microsoft.AspNetCore.Mvc;
using ControllerBase = Shintio.Trader.Common.ControllerBase;

namespace Shintio.Trader.Controllers;

public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        return Json(DateTime.UtcNow);
    }
}