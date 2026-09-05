using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Portal.Application;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Domain;

namespace Modules.Portal.Api;

[ApiController]
[Route("api/portal")]
public sealed class PortalController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "portal", status = "ok" });

    [Authorize]
    [HttpGet("perfil")]
    public Task<PerfilDocenteDto> Perfil(ServicioPortal servicio, CancellationToken ct) => servicio.ObtenerPropioAsync(ct);

    [Authorize]
    [HttpPut("perfil/contacto")]
    public Task<ContactoDto> Contacto(GuardarContactoDto datos, ServicioPortal servicio, CancellationToken ct) => servicio.GuardarContactoAsync(datos, ct);

    [Authorize]
    [HttpPut("perfil/cv")]
    public Task<CvDto> Cv(GuardarCvDto datos, ServicioPortal servicio, CancellationToken ct) => servicio.GuardarCvAsync(datos, ct);

    [Authorize]
    [HttpDelete("perfil/cv")]
    public async Task<IActionResult> EliminarCv(ServicioPortal servicio, CancellationToken ct) { await servicio.EliminarCvAsync(ct); return NoContent(); }

    [Authorize]
    [HttpPut("perfil/habilidades")]
    public async Task<IActionResult> Habilidades(GuardarTagsDto datos, ServicioPortal servicio, CancellationToken ct) { await servicio.ReemplazarTagsAsync("habilidad", datos, ct); return NoContent(); }

    [Authorize]
    [HttpPut("perfil/intereses")]
    public async Task<IActionResult> Intereses(GuardarTagsDto datos, ServicioPortal servicio, CancellationToken ct) { await servicio.ReemplazarTagsAsync("interes", datos, ct); return NoContent(); }

    [Authorize, HttpPost("perfil/experiencia")]
    public async Task<ActionResult<ExperienciaDto>> Crear(GuardarExperienciaDto datos, ServicioPortal servicio, CancellationToken ct) => Created("/api/portal/perfil", await servicio.CrearAsync(datos, ct));
    [Authorize, HttpPut("perfil/experiencia/{id:guid}")]
    public Task<ExperienciaDto> Editar(Guid id, GuardarExperienciaDto datos, ServicioPortal servicio, CancellationToken ct) => servicio.EditarAsync(id, datos, ct);
    [Authorize, HttpDelete("perfil/experiencia/{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, ServicioPortal servicio, CancellationToken ct) { await servicio.EliminarAsync<Experiencia>(id, ct); return NoContent(); }

    [Authorize, HttpPost("perfil/educacion")]
    public async Task<ActionResult<EducacionDto>> Crear(GuardarEducacionDto datos, ServicioPortal servicio, CancellationToken ct) => Created("/api/portal/perfil", await servicio.CrearAsync(datos, ct));
    [Authorize, HttpPut("perfil/educacion/{id:guid}")]
    public Task<EducacionDto> Editar(Guid id, GuardarEducacionDto datos, ServicioPortal servicio, CancellationToken ct) => servicio.EditarAsync(id, datos, ct);
    [Authorize, HttpDelete("perfil/educacion/{id:guid}")]
    public async Task<IActionResult> EliminarEducacion(Guid id, ServicioPortal servicio, CancellationToken ct) { await servicio.EliminarAsync<Educacion>(id, ct); return NoContent(); }

    [Authorize, HttpPost("perfil/certificaciones")]
    public async Task<ActionResult<CertificacionDto>> Crear(GuardarCertificacionDto datos, ServicioPortal servicio, CancellationToken ct) => Created("/api/portal/perfil", await servicio.CrearAsync(datos, ct));
    [Authorize, HttpPut("perfil/certificaciones/{id:guid}")]
    public Task<CertificacionDto> Editar(Guid id, GuardarCertificacionDto datos, ServicioPortal servicio, CancellationToken ct) => servicio.EditarAsync(id, datos, ct);
    [Authorize, HttpDelete("perfil/certificaciones/{id:guid}")]
    public async Task<IActionResult> EliminarCertificacion(Guid id, ServicioPortal servicio, CancellationToken ct) { await servicio.EliminarAsync<Certificacion>(id, ct); return NoContent(); }

    [Authorize, HttpPost("perfil/proyectos")]
    public async Task<ActionResult<ProyectoDto>> Crear(GuardarProyectoDto datos, ServicioPortal servicio, CancellationToken ct) => Created("/api/portal/perfil", await servicio.CrearAsync(datos, ct));
    [Authorize, HttpPut("perfil/proyectos/{id:guid}")]
    public Task<ProyectoDto> Editar(Guid id, GuardarProyectoDto datos, ServicioPortal servicio, CancellationToken ct) => servicio.EditarAsync(id, datos, ct);
    [Authorize, HttpDelete("perfil/proyectos/{id:guid}")]
    public async Task<IActionResult> EliminarProyecto(Guid id, ServicioPortal servicio, CancellationToken ct) { await servicio.EliminarAsync<Proyecto>(id, ct); return NoContent(); }
}
