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
        var personas = await directorio.CargarAsync(ct);
        ValidarDatos(datos.Nombre, datos.FechaInicio, datos.FechaFin);
        ValidarResponsable(actor, personas, datos.ResponsableId);

        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        var proyecto = new Proyecto
        {
            Id = Guid.NewGuid(),
            Nombre = datos.Nombre.Trim(),
            Descripcion = datos.Descripcion?.Trim() ?? string.Empty,
            FechaInicio = datos.FechaInicio,
            FechaFin = datos.FechaFin,
            EstadoId = catalogos.EstadoInicialProyecto.Id,
            ResponsableId = datos.ResponsableId,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        repositorio.Agregar(proyecto);
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, catalogos, personas);
    }

    public async Task<ProyectoTareasDto> EditarAsync(Guid id, EditarProyectoRequest datos, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw ServicioTareas.NoEncontrada("El proyecto no existe.");
        var personas = await directorio.CargarAsync(ct);
        ValidarDatos(datos.Nombre, datos.FechaInicio, datos.FechaFin);

        // Conservar al mismo Responsable no reevalúa el rol ni la jerarquía, para no bloquear
        // la edición de un proyecto si el rol de esa persona cambió después.
        if (datos.ResponsableId != proyecto.ResponsableId)
        {
            ValidarResponsable(actor, personas, datos.ResponsableId);
        }

        proyecto.Nombre = datos.Nombre.Trim();
        proyecto.Descripcion = datos.Descripcion?.Trim() ?? string.Empty;
        proyecto.FechaInicio = datos.FechaInicio;
        proyecto.FechaFin = datos.FechaFin;
        proyecto.ResponsableId = datos.ResponsableId;
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, await repositorio.ObtenerCatalogosAsync(ct), personas);
    }

    public async Task<ProyectoTareasDto> CambiarEstadoAsync(Guid id, CambiarEstadoProyectoRequest datos, CancellationToken ct)
    {
        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        var destino = catalogos.EstadoProyecto(datos.Estado)
            ?? throw ServicioTareas.Validacion("estado", "El estado del proyecto no es válido.");

        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw ServicioTareas.NoEncontrada("El proyecto no existe.");

        // Un proyecto cancelado no se da por finalizado: tendría que reabrirse primero.
        if (catalogos.EstadoProyecto(proyecto.EstadoId)?.Codigo == EstadosProyecto.Cancelado
            && destino.Codigo == EstadosProyecto.Finalizado)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ReglaDeNegocio,
                "proyecto-cancelado-no-finalizable",
                "Un proyecto Cancelado no puede pasar a Finalizado.");
        }

        proyecto.EstadoId = destino.Id;
        await repositorio.GuardarAsync(ct);
        return Mapear(proyecto, catalogos, await directorio.CargarAsync(ct));
    }

    private static void ValidarDatos(string nombre, DateOnly fechaInicio, DateOnly fechaFin)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ServicioTareas.Validacion("nombre", "El nombre es obligatorio.");
        }

        MaquinaEstadosTarea.ValidarFechas(fechaInicio, fechaFin);
    }

    private static void ValidarResponsable(
        ActorTareas actor, IReadOnlyDictionary<Guid, PersonaResuelta> personas, Guid responsableId)
    {
        if (!personas.TryGetValue(responsableId, out var responsable) || !responsable.Activa)
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
    }

    private static ProyectoTareasDto Mapear(
        Proyecto p, CatalogosTareas catalogos, IReadOnlyDictionary<Guid, PersonaResuelta> personas)
    {
        var estado = catalogos.EstadoProyecto(p.EstadoId);
        return new ProyectoTareasDto(
            p.Id, p.Numero, p.Nombre, p.Descripcion, p.FechaInicio, p.FechaFin,
            estado?.Codigo ?? string.Empty, estado?.Nombre ?? string.Empty, estado?.AdmiteTareas ?? false,
            ServicioTareas.Persona(personas, p.ResponsableId));
    }
}
