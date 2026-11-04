using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using Modules.Notificaciones.Api;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Repositories;

namespace Modules.Notificaciones.Services;

public interface IServicioNotificaciones
{
    Task<CantidadNoLeidasDto> ContarNoLeidasAsync(CancellationToken ct);
    Task<PaginaNotificacionesDto> ListarAsync(string? pagina, string? tamanio, string? estado, CancellationToken ct);
    Task<NotificacionDto> MarcarLeidaAsync(Guid id, CancellationToken ct);
    Task<CantidadActualizadaDto> MarcarLeidasAsync(JsonElement cuerpo, CancellationToken ct);
}

/// <summary>
/// Read side: everything is scoped to the session user and the active profile (the first role of
/// the session, as <c>rol_solicitante</c> is captured).
/// </summary>
internal sealed partial class ServicioNotificaciones(
    RepositorioNotificaciones repositorio, ICurrentUser usuarioActual) : IServicioNotificaciones
{
    private const int TamanioPorDefecto = 20;
    private const int TamanioMaximo = 100;
    private const int MaximoIds = 100;

    public async Task<CantidadNoLeidasDto> ContarNoLeidasAsync(CancellationToken ct)
    {
        var (usuarioId, perfil) = Sesion();
        return new CantidadNoLeidasDto(await repositorio.ContarNoLeidasAsync(usuarioId, perfil, ct));
    }

    public async Task<PaginaNotificacionesDto> ListarAsync(
        string? pagina, string? tamanio, string? estado, CancellationToken ct)
    {
        var (usuarioId, perfil) = Sesion();
        bool? leidas = estado switch
        {
            null => null,
            "NO_LEIDAS" => false,
            "LEIDAS" => true,
            _ => throw Validacion("estado", "estado debe ser NO_LEIDAS o LEIDAS."),
        };
        var numeroPagina = EnteroPositivo(pagina, "pagina", 1, null);
        var tamanioPagina = EnteroPositivo(tamanio, "tamanio", TamanioPorDefecto, TamanioMaximo);

        var (datos, total) = await repositorio.ListarAsync(usuarioId, perfil, leidas, numeroPagina, tamanioPagina, ct);
        return new PaginaNotificacionesDto(
            datos.Select(Mapear).ToList(), total, numeroPagina, tamanioPagina,
            Math.Max(1, (int)Math.Ceiling(total / (double)tamanioPagina)));
    }

    public async Task<NotificacionDto> MarcarLeidaAsync(Guid id, CancellationToken ct)
    {
        var (usuarioId, perfil) = Sesion();
        var notificacion = await repositorio.MarcarLeidaAsync(usuarioId, perfil, id, DateTimeOffset.UtcNow, ct)
            // 404 and not 403: a foreign notice must not confirm that it exists.
            ?? throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoEncontrado, "notificacion-not-found", "La notificación no existe");
        return Mapear(notificacion);
    }

    public async Task<CantidadActualizadaDto> MarcarLeidasAsync(JsonElement cuerpo, CancellationToken ct)
    {
        var (usuarioId, perfil) = Sesion();
        var (ids, hasta) = LeerSeleccion(cuerpo);
        var cantidad = await repositorio.MarcarLeidasAsync(usuarioId, perfil, ids, hasta, DateTimeOffset.UtcNow, ct);
        return new CantidadActualizadaDto(cantidad);
    }

    /// <summary>Validates exactly one of <c>{ ids }</c> / <c>{ todas: true, hasta? }</c>.</summary>
    private static (IReadOnlyCollection<Guid>? Ids, DateTimeOffset? Hasta) LeerSeleccion(JsonElement cuerpo)
    {
        if (cuerpo.ValueKind != JsonValueKind.Object)
            throw Seleccion("todas", "Enviá ids o todas: true");

        var tieneIds = cuerpo.TryGetProperty("ids", out var idsJson);
        var tieneTodas = cuerpo.TryGetProperty("todas", out var todasJson);
        var tieneHasta = cuerpo.TryGetProperty("hasta", out var hastaJson);

        if (tieneIds)
        {
            if (tieneTodas) throw Seleccion("todas", "Enviá ids o todas: true, pero no ambos a la vez");
            if (tieneHasta) throw Seleccion("hasta", "hasta solo se puede usar con todas: true");
            if (idsJson.ValueKind != JsonValueKind.Array) throw Seleccion("ids", "ids debe ser una lista");
            var elementos = idsJson.EnumerateArray().ToList();
            if (elementos.Count < 1) throw Seleccion("ids", "ids debe tener al menos 1 elemento");
            if (elementos.Count > MaximoIds) throw Seleccion("ids", $"ids no puede tener más de {MaximoIds} elementos");
            var ids = new List<Guid>(elementos.Count);
            foreach (var elemento in elementos)
            {
                if (elemento.ValueKind != JsonValueKind.String || !Guid.TryParse(elemento.GetString(), out var id))
                    throw Seleccion("ids", "Cada id debe ser un UUID");
                ids.Add(id);
            }
            if (ids.Distinct().Count() != ids.Count) throw Seleccion("ids", "ids no puede tener elementos repetidos");
            return (ids, null);
        }

        if (!tieneTodas) throw Seleccion("todas", "Enviá ids o todas: true");
        if (todasJson.ValueKind != JsonValueKind.True) throw Seleccion("todas", "todas solo acepta el valor true");
        if (!tieneHasta) return (null, null);
        if (hastaJson.ValueKind != JsonValueKind.String
            || !FechaConZona().IsMatch(hastaJson.GetString()!)
            || !DateTimeOffset.TryParse(hastaJson.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hasta))
        {
            throw Seleccion("hasta",
                "hasta debe ser una fecha ISO 8601 con hora y zona horaria (ej. 2026-10-15T12:00:00.000Z)");
        }
        return (null, hasta);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex FechaConZona();

    private (Guid UsuarioId, string? Perfil) Sesion()
    {
        if (!usuarioActual.IsAuthenticated || !Guid.TryParse(usuarioActual.UserId, out var usuarioId))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoAutenticado, "unauthenticated", "Se requiere autenticación.");
        }

        return (usuarioId, usuarioActual.Roles.FirstOrDefault());
    }

    private static int EnteroPositivo(string? valor, string campo, int porDefecto, int? maximo)
    {
        if (valor is null) return porDefecto;
        if (!int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
            || numero < 1 || (maximo is { } tope && numero > tope))
        {
            throw Validacion(campo, $"{campo} debe ser un entero entre 1 y {(maximo?.ToString(CultureInfo.InvariantCulture) ?? "∞")}.");
        }
        return numero;
    }

    private static ExcepcionAplicacion Validacion(string campo, string mensaje) =>
        new(TipoErrorAplicacion.Validacion, "validation", mensaje,
            new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static ExcepcionAplicacion Seleccion(string campo, string mensaje) =>
        new(TipoErrorAplicacion.Validacion, "notificaciones-seleccion-invalida", mensaje,
            new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static NotificacionDto Mapear(Notificacion n) =>
        new(n.Id, n.RolDestino, n.TipoEvento, n.TipoEntidad, n.EntidadId, n.Titulo, n.Mensaje,
            JsonSerializer.Deserialize<JsonElement>(n.Contexto), n.LeidaEn, n.CreadaEn);
}
