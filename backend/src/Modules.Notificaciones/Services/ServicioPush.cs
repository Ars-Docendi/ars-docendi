using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using Modules.Notificaciones.Contracts;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Repositories;

namespace Modules.Notificaciones.Services;

public sealed record ClavePublicaPushDto(string ClavePublica);

public sealed record ClavesSuscripcionDto(string P256dh, string Auth);

/// <summary>Shape of the browser's <c>PushSubscription.toJSON()</c>.</summary>
public sealed record SuscripcionPushDto(string Endpoint, ClavesSuscripcionDto? Keys);

public sealed record CancelarSuscripcionPushDto(string Endpoint);

public sealed record PreferenciaPushDto(string Modulo, bool Habilitado);

public sealed record PreferenciasPushDto(IReadOnlyList<PreferenciaPushDto> Modulos);

public interface IServicioPush
{
    /// <summary>VAPID public key, or <c>null</c> when Web Push is disabled.</summary>
    string? ClavePublica { get; }
    Task SuscribirAsync(SuscripcionPushDto dto, CancellationToken ct);
    Task CancelarAsync(string endpoint, CancellationToken ct);
    Task<PreferenciasPushDto> ObtenerPreferenciasAsync(CancellationToken ct);
    Task<PreferenciasPushDto> GuardarPreferenciasAsync(PreferenciasPushDto dto, CancellationToken ct);
}

internal sealed class ServicioPush(
    RepositorioPush repositorio, ICurrentUser usuarioActual, ConfiguracionWebPush configuracion) : IServicioPush
{
    private const int MaximoClave = 256;

    public string? ClavePublica => configuracion.ClavePublica;

    public async Task SuscribirAsync(SuscripcionPushDto dto, CancellationToken ct)
    {
        var usuarioId = UsuarioId();
        if (!EndpointsPush.EsPermitido(dto.Endpoint))
            throw Validacion("endpoint", "El endpoint debe ser una URL https de un servicio de notificaciones conocido.");
        var claves = dto.Keys;
        if (claves is null || string.IsNullOrWhiteSpace(claves.P256dh) || string.IsNullOrWhiteSpace(claves.Auth)
            || claves.P256dh.Length > MaximoClave || claves.Auth.Length > MaximoClave)
            throw Validacion("keys", "keys.p256dh y keys.auth son obligatorios.");

        await repositorio.GuardarSuscripcionAsync(new SuscripcionPush
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Endpoint = dto.Endpoint,
            P256dh = claves.P256dh,
            Auth = claves.Auth,
            CreadaEn = DateTimeOffset.UtcNow,
        }, ct);
    }

    public Task CancelarAsync(string endpoint, CancellationToken ct) =>
        repositorio.EliminarSuscripcionAsync(UsuarioId(), endpoint ?? string.Empty, ct);

    public async Task<PreferenciasPushDto> ObtenerPreferenciasAsync(CancellationToken ct)
    {
        var guardadas = await repositorio.ListarPreferenciasAsync(UsuarioId(), ct);
        return new PreferenciasPushDto(ModulosPush.Todos
            .Select(modulo => new PreferenciaPushDto(modulo, guardadas.GetValueOrDefault(modulo, true)))
            .ToList());
    }

    public async Task<PreferenciasPushDto> GuardarPreferenciasAsync(PreferenciasPushDto dto, CancellationToken ct)
    {
        var usuarioId = UsuarioId();
        var desconocidos = (dto.Modulos ?? []).Select(m => m.Modulo)
            .Where(modulo => !ModulosPush.Todos.Contains(modulo)).Distinct().ToArray();
        if (dto.Modulos is null || desconocidos.Length > 0)
            throw Validacion("modulos", "Hay módulos desconocidos o la lista falta.");

        // Last entry wins for a repeated module; nothing is written unless the whole body is valid.
        var cambios = dto.Modulos.GroupBy(m => m.Modulo).ToDictionary(g => g.Key, g => g.Last().Habilitado);
        await repositorio.GuardarPreferenciasAsync(usuarioId, cambios, ct);
        return await ObtenerPreferenciasAsync(ct);
    }

    private static ExcepcionAplicacion Validacion(string campo, string mensaje) =>
        new(TipoErrorAplicacion.Validacion, "validation", mensaje,
            new Dictionary<string, string[]> { [campo] = [mensaje] });

    private Guid UsuarioId() =>
        Guid.TryParse(usuarioActual.UserId, out var id) ? id : throw new UnauthorizedAccessException();
}
