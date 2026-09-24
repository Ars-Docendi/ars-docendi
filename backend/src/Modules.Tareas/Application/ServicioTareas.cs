using ArsDocendi.Shared.Aplicacion;
using Modules.Tareas.Domain;
using Modules.Tareas.Repositories;

namespace Modules.Tareas.Application;

public sealed class ServicioTareas(RepositorioTareas repositorio, DirectorioPersonas directorio)
{
    /// <summary>Tope de resultados del buscador de candidatos: se afina buscando, no paginando.</summary>
    private const int MaximoCandidatos = 50;

    // Consultas ---------------------------------------------------------

    /// <summary>Quien no ve todas las tareas (sin `tareas.gestionar`) recibe solo las que tiene asignadas.</summary>
    public async Task<IReadOnlyList<TareaDto>> ListarAsync(CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var tareas = await repositorio.ListarAsync(actor.SoloResponsableId, ct);
        var personas = await directorio.CargarAsync(ct);
        var relaciones = Vecinos(await repositorio.ListarRelacionesAsync(ct));
        return tareas.Select(t => Mapear(t, personas, relaciones.GetValueOrDefault(t.Id) ?? [], detalle: false)).ToList();
    }

    public async Task<TareaDto> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        return await MapearDetalleAsync(await RequerirAsync(id, actor, ct), ct);
    }

    /// <summary>Usuarios que el actor puede asignar como Responsable, según la jerarquía, filtrados por la búsqueda.</summary>
    public async Task<IReadOnlyList<CandidatoResponsableDto>> ListarCandidatosAsync(
        bool paraProyecto, string? busqueda, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var candidatos = await directorio.ListarCandidatosAsync(busqueda, ct);
        return candidatos
            .Where(c => JerarquiaAsignacion.PuedeAsignar(actor.Nivel, c.RolesCodigo)
                     && (!paraProyecto || JerarquiaAsignacion.EsResponsableDeProyecto(c.RolesCodigo)))
            .Take(MaximoCandidatos)
            .Select(c => new CandidatoResponsableDto(c.Id, c.Nombre, c.RolNombre, c.Usuario, c.Legajo, c.Documento))
            .ToList();
    }

    // Comandos ----------------------------------------------------------

    public async Task<TareaDto> CrearAsync(CrearTareaRequest datos, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        ValidarDatos(datos.Titulo, datos.Prioridad, datos.Tipo, datos.FechaInicio, datos.FechaFin, catalogos);
        await RequerirResponsableAsignableAsync(actor, datos.ResponsableId, ct);

        // Una hija hereda el Proyecto de su padre: lo que llegue en `ProyectoId` se ignora.
        var proyectoId = datos.ProyectoId;
        if (datos.TareaPadreId is { } padreId)
        {
            var padre = await repositorio.ObtenerAsync(padreId, ct)
                ?? throw NoEncontrada("La tarea padre no existe.");
            proyectoId = padre.ProyectoId;
        }
        else
        {
            await RequerirProyectoQueAdmiteTareasAsync(proyectoId, catalogos, ct);
        }

        var ahora = DateTimeOffset.UtcNow;
        var tarea = new Tarea
        {
            Id = Guid.NewGuid(),
            Titulo = datos.Titulo.Trim(),
            Descripcion = datos.Descripcion?.Trim() ?? string.Empty,
            FechaInicio = datos.FechaInicio,
            FechaFin = datos.FechaFin,
            Prioridad = datos.Prioridad,
            Tipo = datos.Tipo,
            Estado = catalogos.EstadoInicialTarea,
            ResponsableId = datos.ResponsableId,
            CreadoPorId = actor.UsuarioId,
            ProyectoId = proyectoId,
            TareaPadreId = datos.TareaPadreId,
            CreadoEn = ahora,
        };
        MaquinaEstadosTarea.Registrar(tarea, actor, AccionesHistorial.Crear, null, ahora);

        repositorio.Agregar(tarea);
        await repositorio.GuardarAsync(ct);
        return await MapearDetalleAsync(tarea, ct);
    }

    public async Task<TareaDto> EditarAsync(Guid id, EditarTareaRequest datos, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var tarea = await RequerirAsync(id, actor, ct);
        MaquinaEstadosTarea.RequerirPuedeEditarCampos(tarea, actor);
        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        ValidarDatos(datos.Titulo, datos.Prioridad, datos.Tipo, datos.FechaInicio, datos.FechaFin, catalogos);

        // La jerarquía se exige al reasignar; conservar al mismo Responsable no la reevalúa,
        // para no bloquear la edición de una tarea si su rol cambió después.
        if (datos.ResponsableId != tarea.ResponsableId)
        {
            await RequerirResponsableAsignableAsync(actor, datos.ResponsableId, ct);
        }

        // Una hija conserva el Proyecto de su padre: no se puede desasociar por acá. Conservar
        // el Proyecto actual no exige que siga admitiendo tareas (p. ej. si ya se finalizó).
        if (tarea.TareaPadreId is null)
        {
            if (datos.ProyectoId != tarea.ProyectoId)
            {
                await RequerirProyectoQueAdmiteTareasAsync(datos.ProyectoId, catalogos, ct);
            }

            tarea.ProyectoId = datos.ProyectoId;
        }

        tarea.Titulo = datos.Titulo.Trim();
        tarea.Descripcion = datos.Descripcion?.Trim() ?? string.Empty;
        tarea.FechaInicio = datos.FechaInicio;
        tarea.FechaFin = datos.FechaFin;
        tarea.Prioridad = datos.Prioridad;
        tarea.Tipo = datos.Tipo;
        tarea.ResponsableId = datos.ResponsableId;
        MaquinaEstadosTarea.Registrar(tarea, actor, AccionesHistorial.Editar, null, DateTimeOffset.UtcNow);

        await repositorio.GuardarAsync(ct);
        return await MapearDetalleAsync(tarea, ct);
    }

    public async Task<TareaDto> CambiarEstadoAsync(Guid id, CambiarEstadoTareaRequest datos, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var tarea = await RequerirAsync(id, actor, ct);
        var catalogos = await repositorio.ObtenerCatalogosAsync(ct);
        if (!catalogos.EstadosTarea.Any(e => e.Codigo == datos.Estado))
        {
            throw Validacion("estado", $"El estado \"{datos.Estado}\" no existe.");
        }

        MaquinaEstadosTarea.CambiarEstado(tarea, actor, datos.Estado, datos.Comentario, datos.Solucion);
        await repositorio.GuardarAsync(ct);
        return await MapearDetalleAsync(tarea, ct);
    }

    public async Task<TareaDto> EditarAvanceAsync(Guid id, EditarAvanceRequest datos, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        var tarea = await RequerirAsync(id, actor, ct);
        MaquinaEstadosTarea.EditarAvance(tarea, actor, datos.PorcentajeAvance);
        await repositorio.GuardarAsync(ct);
        return await MapearDetalleAsync(tarea, ct);
    }

    public async Task<TareaDto> ComentarAsync(Guid id, ComentarTareaRequest datos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datos.Texto))
        {
            throw Validacion("texto", "El comentario no puede estar vacío.");
        }

        var actor = await directorio.ResolverActorAsync(ct);
        var tarea = await RequerirAsync(id, actor, ct);
        tarea.Comentarios.Add(MaquinaEstadosTarea.NuevoComentario(tarea, actor, datos.Texto, DateTimeOffset.UtcNow));
        await repositorio.GuardarAsync(ct);
        return await MapearDetalleAsync(tarea, ct);
    }

    /// <summary>Vínculo simple: sin jerarquía y sin efecto en estado ni avance. Idempotente.</summary>
    public async Task RelacionarAsync(Guid id, Guid otraId, CancellationToken ct)
    {
        if (id == otraId)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ReglaDeNegocio, "tarea-regla-invalida", "Una tarea no puede relacionarse consigo misma.");
        }

        // Quien solo ve sus tareas asignadas solo puede relacionar tareas que ve.
        var actor = await directorio.ResolverActorAsync(ct);
        if (!await repositorio.ExisteAsync(id, actor.SoloResponsableId, ct)
            || !await repositorio.ExisteAsync(otraId, actor.SoloResponsableId, ct))
        {
            throw NoEncontrada("La tarea no existe.");
        }

        var (menor, mayor) = Par(id, otraId);
        if (await repositorio.ObtenerRelacionAsync(menor, mayor, ct) is null)
        {
            repositorio.Agregar(new RelacionTarea { TareaId = menor, RelacionadaId = mayor, CreadoEn = DateTimeOffset.UtcNow });
            await repositorio.GuardarAsync(ct);
        }
    }

    public async Task QuitarRelacionAsync(Guid id, Guid otraId, CancellationToken ct)
    {
        var actor = await directorio.ResolverActorAsync(ct);
        if (!await repositorio.ExisteAsync(id, actor.SoloResponsableId, ct))
        {
            throw NoEncontrada("La tarea no existe.");
        }

        var (menor, mayor) = Par(id, otraId);
        if (await repositorio.ObtenerRelacionAsync(menor, mayor, ct) is { } relacion)
        {
            repositorio.Eliminar(relacion);
            await repositorio.GuardarAsync(ct);
        }
    }

    // Internos ----------------------------------------------------------

    /// <summary>Carga la tarea; si el actor no la ve (no le está asignada) responde como si no existiera.</summary>
    private async Task<Tarea> RequerirAsync(Guid id, ActorTareas actor, CancellationToken ct)
    {
        var tarea = await repositorio.ObtenerAsync(id, ct);
        if (tarea is null || (!actor.VeTodas && tarea.ResponsableId != actor.UsuarioId))
        {
            throw NoEncontrada("La tarea no existe.");
        }

        return tarea;
    }

    /// <summary>Un proyecto solo recibe tareas nuevas si su estado del catálogo lo admite (p. ej. Abierto).</summary>
    private async Task RequerirProyectoQueAdmiteTareasAsync(
        Guid? proyectoId, CatalogosTareas catalogos, CancellationToken ct)
    {
        if (proyectoId is not { } id) return;

        var proyecto = await repositorio.ObtenerProyectoAsync(id, ct)
            ?? throw Validacion("proyectoId", "El proyecto indicado no existe.");
        if (catalogos.EstadoProyecto(proyecto.Estado)?.AdmiteTareas != true)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ReglaDeNegocio,
                "proyecto-no-admite-tareas",
                "El proyecto elegido no admite tareas nuevas.");
        }
    }

    private async Task RequerirResponsableAsignableAsync(ActorTareas actor, Guid responsableId, CancellationToken ct)
    {
        var personas = await directorio.CargarAsync(ct);
        if (!personas.TryGetValue(responsableId, out var responsable) || !responsable.Activa)
        {
            throw Validacion("responsableId", "El Responsable indicado no existe o está inactivo.");
        }

        if (!JerarquiaAsignacion.PuedeAsignar(actor.Nivel, responsable.RolesCodigo))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ReglaDeNegocio,
                "tarea-jerarquia",
                "No podés asignar un Responsable de mayor jerarquía que la tuya.");
        }
    }

    private static void ValidarDatos(
        string titulo, string prioridad, string tipo, DateOnly inicio, DateOnly fin, CatalogosTareas catalogos)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw Validacion("titulo", "El título es obligatorio.");
        if (!catalogos.Prioridades.Any(p => p.Codigo == prioridad)) throw Validacion("prioridad", "La prioridad no es válida.");
        if (!catalogos.Tipos.Any(t => t.Codigo == tipo)) throw Validacion("tipo", "El tipo no es válido.");
        MaquinaEstadosTarea.ValidarFechas(inicio, fin);
    }

    private async Task<TareaDto> MapearDetalleAsync(Tarea tarea, CancellationToken ct)
    {
        var personas = await directorio.CargarAsync(ct);
        var vecinos = Vecinos(await repositorio.ListarRelacionesDeAsync(tarea.Id, ct));
        return Mapear(tarea, personas, vecinos.GetValueOrDefault(tarea.Id) ?? [], detalle: true);
    }

    private static TareaDto Mapear(
        Tarea t,
        IReadOnlyDictionary<Guid, PersonaResuelta> personas,
        IReadOnlyList<Guid> relacionadas,
        bool detalle) =>
        new(
            t.Id, t.Numero, t.Titulo, t.Descripcion, t.FechaInicio, t.FechaFin, t.Prioridad, t.Tipo, t.Estado,
            t.PorcentajeAvance, t.Solucion,
            Persona(personas, t.ResponsableId),
            Persona(personas, t.CreadoPorId),
            detalle
                ? t.Comentarios.OrderBy(c => c.CreadoEn)
                    .Select(c => new ComentarioTareaDto(c.Id, Persona(personas, c.AutorId).Nombre, c.AutorRol, c.Texto, c.CreadoEn))
                    .ToList()
                : [],
            detalle
                ? t.Historial.OrderBy(e => e.CreadoEn)
                    .Select(e => new EventoTareaDto(e.Id, e.Accion, e.ActorRol, Persona(personas, e.ActorId).Nombre, e.Estado, e.Detalle, e.CreadoEn))
                    .ToList()
                : [],
            t.ProyectoId, t.TareaPadreId, relacionadas);

    internal static PersonaTareaDto Persona(IReadOnlyDictionary<Guid, PersonaResuelta> personas, Guid id) =>
        personas.TryGetValue(id, out var p)
            ? new PersonaTareaDto(p.Id, p.Nombre, p.RolNombre)
            : new PersonaTareaDto(id, "Usuario desconocido", string.Empty);

    /// <summary>Cada relación guardada una sola vez se lee en ambos sentidos.</summary>
    private static Dictionary<Guid, List<Guid>> Vecinos(IEnumerable<RelacionTarea> relaciones)
    {
        var mapa = new Dictionary<Guid, List<Guid>>();
        foreach (var r in relaciones)
        {
            if (!mapa.TryGetValue(r.TareaId, out var a)) mapa[r.TareaId] = a = [];
            a.Add(r.RelacionadaId);
            if (!mapa.TryGetValue(r.RelacionadaId, out var b)) mapa[r.RelacionadaId] = b = [];
            b.Add(r.TareaId);
        }

        return mapa;
    }

    // Orden por texto canónico: coincide con el orden bytewise de `uuid` en Postgres, que es el
    // que evalúa el CHECK `tarea_id < relacionada_id` (Guid.CompareTo NO lo respeta).
    private static (Guid Menor, Guid Mayor) Par(Guid a, Guid b) =>
        string.CompareOrdinal(a.ToString(), b.ToString()) < 0 ? (a, b) : (b, a);

    internal static ExcepcionAplicacion NoEncontrada(string mensaje) =>
        new(TipoErrorAplicacion.NoEncontrado, "resource-not-found", mensaje);

    internal static ExcepcionAplicacion Validacion(string campo, string mensaje) =>
        new(TipoErrorAplicacion.Validacion, "validation", mensaje,
            new Dictionary<string, string[]> { [campo] = [mensaje] });
}
