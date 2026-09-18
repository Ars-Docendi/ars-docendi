using ArsDocendi.Shared.Auth;
using ArsDocendi.Storage.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Storage.Api;

[ApiController]
[Route("api/archivos")]
[Authorize]
public sealed class ArchivosController(IAlmacenamientoArchivos almacenamiento, ICurrentUser usuario) : ControllerBase
{
    [HttpPost("cargas")]
    public async Task<ActionResult<SesionCargaArchivoDto>> Iniciar(IniciarCargaArchivoDto datos, CancellationToken ct)
    {
        var propietario = RequerirUsuario();
        return Ok(await almacenamiento.IniciarCargaAsync(datos, propietario, ct));
    }

    [HttpPost("cargas/{id:guid}/confirmar")]
    public async Task<ActionResult<ArchivoDto>> Confirmar(Guid id, ConfirmarCargaArchivoDto datos, CancellationToken ct)
    {
        if (id != datos.ArchivoId) return BadRequest("El archivo de la ruta no coincide con el payload.");
        return Ok(await almacenamiento.ConfirmarCargaAsync(datos, RequerirUsuario(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArchivoDto>> Obtener(Guid id, CancellationToken ct)
    {
        if (!await almacenamiento.EsPropietarioAsync(id, RequerirUsuario(), ct)) return NotFound();
        var archivo = await almacenamiento.ObtenerAsync(id, ct);
        return archivo is null ? NotFound() : Ok(archivo);
    }

    [HttpGet("{id:guid}/descarga")]
    public async Task<IActionResult> Descargar(Guid id, CancellationToken ct)
    {
        var propietario = RequerirUsuario();
        if (!await almacenamiento.EsPropietarioAsync(id, propietario, ct)) return NotFound();
        var descarga = await almacenamiento.AbrirDescargaAsync(id, ct);
        return descarga is null ? NotFound() : File(descarga.Contenido, descarga.Mime, descarga.Nombre, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await almacenamiento.EliminarAsync(id, RequerirUsuario(), ct);
        return NoContent();
    }

    private Guid RequerirUsuario() => Guid.TryParse(usuario.UserId, out var id)
        ? id
        : throw new ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion(
            ArsDocendi.Shared.Aplicacion.TipoErrorAplicacion.NoAutenticado,
            "unauthenticated", "Se requiere autenticación.");
}
