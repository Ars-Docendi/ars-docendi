using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/usuarios")]
[Authorize]
public sealed class UsuariosController(ServicioUsuarios servicio) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.UsuariosVer)]
    [ProducesResponseType<IReadOnlyList<UsuarioAdministracionDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UsuarioAdministracionDto>> Listar(CancellationToken ct) =>
        servicio.ListarAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.UsuariosVer)]
    [ProducesResponseType<UsuarioAdministracionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UsuarioAdministracionDto> Obtener(Guid id, CancellationToken ct) =>
        servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    [ProducesResponseType<UsuarioAdministracionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UsuarioAdministracionDto>> Crear(
        GuardarUsuarioDto datos,
        CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(datos, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    [ProducesResponseType<UsuarioAdministracionDto>(StatusCodes.Status200OK)]
    public Task<UsuarioAdministracionDto> Editar(
        Guid id,
        GuardarUsuarioDto datos,
        CancellationToken ct) => servicio.EditarAsync(id, datos, ct);

    [HttpPost("{id:guid}/activar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public Task<UsuarioAdministracionDto> Activar(
        Guid id,
        CambiarEstadoUsuarioDto datos,
        CancellationToken ct) => servicio.CambiarEstadoAsync(id, true, datos.Version, ct);

    [HttpPost("{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public Task<UsuarioAdministracionDto> Desactivar(
        Guid id,
        CambiarEstadoUsuarioDto datos,
        CancellationToken ct) => servicio.CambiarEstadoAsync(id, false, datos.Version, ct);
}
