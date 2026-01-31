using Microsoft.AspNetCore.Mvc;

namespace MiBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { mensaje = "¡Hola desde el backend .NET 10!" });
    }
}
