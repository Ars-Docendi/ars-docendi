using System.Text.Json;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>
/// Administración del uso del asistente: modo mantenimiento y (grupo 9)
/// presupuestos y panel de uso (asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Todo el controller exige <c>asistente.administrar</c> — un permiso propio,
/// sembrado directamente a <c>sys_admin</c> (design.md D13), distinto de
/// <c>asistente.leer_historial_ajeno</c>: administrar presupuestos y el kill
/// switch no habilita leer conversaciones de otros usuarios, ni viceversa.
/// </remarks>
[ApiController]
[Route("api/asistente/administracion")]
[Authorize(Policy = Permisos.AsistenteAdministrar)]
public sealed class AdministracionAsistenteController(
    IDisponibilidadDelModulo disponibilidadDelModulo,
    IAuditoriaDeAdministracion auditoria,
    IConsultasDeUso consultasDeUso,
    IPresupuestosAdministrables presupuestos,
    TimeProvider reloj,
    ICurrentUser usuario) : ControllerBase
{
    /// <summary>
    /// El panel de uso: turnos, resultado, llamadas, tokens, latencia,
    /// proveedor y costo estimado, por usuario, por rol y organizacional
    /// (tareas 9.1-9.4). Cada fila de <c>PorUsuario</c> también trae sus
    /// códigos de rol y el cupo diario efectivo con su origen — resuelto acá
    /// contra <see cref="IPresupuestosAdministrables.ObtenerEstadoAsync"/>
    /// para que el valor mostrado sea, siempre, el que la aplicación real del
    /// cupo usaría (tarea «rol y cupo efectivo por usuario» de
    /// sistema-seccion-unificada).
    /// </summary>
    /// <param name="periodo">
    /// Uno de <c>dia</c>, <c>semana</c>, <c>mes</c> (relativo a ahora). Default: <c>dia</c>.
    /// Ignorado si <paramref name="desde"/> viene puesto.
    /// </param>
    /// <param name="desde">Rango explícito: desde. Requiere <paramref name="hasta"/>.</param>
    /// <param name="hasta">Rango explícito: hasta (exclusivo).</param>
    [HttpGet("uso")]
    public async Task<ActionResult<UsoDto>> Uso(
        string? periodo, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        RangoDePeriodo rango;
        if (desde is { } d && hasta is { } h)
        {
            rango = new RangoDePeriodo(d, h);
        }
        else
        {
            var ahora = reloj.GetUtcNow();
            var inicio = DateOnly.FromDateTime(ahora.UtcDateTime).ToDateTime(TimeOnly.MinValue);
            var comienzo = periodo switch
            {
                "semana" => inicio.AddDays(-7),
                "mes" => inicio.AddMonths(-1),
                _ => inicio,
            };
            rango = new RangoDePeriodo(new DateTimeOffset(comienzo, TimeSpan.Zero), ahora);
        }

        var panel = await consultasDeUso.ObtenerAsync(rango, ct);
        var estado = await presupuestos.ObtenerEstadoAsync(ct);

        return Ok(UsoDto.De(panel, estado));
    }

    /// <summary>
    /// El estado persistido de los presupuestos: el tope organizacional
    /// vigente, el cupo default de cada rol, cada override de usuario
    /// vigente, y el gasto estimado del mes calendario en curso (tarea 12.8
    /// de sistema-seccion-unificada — cierra el gap de "sólo <c>PUT</c>,
    /// nunca <c>GET</c>" que documentaban
    /// <c>TopeOrganizacionalCard</c>/<c>EditorDeCupoEnFila</c>).
    /// </summary>
    /// <remarks>
    /// Deliberadamente NO trae la serie diaria de uso ni la telemetría del
    /// proveedor cloud/local — siguen sin implementar. El acceso por rol y
    /// las revocaciones por usuario sí (asistente-acceso-granular).
    /// </remarks>
    [HttpGet("presupuestos")]
    public async Task<ActionResult<PresupuestosDto>> Presupuestos(CancellationToken ct)
    {
        var estado = await presupuestos.ObtenerEstadoAsync(ct);

        // El mes CALENDARIO en curso, con el mismo límite UTC que el
        // acumulador que aplica el tope (design.md D2 de
        // asistente-administracion-de-uso: "el límite ya es UTC en todo el
        // resto del módulo") — nunca América/Argentina/Buenos_Aires, para no
        // introducir una segunda noción de "mes" para este único endpoint.
        var ahora = reloj.GetUtcNow();
        var inicioDelMes = new DateTimeOffset(ahora.Year, ahora.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var panelDelMes = await consultasDeUso.ObtenerAsync(new RangoDePeriodo(inicioDelMes, ahora), ct);

        return Ok(PresupuestosDto.De(estado, panelDelMes.Organizacion.CostoEstimado));
    }

    /// <summary>Edita el cupo diario default de un rol. Audita (tarea 9.5).</summary>
    [HttpPut("presupuestos/roles/{rol}")]
    public async Task<IActionResult> PresupuestoDeRol(string rol, PedidoDeCupoDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var (antes, despues) = await presupuestos.EditarCupoDeRolAsync(rol, pedido.Cupo, ct);

        await auditoria.RegistrarAsync(
            actor, "presupuesto.rol",
            $"{{\"rol\":\"{rol}\",\"cupo\":{antes}}}",
            $"{{\"rol\":\"{rol}\",\"cupo\":{despues}}}",
            ct);

        return NoContent();
    }

    /// <summary>Edita el override de cupo diario de un usuario. Audita (tarea 9.5).</summary>
    [HttpPut("presupuestos/usuarios/{actorId:guid}")]
    public async Task<IActionResult> PresupuestoDeUsuario(
        Guid actorId, PedidoDeCupoDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var (antes, despues) = await presupuestos.EditarOverrideDeUsuarioAsync(actorId, pedido.Cupo, ct);

        await auditoria.RegistrarAsync(
            actor, "presupuesto.usuario",
            $"{{\"actorId\":\"{actorId}\",\"cupo\":{(antes is null ? "null" : antes)}}}",
            $"{{\"actorId\":\"{actorId}\",\"cupo\":{despues}}}",
            ct);

        return NoContent();
    }

    /// <summary>
    /// Restablece el cupo de un usuario al de su rol: cierra la vigencia de
    /// su override (asistente-acceso-granular, design.md D5). Audita.
    /// </summary>
    [HttpDelete("presupuestos/usuarios/{actorId:guid}")]
    public async Task<IActionResult> RestablecerPresupuestoDeUsuario(Guid actorId, CancellationToken ct)
    {
        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var antes = await presupuestos.RestablecerOverrideDeUsuarioAsync(actorId, ct);

        await auditoria.RegistrarAsync(
            actor, "presupuesto.usuario.restablecer",
            $"{{\"actorId\":\"{actorId}\",\"cupo\":{(antes is null ? "null" : antes)}}}",
            $"{{\"actorId\":\"{actorId}\",\"cupo\":null}}",
            ct);

        return NoContent();
    }

    /// <summary>
    /// Prende o apaga el acceso operativo de un rol (asistente-acceso-granular).
    /// Audita.
    /// </summary>
    [HttpPut("presupuestos/roles/{rol}/acceso")]
    public async Task<IActionResult> AccesoDeRol(string rol, PedidoDeAccesoDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var (antes, despues) = await presupuestos.EditarAccesoDeRolAsync(rol, pedido.Habilitado, ct);

        await auditoria.RegistrarAsync(
            actor, "acceso.rol",
            JsonSerializer.Serialize(new { rol, habilitado = antes }),
            JsonSerializer.Serialize(new { rol, habilitado = despues }),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Revoca (<c>habilitado: false</c>) o restablece (<c>true</c>) el acceso
    /// de un usuario (asistente-acceso-granular, design.md D3): a un usuario
    /// se le puede quitar el acceso, no darlo por encima de su rol. Audita.
    /// </summary>
    [HttpPut("presupuestos/usuarios/{actorId:guid}/acceso")]
    public async Task<IActionResult> AccesoDeUsuario(
        Guid actorId, PedidoDeAccesoDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var (revocadoAntes, revocadoDespues) =
            await presupuestos.EditarAccesoDeUsuarioAsync(actorId, pedido.Habilitado, actor, ct);

        await auditoria.RegistrarAsync(
            actor, "acceso.usuario",
            JsonSerializer.Serialize(new { actorId, revocado = revocadoAntes }),
            JsonSerializer.Serialize(new { actorId, revocado = revocadoDespues }),
            ct);

        return NoContent();
    }

    /// <summary>Edita el tope organizacional de gasto mensual. Audita (tarea 9.6).</summary>
    [HttpPut("tope-organizacional")]
    public async Task<IActionResult> TopeOrganizacional(PedidoDeTopeDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        var (antes, despues) = await presupuestos.EditarTopeOrganizacionalAsync(pedido.TopeMensualUsd, ct);

        await auditoria.RegistrarAsync(
            actor, "tope_organizacional",
            $"{{\"topeMensualUsd\":{antes}}}",
            $"{{\"topeMensualUsd\":{despues}}}",
            ct);

        return NoContent();
    }

    /// <summary>
    /// Prende o apaga el modo mantenimiento. La razón es obligatoria para
    /// prenderlo (tarea 6.2).
    /// </summary>
    [HttpPatch("mantenimiento")]
    public async Task<ActionResult<MantenimientoDto>> Mantenimiento(
        PedidoDeMantenimientoDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (!ActorDeLaSesion(out var actor))
        {
            return Unauthorized();
        }

        if (pedido.Activo && string.IsNullOrWhiteSpace(pedido.Razon))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Falta la razón",
                Detail = "Activar el modo mantenimiento exige una razón, y no puede ser sólo espacios.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var antes = await disponibilidadDelModulo.ConsultarAsync(ct);

        if (pedido.Activo)
        {
            await disponibilidadDelModulo.ActivarAsync(actor, pedido.Razon!, ct);
        }
        else
        {
            await disponibilidadDelModulo.DesactivarAsync(actor, ct);
        }

        var despues = await disponibilidadDelModulo.ConsultarAsync(ct);

        // Write-before-respond (mismo criterio que asistente-historial-conversaciones
        // para su propia auditoría de soporte): la fila de auditoría se escribe
        // ANTES de devolver éxito al cliente, así que un 200 recibido siempre
        // implica que la fila ya existe.
        await auditoria.RegistrarAsync(
            actor,
            despues.Activo ? "mantenimiento.activar" : "mantenimiento.desactivar",
            JsonSerializer.Serialize(antes),
            JsonSerializer.Serialize(despues),
            ct);

        return Ok(MantenimientoDto.De(despues));
    }

    private bool ActorDeLaSesion(out Guid actor) => Guid.TryParse(usuario.UserId, out actor);
}
