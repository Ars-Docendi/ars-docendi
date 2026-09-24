using ArsDocendi.Shared.Aplicacion;
using Modules.Tareas.Domain;
using Modules.Tareas.Repositories;

namespace Modules.Tareas.Application;

/// <summary>
/// Proyectos: crear y cambiar su estado (`proyectos.gestionar`, por rol y no por autoría).
/// El Responsable es Decanato o Secretaría Académica y respeta la jerarquía de asignación.
/// Los estados salen del catálogo `tareas.estados_proyecto`.
/// </summary>
public sealed class ServicioProyectos(RepositorioTareas repositorio, DirectorioPersonas directorio)
{
    public async Task<IReadOnlyList<EstadoProyectoDto>> ListarEstadosAsync(CancellationToken ct) =>
        (await repositorio.ObtenerCatalogosAsync(ct)).EstadosProyecto
            .Select(e => new EstadoProyectoDto(e.Codigo, e.Nombre, e.Verbo, e.EsInicial, e.AdmiteTareas))
            .ToList();

    public async Task<IReadOnlyList<ProyectoTareasDto>> ListarAsync(CancellationToken ct)
    {
        var proyectos = await repositorio.ListarProyectosAsync(ct);
        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        var personas = await directorio.CargarAsync(ct);
        return proyectos.Select(p => Mapear(p, catalogos, personas)).ToList();
    }

    public async Task<ProyectoTareasDto> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw ServicioTareas.NoEncontrada("El proyecto no existe.");
        return Mapear(proyecto, await repositorio.ObtenerCatalogosAsync(ct), await directorio.CargarAsync(ct));
    }

    public async Task<ProyectoTareasDto> CrearAsync(CrearProyectoRequest datos, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            throw ServicioTareas.Validacion("nombre", "El nombre es obligatorio.");
        }

        MaquinaEstadosTarea.ValidarFechas(datos.FechaInicio, datos.FechaFin);

        var personas = await directorio.CargarAsync(ct);
        if (!personas.TryGetValue(datos.ResponsableId, out var responsable) || !responsable.Activa)
        {
            throw ServicioTareas.Validacion("responsableId", "El Responsable indicado no existe o está inactivo.");
        }

        if (!JerarquiaAsignacion.EsResponsableDeProyecto(responsable.RolesCodigo)
            || !JerarquiaAsignacion.PuedeAsignar(actor.Nivel, responsable.RolesCodigo))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ReglaDeNegocio,
                "proyecto-responsable-invalido",
                "El Responsable de un Proyecto debe ser Decanato o Secretaría Académica, respetando la jerarquía.");
        }

        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        var proyecto = new Proyecto
        {
            Id = Guid.NewGuid(),
            Nombre = datos.Nombre.Trim(),
            Descripcion = datos.Descripcion?.Trim() ?? string.Empty,
            FechaInicio = datos.FechaInicio,
            FechaFin = datos.FechaFin,
            Estado = catalogos.EstadoInicialProyecto,
            ResponsableId = datos.ResponsableId,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        repositorio.Agregar(proyecto);
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, catalogos, personas);
    }

    public async Task<ProyectoTareasDto> CambiarEstadoAsync(Guid id, CambiarEstadoProyectoRequest datos, CancellationToken ct)
    {
        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        if (catalogos.EstadoProyecto(datos.Estado) is null)
        {
            throw ServicioTareas.Validacion("estado", "El estado del proyecto no es válido.");
        }

        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw ServicioTareas.NoEncontrada("El proyecto no existe.");
        proyecto.Estado = datos.Estado;
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, catalogos, await directorio.CargarAsync(ct));
    }

    private static ProyectoTareasDto Mapear(
        Proyecto p, CatalogosTareas catalogos, IReadOnlyDictionary<Guid, PersonaResuelta> personas)
    {
        var estado = catalogos.EstadoProyecto(p.Estado);
        return new ProyectoTareasDto(
            p.Id, p.Numero, p.Nombre, p.Descripcion, p.FechaInicio, p.FechaFin, p.Estado,
            estado?.Nombre ?? p.Estado, estado?.AdmiteTareas ?? false,
            ServicioTareas.Persona(personas, p.ResponsableId));
    }
}
