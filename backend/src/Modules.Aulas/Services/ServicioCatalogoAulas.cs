using ArsDocendi.Shared.Aplicacion;
using Modules.Aulas.Api;
using Modules.Aulas.Domain;
using Modules.Aulas.Repositories;

namespace Modules.Aulas.Services;

public interface IServicioCatalogoAulas
{
    Task<IReadOnlyList<AulaDto>> ListarAsync(bool soloActivas, CancellationToken ct);
    Task<AulaDto> CrearAsync(GuardarAulaDto datos, CancellationToken ct);
    Task<AulaDto> EditarAsync(Guid id, GuardarAulaDto datos, CancellationToken ct);
    Task<AulaDto> CambiarEstadoAsync(Guid id, bool activa, uint version, CancellationToken ct);
    Task EliminarAsync(Guid id, CancellationToken ct);
}

internal sealed class ServicioCatalogoAulas(RepositorioSolicitudesAula repositorio) : IServicioCatalogoAulas
{
    public async Task<IReadOnlyList<AulaDto>> ListarAsync(bool soloActivas, CancellationToken ct) =>
        (await repositorio.ListarAulasAsync(ct))
            .Where(a => !soloActivas || a.Activa)
            .Select(Mapear)
            .ToArray();

    public async Task<AulaDto> CrearAsync(GuardarAulaDto datos, CancellationToken ct)
    {
        Validar(datos);
        var aula = new Aula
        {
            Id = Guid.NewGuid(),
            Nombre = datos.Nombre.Trim(),
            Tipo = datos.Tipo,
            Capacidad = datos.Capacidad,
            Equipamiento = Normalizar(datos.Equipamiento),
            Activa = true,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        repositorio.AgregarAula(aula);
        await repositorio.GuardarCambiosAsync(ct);
        return Mapear(aula);
    }

    public async Task<AulaDto> EditarAsync(Guid id, GuardarAulaDto datos, CancellationToken ct)
    {
        if (datos.Version is null) throw ErrorCampo("version", "Campo obligatorio.");
        Validar(datos);
        var aula = await ObtenerRequeridaAsync(id, ct);
        repositorio.EsperarVersion(aula, datos.Version.Value);
        aula.Nombre = datos.Nombre.Trim();
        aula.Tipo = datos.Tipo;
        aula.Capacidad = datos.Capacidad;
        aula.Equipamiento = Normalizar(datos.Equipamiento);
        await repositorio.GuardarCambiosAsync(ct);
        return Mapear(aula);
    }

    public async Task<AulaDto> CambiarEstadoAsync(Guid id, bool activa, uint version, CancellationToken ct)
    {
        var aula = await ObtenerRequeridaAsync(id, ct);
        repositorio.EsperarVersion(aula, version);
        aula.Activa = activa;
        await repositorio.GuardarCambiosAsync(ct);
        return Mapear(aula);
    }

    public async Task EliminarAsync(Guid id, CancellationToken ct)
    {
        repositorio.EliminarAula(await ObtenerRequeridaAsync(id, ct));
        await repositorio.GuardarCambiosAsync(ct);
    }

    private static void Validar(GuardarAulaDto datos)
    {
        var errores = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(datos.Nombre)) errores["nombre"] = ["Campo obligatorio."];
        if (datos.Tipo is not (TiposAula.Aula or TiposAula.Laboratorio))
            errores["tipo"] = ["Elegí aula o laboratorio."];
        if (datos.Capacidad <= 0) errores["capacidad"] = ["Debe ser mayor a cero."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Revisá los datos del aula.", errores);
        }
    }

    private static string? Normalizar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private async Task<Aula> ObtenerRequeridaAsync(Guid id, CancellationToken ct) =>
        await repositorio.ObtenerAulaAsync(id, true, ct)
        ?? throw new ExcepcionAplicacion(
            TipoErrorAplicacion.NoEncontrado, "aula-not-found", "No se encontró el aula.");

    private static ExcepcionAplicacion ErrorCampo(string campo, string mensaje) => new(
        TipoErrorAplicacion.Validacion,
        "validation",
        "Revisá los datos del aula.",
        new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static AulaDto Mapear(Aula a) =>
        new(a.Id, a.Nombre, a.Tipo, a.Capacidad, a.Equipamiento, a.Activa, a.Version);
}
