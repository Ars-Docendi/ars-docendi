using ArsDocendi.Shared.Aplicacion;

namespace Modules.Tareas.Domain;

/// <summary>
/// Reglas de una tarea: quién puede hacer qué y qué exige cada transición. Sin I/O:
/// valida los guards y muta la tarea recibida (la que el servicio ya tiene cargada),
/// o lanza <see cref="ExcepcionAplicacion"/> — <c>Prohibido</c> (403) si el actor no
/// puede, <c>ReglaDeNegocio</c> (422) si la regla no se cumple. Espejo del
/// <c>maquinaEstadosTarea.ts</c> que el frontend usa para anticipar los mismos guards;
/// la fuente de verdad es esta.
/// </summary>
public static class MaquinaEstadosTarea
{
    // El Administrador de Sistemas puede todo: se lo trata como autoridad creadora y como
    // Responsable de cualquier tarea.
    private static bool EsAdministrador(ActorTareas actor) =>
        actor.Roles.Contains(JerarquiaAsignacion.Administrador);

    public static bool EsAutoridadCreadora(Tarea tarea, ActorTareas actor) =>
        tarea.CreadoPorId == actor.UsuarioId || EsAdministrador(actor);

    public static bool EsResponsable(Tarea tarea, ActorTareas actor) =>
        tarea.ResponsableId == actor.UsuarioId || EsAdministrador(actor);

    /// <summary>Título, Descripción, fechas, Prioridad, Tipo, Responsable y Proyecto: solo la autoridad creadora.</summary>
    public static void RequerirPuedeEditarCampos(Tarea tarea, ActorTareas actor)
    {
        if (!EsAutoridadCreadora(tarea, actor))
        {
            throw Prohibido("Solo la autoridad creadora puede editar los campos de la tarea.");
        }
    }

    public static void ValidarFechas(DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "La Fecha de Fin debe ser posterior o igual a la Fecha de Inicio.",
                new Dictionary<string, string[]> { ["fechaFin"] = ["Debe ser posterior o igual a la Fecha de Inicio."] });
        }
    }

    /// <summary>
    /// Cancelar es exclusivo de la autoridad creadora y solo desde un estado no terminal.
    /// Cualquier otro destino lo mueve libremente el Responsable; desde un estado terminal
    /// (resuelta/cancelada) solo la autoridad creadora puede reabrir.
    /// Pausa exige un comentario (queda en el hilo) y Resuelta exige la Solución.
    /// </summary>
    public static void CambiarEstado(
        Tarea tarea, ActorTareas actor, EstadoTareaCatalogo actual, EstadoTareaCatalogo destinoEstado,
        string? comentario, string? solucion)
    {
        var destino = destinoEstado.Codigo;
        if (destino == EstadosTarea.Cancelada)
        {
            if (!EsAutoridadCreadora(tarea, actor) || EstadosTarea.EsTerminal(actual.Codigo))
            {
                throw Prohibido("Solo la autoridad creadora puede cancelar la tarea, y solo si no está cerrada.");
            }
        }
        else if (EstadosTarea.EsTerminal(actual.Codigo))
        {
            if (!EsAutoridadCreadora(tarea, actor))
            {
                throw Prohibido($"El estado \"{actual.Codigo}\" es terminal: solo la autoridad creadora puede reabrir la tarea.");
            }
        }
        else if (!EsResponsable(tarea, actor) && !EsAutoridadCreadora(tarea, actor))
        {
            throw Prohibido("Solo el Responsable o la autoridad creadora pueden cambiar el estado de la tarea.");
        }

        if (destinoEstado.Id == actual.Id)
        {
            throw Regla($"La tarea ya está en estado \"{destino}\".");
        }

        var ahora = DateTimeOffset.UtcNow;
        string? detalle = null;

        if (destino == EstadosTarea.Pausa)
        {
            var motivo = Requerido(comentario, "Pasar a Pausa exige un comentario con el motivo de la consulta.");
            tarea.Comentarios.Add(NuevoComentario(tarea, actor, motivo, ahora));
        }

        if (destino == EstadosTarea.Resuelta)
        {
            tarea.Solucion = Requerido(solucion, "Pasar a Resuelta exige completar el campo Solución.");
            detalle = tarea.Solucion;
        }

        tarea.EstadoId = destinoEstado.Id;
        Registrar(tarea, actor, AccionesHistorial.CambiarEstado, detalle, ahora);
    }

    /// <summary>El % de avance (0-100) lo completa el Responsable, o la autoridad creadora.</summary>
    public static void EditarAvance(Tarea tarea, ActorTareas actor, int porcentaje)
    {
        if (!EsResponsable(tarea, actor) && !EsAutoridadCreadora(tarea, actor))
        {
            throw Prohibido("Solo el Responsable o la autoridad creadora editan el avance.");
        }

        if (porcentaje is < 0 or > 100)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "El porcentaje de avance debe estar entre 0 y 100.",
                new Dictionary<string, string[]> { ["porcentajeAvance"] = ["Debe estar entre 0 y 100."] });
        }

        tarea.PorcentajeAvance = (short)porcentaje;
        Registrar(tarea, actor, AccionesHistorial.EditarAvance, $"{porcentaje}%", DateTimeOffset.UtcNow);
    }

    public static ComentarioTarea NuevoComentario(Tarea tarea, ActorTareas actor, string texto, DateTimeOffset ahora) =>
        new()
        {
            Id = Guid.NewGuid(),
            TareaId = tarea.Id,
            AutorId = actor.UsuarioId,
            AutorRol = actor.RolNombre,
            Texto = texto.Trim(),
            CreadoEn = ahora,
        };

    public static void Registrar(
        Tarea tarea, ActorTareas actor, string accion, string? detalle, DateTimeOffset ahora) =>
        tarea.Historial.Add(new EventoTarea
        {
            Id = Guid.NewGuid(),
            TareaId = tarea.Id,
            Accion = accion,
            ActorId = actor.UsuarioId,
            ActorRol = actor.RolNombre,
            EstadoId = tarea.EstadoId,
            Detalle = detalle,
            CreadoEn = ahora,
        });

    private static string Requerido(string? texto, string mensaje) =>
        string.IsNullOrWhiteSpace(texto) ? throw Regla(mensaje) : texto.Trim();

    private static ExcepcionAplicacion Prohibido(string mensaje) =>
        new(TipoErrorAplicacion.Prohibido, "tarea-prohibida", mensaje);

    private static ExcepcionAplicacion Regla(string mensaje) =>
        new(TipoErrorAplicacion.ReglaDeNegocio, "tarea-regla-invalida", mensaje);
}
