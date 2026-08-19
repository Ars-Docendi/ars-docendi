using ArsDocendi.Shared.Aplicacion;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

public sealed class ServicioPeriodos(IRepositorioPeriodos repositorio)
{
    public async Task<IReadOnlyList<PeriodoDto>> ListarAsync(CancellationToken ct) =>
        (await repositorio.ListarAsync(ct)).Select(Mapear).ToArray();

    public async Task<PeriodoDto> ObtenerAsync(Guid id, CancellationToken ct) =>
        Mapear(await ObtenerRequeridoAsync(id, false, ct));

    public async Task<PeriodoDto> CrearAsync(GuardarPeriodoDto datos, CancellationToken ct)
    {
        await ValidarAsync(datos, null, ct);
        var periodo = new Periodo
        {
            Id = Guid.NewGuid(),
            Nombre = datos.Nombre.Trim(),
            CargaDesde = datos.CargaDesde,
            CargaHasta = datos.CargaHasta,
            ImpactoDesde = datos.ImpactoDesde,
            ImpactoHasta = datos.ImpactoHasta,
            Activo = datos.Activo,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        repositorio.Agregar(periodo);
        await repositorio.GuardarAsync(ct);
        return Mapear(periodo);
    }

    public async Task<PeriodoDto> EditarAsync(Guid id, GuardarPeriodoDto datos, CancellationToken ct)
    {
        if (datos.Version is null) throw ErrorCampo("version", "Campo obligatorio.");
        await ValidarAsync(datos, id, ct);
        var periodo = await ObtenerRequeridoAsync(id, true, ct);
        repositorio.EsperarVersion(periodo, datos.Version.Value);
        periodo.Nombre = datos.Nombre.Trim();
        periodo.CargaDesde = datos.CargaDesde;
        periodo.CargaHasta = datos.CargaHasta;
        periodo.ImpactoDesde = datos.ImpactoDesde;
        periodo.ImpactoHasta = datos.ImpactoHasta;
        periodo.Activo = datos.Activo;
        await repositorio.GuardarAsync(ct);
        return Mapear(periodo);
    }

    public async Task<PeriodoDto> CambiarEstadoAsync(
        Guid id,
        bool activo,
        uint version,
        CancellationToken ct)
    {
        if (activo && await repositorio.ExisteOtroActivoAsync(id, ct)) throw ConflictoActivo();
        var periodo = await ObtenerRequeridoAsync(id, true, ct);
        repositorio.EsperarVersion(periodo, version);
        periodo.Activo = activo;
        await repositorio.GuardarAsync(ct);
        return Mapear(periodo);
    }

    public async Task EliminarAsync(Guid id, CancellationToken ct)
    {
        var periodo = await ObtenerRequeridoAsync(id, true, ct);
        repositorio.Eliminar(periodo);
        await repositorio.GuardarAsync(ct);
    }

    private async Task ValidarAsync(GuardarPeriodoDto datos, Guid? id, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(datos.Nombre)) errores["nombre"] = ["Campo obligatorio."];
        if (datos.CargaHasta < datos.CargaDesde)
            errores["cargaHasta"] = ["Debe ser posterior o igual a cargaDesde."];
        if (datos.ImpactoHasta < datos.ImpactoDesde)
            errores["impactoHasta"] = ["Debe ser posterior o igual a impactoDesde."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Revisá las fechas del período.", errores);
        }
        if (datos.Activo && await repositorio.ExisteOtroActivoAsync(id, ct)) throw ConflictoActivo();
    }

    private async Task<Periodo> ObtenerRequeridoAsync(Guid id, bool tracking, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, tracking, ct)
        ?? throw new ExcepcionAplicacion(
            TipoErrorAplicacion.NoEncontrado, "resource-not-found", "No se encontró el período solicitado.");

    private static ExcepcionAplicacion ErrorCampo(string campo, string mensaje) => new(
        TipoErrorAplicacion.Validacion,
        "validation",
        "Revisá los datos del período.",
        new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static ExcepcionAplicacion ConflictoActivo() => new(
        TipoErrorAplicacion.Conflicto,
        "periodo-active-conflict",
        "Ya existe otro período activo. Desactivalo primero.");

    private static PeriodoDto Mapear(Periodo p) => new(
        p.Id, p.Nombre, p.CargaDesde, p.CargaHasta,
        p.ImpactoDesde, p.ImpactoHasta, p.Activo, p.Version);
}
