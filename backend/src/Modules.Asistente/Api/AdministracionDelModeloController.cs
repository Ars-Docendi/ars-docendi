using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Asistente.Api;

/// <summary>
/// Model and provider-key administration (asistente-modelo-en-caliente, slice 3).
/// </summary>
[ApiController]
[Route("api/asistente/administracion")]
[Authorize(Policy = Permisos.AsistenteAdministrar)]
public sealed class AdministracionDelModeloController(ICurrentUser usuario) : ControllerBase
{
    /// <summary>What is in use, the configured local servers and the catalog with key status.</summary>
    [HttpGet("modelo")]
    public async Task<ActionResult<EstadoDelModeloDto>> Estado(CancellationToken ct) =>
        Ok(await Servicio().LeerAsync(ct));

    /// <summary>One minimal real call. Persists nothing. A failed test is a 200 with a code.</summary>
    [HttpPost("modelo/prueba")]
    public async Task<ActionResult<ResultadoDePruebaDto>> Probar(PedidoDeModeloDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var resultado = await Servicio().ProbarAsync(pedido, ct);
        return resultado.Desenlace == Desenlace.Exito ? Ok(resultado.Valor) : Rechazo(resultado.Desenlace, resultado.Codigo!);
    }

    /// <summary>Re-tests on the server, saves a new key if given, sets the selection.</summary>
    [HttpPut("modelo")]
    public async Task<ActionResult<ModeloAplicadoDto>> Aplicar(PedidoDeModeloDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var resultado = await Servicio().AplicarAsync(pedido, actor, ct);
        return resultado.Desenlace == Desenlace.Exito ? Ok(resultado.Valor) : Rechazo(resultado.Desenlace, resultado.Codigo!);
    }

    /// <summary>Back to server configuration.</summary>
    [HttpDelete("modelo")]
    public async Task<IActionResult> Restablecer(CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        await Servicio().RestablecerAsync(actor, ct);
        return NoContent();
    }

    /// <summary>Validates a new key against the provider and replaces the stored one only on success.</summary>
    [HttpPost("credenciales/{empresa}/reemplazo")]
    public async Task<ActionResult<CredencialReemplazadaDto>> Reemplazar(
        string empresa, PedidoDeReemplazoDeClaveDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var resultado = await Servicio().ReemplazarClaveAsync(empresa, pedido, actor, ct);
        return resultado.Desenlace == Desenlace.Exito ? Ok(resultado.Valor) : Rechazo(resultado.Desenlace, resultado.Codigo!);
    }

    /// <summary>
    /// Writes a price (USD per million tokens) as a new version of a catalog model.
    /// </summary>
    [HttpPost("precios")]
    public async Task<ActionResult<PrecioDelModeloDto>> CargarPrecio(PedidoDePrecioDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var resultado = await Servicio().CargarPrecioAsync(pedido, actor, ct);
        return resultado.Desenlace == Desenlace.Exito ? Ok(resultado.Valor) : Rechazo(resultado.Desenlace, resultado.Codigo!);
    }

    private AdministracionDelModelo Servicio() =>
        HttpContext.RequestServices.GetRequiredService<AdministracionDelModelo>();

    private ObjectResult Rechazo(Desenlace desenlace, string codigo)
    {
        var (estado, titulo, detalle) = desenlace switch
        {
            Desenlace.ClaveMaestraNoDisponible => (
                StatusCodes.Status503ServiceUnavailable,
                "Guardar claves no está disponible",
                "El servidor no tiene una clave maestra válida para cifrar claves de API. "
                + "Los modelos locales siguen disponibles."),
            Desenlace.PruebaFallida => (
                StatusCodes.Status422UnprocessableEntity,
                "La prueba de conexión falló",
                "El proveedor no aceptó la prueba. No se cambió nada."),
            Desenlace.PedidoInvalido when EsDePrecio(codigo) => (
                StatusCodes.Status400BadRequest,
                "Precio no válido",
                "El precio o la fecha de vigencia del pedido no son válidos. No se cambió nada."),
            _ => (
                StatusCodes.Status400BadRequest,
                "Pedido no válido",
                "La empresa, el modelo o la clave del pedido no son válidos para este servidor."),
        };

        var problema = new ProblemDetails { Title = titulo, Detail = detalle, Status = estado };
        problema.Extensions["codigo"] = codigo;

        return new ObjectResult(problema) { StatusCode = estado };
    }

    private static bool EsDePrecio(string codigo) =>
        codigo.StartsWith("precio-", StringComparison.Ordinal)
        || codigo == CodigosDelModelo.VigenciaAnteriorALaVersionActual;

    private bool ActorDeLaSesion(out Guid actor) => Guid.TryParse(usuario.UserId, out actor);
}
