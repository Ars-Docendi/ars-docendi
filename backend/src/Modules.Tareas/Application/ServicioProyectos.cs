using ArsDocendi.Shared.Aplicacion;
using Modules.Tareas.Domain;
using Modules.Tareas.Repositories;

namespace Modules.Tareas.Application;

/// <summary>
/// Proyectos: crear y cambiar su estado (`proyectos.gestionar`, por rol y no por autoría).
/// El Responsable es Decanato o Secretaría Académica y respeta la jerarquía de asignación.
/// </summary>
public sealed class ServicioProyectos(RepositorioTareas repositorio, DirectorioPersonas directorio)
{
    public async Task<IReadOnlyList<ProyectoTareasDto>> ListarAsync(CancellationToken ct)
    {
        var proyectos = await repositorio.ListarProyectosAsync(ct);
        var personas = await directorio.CargarAsync(ct);
        return proyectos.Select(p => Mapear(p, personas)).ToList();
    }

    public async Task<ProyectoTareasDto> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw ServicioTareas.NoEncontrada("El proyecto no existe.");
        return Mapear(proyecto, await directorio.CargarAsync(ct));
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

        var proyecto = new Proyecto
        {
            Id = Guid.NewGuid(),
            Nombre = datos.Nombre.Trim(),
            Descripcion = datos.Descripcion?.Trim() ?? string.Empty,
            FechaInicio = datos.FechaInicio,
            FechaFin = datos.FechaFin,
            Estado = EstadosProyecto.Abierto,
            ResponsableId = datos.ResponsableId,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        repositorio.Agregar(proyecto);
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, personas);
    }

    public async Task<ProyectoTareasDto> CambiarEstadoAsync(Guid id, CambiarEstadoProyectoRequest datos, CancellationToken ct)
    {
        if (!EstadosProyecto.Todos.Contains(datos.Estado))
        {
            throw ServicioTareas.Validacion("estado", "El estado del proyecto no es válido.");
        }

        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw ServicioTareas.NoEncontrada("El proyecto no existe.");
        proyecto.Estado = datos.Estado;
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, await directorio.CargarAsync(ct));
    }

    private static ProyectoTareasDto Mapear(Proyecto p, IReadOnlyDictionary<Guid, PersonaResuelta> personas) =>
        new(p.Id, p.Numero, p.Nombre, p.Descripcion, p.FechaInicio, p.FechaFin, p.Estado,
            ServicioTareas.Persona(personas, p.ResponsableId));
}
