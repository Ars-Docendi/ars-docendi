using Modules.Designaciones.Contracts.Administracion;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

internal sealed class ServicioAdministracionDesignaciones(IRepositorioDesignaciones repositorio)
    : IAdministracionDesignaciones
{
    public async Task<IReadOnlyList<DesignacionVigenteDto>> ListarVigentesAsync(CancellationToken ct) =>
        (await repositorio.ListarTodasVigentesSinTrackingAsync(ct)).Select(Mapear).ToArray();

    public async Task<IReadOnlyList<CargoAdministracionDto>> ListarCargosAsync(CancellationToken ct) =>
        (await repositorio.ListarCargosAsync(ct)).Select(Mapear).ToArray();

    public async Task ValidarReemplazoAsync(
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones,
        CancellationToken ct)
    {
        if (designaciones.Select(d => d.MateriaId).Distinct().Count() != designaciones.Count)
        {
            throw new ErrorDominioPedido("No se puede repetir una materia en las designaciones vigentes.");
        }
        if (designaciones.Any(d => d.Horas <= 0))
        {
            throw new ErrorDominioPedido("Las horas de una designación deben ser mayores a cero.");
        }
        var cargos = designaciones.Select(d => d.CargoId).Distinct().ToArray();
        if ((await repositorio.ObtenerCargosActivosAsync(cargos, ct)).Count != cargos.Length)
        {
            throw new ErrorDominioPedido("Uno de los cargos no existe o está inactivo.");
        }
    }

    public async Task<IReadOnlyList<DesignacionVigenteDto>> ReemplazarVigentesAsync(
        Guid personaId,
        IReadOnlyList<GuardarDesignacionVigenteDto> designaciones,
        CancellationToken ct)
    {
        await ValidarReemplazoAsync(designaciones, ct);
        var actuales = await repositorio.ListarVigentesDePersonaAsync(personaId, ct);
        var deseadas = designaciones.ToDictionary(d => d.MateriaId);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var actual in actuales)
        {
            if (!deseadas.Remove(actual.MateriaId, out var deseada))
            {
                actual.VigenteHasta = hoy > actual.VigenteDesde
                    ? hoy
                    : actual.VigenteDesde.AddDays(1);
                continue;
            }

            actual.CargoId = deseada.CargoId;
            actual.Dedicacion = NormalizarOpcional(deseada.Dedicacion);
            actual.Horas = deseada.Horas;
            // Una edición administrativa deja de atribuir el estado resultante
            // al pedido original; audit.change_log conserva quién y qué cambió.
            actual.OrigenPedidoId = null;
        }

        foreach (var deseada in deseadas.Values)
        {
            repositorio.Agregar(new Designacion
            {
                Id = Guid.NewGuid(),
                PersonaId = personaId,
                MateriaId = deseada.MateriaId,
                CargoId = deseada.CargoId,
                Dedicacion = NormalizarOpcional(deseada.Dedicacion),
                Horas = deseada.Horas,
                VigenteDesde = hoy,
                CreadoEn = DateTimeOffset.UtcNow,
            });
        }

        await repositorio.GuardarAsync(ct);
        return (await repositorio.ListarTodasVigentesSinTrackingAsync(ct))
            .Where(d => d.PersonaId == personaId)
            .Select(Mapear)
            .OrderBy(d => d.MateriaId)
            .ToArray();
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static DesignacionVigenteDto Mapear(Designacion d) => new(
        d.Id,
        d.PersonaId,
        d.MateriaId,
        d.CargoId,
        d.Cargo?.Nombre ?? string.Empty,
        d.Cargo?.Abreviatura ?? string.Empty,
        d.Dedicacion,
        d.Horas,
        d.VigenteDesde);

    private static CargoAdministracionDto Mapear(Cargo c) => new(
        c.Id, c.Codigo, c.Nombre, c.Abreviatura, c.Orden, c.Activo);
}
