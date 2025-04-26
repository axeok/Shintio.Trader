using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Shintio.Trader.Common;

[ApiController]
[Route("[controller]/[action]")]
public abstract class ControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase
{
    protected BadRequestObjectResult Error(string key, string errorMessage)
    {
        ModelState.AddModelError(key, errorMessage);

        return Error();
    }

    protected BadRequestObjectResult Error()
    {
        return BadRequest(ModelState);
    }

    protected ContentResult Json(string data)
    {
        return Content(data, "application/json");
    }

    protected ContentResult Json(object? data)
    {
        return Content(JsonSerializer.Serialize(data), "application/json");
    }
}