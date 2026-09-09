using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

public sealed class ServicioCatalogosDesignaciones(
    RepositorioCatalogosDesignaciones repositorio,
    IConsultasIdentity identity,
    ResolutorActor resolutorActor)
{
    private static readonly string[] TiposBaja = ["Renuncia", "Jubilación", "Otro"];

    public async Task<CatalogosDesignacionesDto> ObtenerAsync(CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);
        var periodos = await repositorio.ListarPeriodosAsync(ct);
        var activo = periodos.SingleOrDefault(p => p.Activo);
        var materias = (await identity.ListarMateriasAsync(ct)).Where(m => m.Activo).ToArray();
        var visibles = actor.EsDeptoWide
            ? materias
            : actor.Tiene(RolesCircuito.JefeCatedra)
                ? materias.Where(m => actor.MateriasACargo.Contains(m.Id)).ToArray()
                : actor.Tiene(RolesCircuito.CoordinadorCarrera)
                    ? materias.Where(m => actor.CarrerasACargo.Contains(m.CarreraId)).ToArray()
                    : [];

        var ocupadas = activo is null
            ? new HashSet<Guid>()
            : await repositorio.ListarPersonasConPedidoVivoAsync(activo.Id, ct);
        var materiasPorId = materias.ToDictionary(m => m.Id);
        var designaciones = (await repositorio.ListarDesignacionesVigentesAsync(ct))
            .GroupBy(d => d.PersonaId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var personas = (await identity.ListarPersonasAsync(ct))
            .Where(p => !ocupadas.Contains(p.Id))
            .Select(p => new PersonaDesignacionesDto(
                p.Id, p.Nombre, p.Apellido, p.Documento, p.Legajo,
                designaciones.GetValueOrDefault(p.Id, [])
                    .Where(d => materiasPorId.ContainsKey(d.MateriaId) && d.Cargo is not null)
                    .Select(d => new DesignacionVigenteCatalogoDto(
                        d.MateriaId,
                        materiasPorId[d.MateriaId].Nombre,
                        d.CargoId,
                        d.Cargo!.Nombre,
                        d.DedicacionCatalogo?.Nombre ?? d.Dedicacion,
                        d.Horas,
                        d.DedicacionId, d.HorasInvestigacion, d.HorasExternas))
                    .ToArray()))
            .ToArray();
        var cargos = (await repositorio.ListarCargosActivosAsync(ct))
            .Select(c => new CargoDesignacionesDto(
                c.Id, c.Codigo, c.Nombre, c.Abreviatura, c.Orden))
            .ToArray();

        return new CatalogosDesignacionesDto(
            activo is null ? null : Mapear(activo),
            periodos.Select(Mapear).ToArray(),
            visibles.Select(m => new MateriaDesignacionesDto(
                m.Id, m.Codigo, m.Nombre, m.CarreraId)).ToArray(),
            personas,
            cargos,
            (await repositorio.ListarDedicacionesActivasAsync(ct))
                .Select(d => new DedicacionDesignacionesDto(d.Id, d.Codigo, d.Nombre, d.Orden)).ToArray(),
            TiposBaja,
            Novedades.Admitidas.Order().ToArray());
    }

    private static PeriodoDto Mapear(Periodo p) => new(
        p.Id, p.Nombre, p.CargaDesde, p.CargaHasta,
        p.ImpactoDesde, p.ImpactoHasta, p.Activo, p.Version);
}
