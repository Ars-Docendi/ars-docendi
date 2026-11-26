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
    private const int MaximoResultadosBusqueda = 20;

    /// <summary>
    /// Personas elegibles para un Alta: coincide el texto con apellido y nombre (sin distinguir
    /// mayúsculas ni acentos), o con los dígitos del DNI o del legajo. Excluye a quien ya tiene un
    /// pedido vivo en el período activo [BR-designaciones-001] y a quien ya tiene una designación
    /// vigente: un Alta es para alguien todavía no designado.
    /// </summary>
    public async Task<IReadOnlyList<PersonaBusquedaDto>> BuscarPersonasAsync(string texto, CancellationToken ct)
    {
        var termino = Normalizar(texto);
        if (termino.Length < 2) return [];
        var digitos = new string(texto.Where(char.IsAsciiDigit).ToArray());

        var activo = (await repositorio.ListarPeriodosAsync(ct)).SingleOrDefault(p => p.Activo);
        var ocupadas = activo is null
            ? new HashSet<Guid>()
            : new HashSet<Guid>(await repositorio.ListarPersonasConPedidoVivoAsync(activo.Id, null, ct));
        ocupadas.UnionWith((await repositorio.ListarDesignacionesVigentesAsync(ct)).Select(d => d.PersonaId));

        return (await identity.ListarPersonasAsync(ct))
            .Where(p => !ocupadas.Contains(p.Id))
            .Where(p => Normalizar($"{p.Apellido} {p.Nombre}").Contains(termino)
                || (digitos.Length >= 3 && (p.Documento.Contains(digitos)
                    || (p.Legajo ?? string.Empty).Contains(digitos))))
            .OrderBy(p => p.Apellido, StringComparer.CurrentCulture)
            .ThenBy(p => p.Nombre, StringComparer.CurrentCulture)
            .Take(MaximoResultadosBusqueda)
            .Select(p => new PersonaBusquedaDto(p.Id, p.Nombre, p.Apellido, p.Documento, p.Legajo))
            .ToArray();
    }

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(descompuesto
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
    }

    public async Task<CatalogosDesignacionesDto> ObtenerAsync(CancellationToken ct, Guid? pedidoEditado = null)
    {
        var actor = await resolutorActor.ResolverAsync(ct);
        var periodos = await repositorio.ListarPeriodosAsync(ct);
        var activo = periodos.SingleOrDefault(p => p.Activo);
        // Catálogo informativo materia–carrera: una materia dictada en dos carreras
        // aparece dos veces (una por carrera). Se deduplica por (materia, carrera):
        // dos planes de la misma carrera con la misma materia no deben repetirse.
        var materias = (await identity.ListarMateriasPlanAsync(ct))
            .Where(mp => mp.Activo && mp.Materia!.Activo && mp.Plan!.Activo)
            .Select(mp => new MateriaDesignacionesDto(
                mp.MateriaId, mp.Materia!.Codigo, mp.Materia.Nombre,
                mp.Plan!.CarreraId, mp.Plan.Carrera!.Nombre))
            .Distinct()
            .ToArray();
        var visibles = actor.EsDeptoWide
            ? materias
            : actor.Tiene(RolesCircuito.JefeCatedra)
                ? materias.Where(m => actor.MateriasACargo.Contains(m.MateriaId)).ToArray()
                : actor.Tiene(RolesCircuito.CoordinadorCarrera)
                    ? materias.Where(m => actor.CarrerasACargo.Contains(m.CarreraId)).ToArray()
                    : [];

        // BR-001: quien ya tiene un pedido vivo no se ofrece para otro. Al editar, el propio
        // pedido no cuenta, así su persona sigue precargada y elegible.
        var ocupadas = activo is null
            ? new HashSet<Guid>()
            : await repositorio.ListarPersonasConPedidoVivoAsync(activo.Id, pedidoEditado, ct);
        var materiasCanonicasPorId = (await identity.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);
        var paresVisibles = visibles.Select(m => (m.MateriaId, m.CarreraId)).ToHashSet();
        var designaciones = (await repositorio.ListarDesignacionesVigentesAsync(ct))
            .GroupBy(d => d.PersonaId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var designacionesVisibles = designaciones
            .SelectMany(g => g.Value)
            .Where(d => actor.EsDeptoWide || paresVisibles.Contains((d.MateriaId, d.CarreraId)))
            .GroupBy(d => d.PersonaId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var personas = (await identity.ListarPersonasAsync(ct))
            .Where(p => !ocupadas.Contains(p.Id)
                && (actor.EsDeptoWide || designacionesVisibles.ContainsKey(p.Id)))
            .Select(p => new PersonaDesignacionesDto(
                p.Id, p.Nombre, p.Apellido, p.Documento, p.Legajo,
                designacionesVisibles.GetValueOrDefault(p.Id, [])
                    .Where(d => materiasCanonicasPorId.ContainsKey(d.MateriaId) && (d.Cargo is not null || d.SectorExterno is not null))
                    .Select(d => new DesignacionVigenteCatalogoDto(
                        d.MateriaId,
                        materiasCanonicasPorId[d.MateriaId].Nombre,
                        d.CarreraId,
                        d.CargoId,
                        d.Cargo?.Nombre,
                        d.DedicacionCatalogo?.Nombre,
                        d.Horas,
                        d.DedicacionId, d.HorasInvestigacion, d.HorasExternas,
                        d.SectorExternoId, d.SectorExterno?.Codigo, d.SectorExterno?.Nombre))
                    .ToArray()))
            .ToArray();
        var cargos = (await repositorio.ListarCargosActivosAsync(ct))
            .Select(c => new CargoDesignacionesDto(
                c.Id, c.Codigo, c.Nombre, c.Abreviatura, c.Orden))
            .ToArray();

        return new CatalogosDesignacionesDto(
            activo is null ? null : Mapear(activo),
            periodos.Select(Mapear).ToArray(),
            visibles,
            personas,
            cargos,
            (await repositorio.ListarDedicacionesActivasAsync(ct))
                .Select(d => new DedicacionDesignacionesDto(d.Id, d.Codigo, d.Nombre, d.Orden)).ToArray(),
            TiposBaja,
            Novedades.Admitidas.Order().ToArray(),
            (await repositorio.ListarSectoresExternosActivosAsync(ct))
                .Select(s => new SectorExternoDesignacionesDto(s.Id, s.Codigo, s.Nombre)).ToArray());
    }

    private static PeriodoDto Mapear(Periodo p) => new(
        p.Id, p.Nombre, p.CargaDesde, p.CargaHasta,
        p.ImpactoDesde, p.ImpactoHasta, p.Activo, p.Version);
}
