using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Aulas.Api;
using Modules.Aulas.Domain;
using Microsoft.Extensions.Logging;
using Modules.Aulas.Repositories;
using Modules.Notificaciones.Contracts;

namespace Modules.Aulas.Services;

public interface IServicioSolicitudesAula
{
    Task<IReadOnlyList<MateriaOpcionDto>> ListarMateriasPropiasAsync(CancellationToken ct);
    Task<SolicitudReservaAulaDto> CrearAsync(CrearSolicitudReservaAulaDto datos, CancellationToken ct);
    Task<IReadOnlyList<SolicitudReservaAulaDto>> ListarMiasAsync(CancellationToken ct);
    Task<IReadOnlyList<SolicitudReservaAulaDto>> ListarTodasAsync(CancellationToken ct);
    Task<SolicitudReservaAulaDto> ObtenerDetalleAsync(Guid id, CancellationToken ct);
    Task CancelarAsync(Guid id, CancellationToken ct);
    Task<SolicitudReservaAulaDto> CancelarReservaAsync(Guid id, CancelarReservaDto datos, CancellationToken ct);
    Task<SolicitudReservaAulaDto> AsignarAulaAsync(Guid id, AsignarAulaDto datos, CancellationToken ct);
    Task<SolicitudReservaAulaDto> RechazarAsync(Guid id, RechazarSolicitudDto datos, CancellationToken ct);
}

/// <summary>
/// Ciclo de vida de <see cref="SolicitudReservaAula"/>. El ámbito (propia vs. todas) se resuelve
/// por el permiso que ya autorizó el endpoint en el controller, no acá: <see cref="ListarMiasAsync"/>
/// y <see cref="ListarTodasAsync"/> son dos métodos distintos, cada uno con su propio filtro — ver
/// design.md (decisión 6).
/// </summary>
internal sealed class ServicioSolicitudesAula(
    RepositorioSolicitudesAula repositorio,
    ICurrentUser usuarioActual,
    IConsultasIdentity identity,
    INotificador notificador,
    ILogger<ServicioSolicitudesAula> logger) : IServicioSolicitudesAula
{
    public async Task<IReadOnlyList<MateriaOpcionDto>> ListarMateriasPropiasAsync(CancellationToken ct)
    {
        var (usuarioId, _) = await ActorActualAsync(ct);
        var materias = await MateriasPropiasAsync(usuarioId, ct);
        return materias.Select(m => new MateriaOpcionDto(m.Id, m.Codigo, m.Nombre)).ToList();
    }

    public async Task<SolicitudReservaAulaDto> CrearAsync(CrearSolicitudReservaAulaDto datos, CancellationToken ct)
    {
        if (datos.Dia == default)
        {
            throw ErrorValidacion("dia", "El día del examen es obligatorio.");
        }

        if (datos.HorarioHasta <= datos.HorarioDesde)
        {
            throw ErrorValidacion("horario", "El horario hasta debe ser posterior al horario desde.");
        }

        if (datos.CantidadAlumnosAprox <= 0)
        {
            throw ErrorValidacion("cantidadAlumnosAprox", "La cantidad aproximada de alumnos debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(datos.Comision))
        {
            throw ErrorValidacion("comision", "La comisión es obligatoria.");
        }

        var (usuarioId, docenteId) = await ActorActualAsync(ct);

        // La materia no se toma de un catálogo abierto: se acota a las que el docente
        // solicitante tiene asignadas, la misma regla que Designaciones aplica al Jefe de
        // Cátedra — ver design.md (decisión 3, actualizada tras revisión).
        var materiasPropias = await MateriasPropiasAsync(usuarioId, ct);
        var materia = materiasPropias.FirstOrDefault(m => m.Id == datos.MateriaId) ?? throw ErrorValidacion(
            "materiaId", "La materia elegida no está entre tus materias asignadas.");

        var solicitud = new SolicitudReservaAula
        {
            Id = Guid.NewGuid(),
            DocenteId = docenteId,
            Dia = datos.Dia,
            HorarioDesde = datos.HorarioDesde,
            HorarioHasta = datos.HorarioHasta,
            CantidadAlumnosAprox = datos.CantidadAlumnosAprox,
            MateriaId = materia.Id,
            Comision = datos.Comision.Trim(),
            Estado = EstadosSolicitudAula.Pendiente,
            // Rol activo de la sesión; las notificaciones lo usan para elegir el destino del enlace.
            RolSolicitante = usuarioActual.Roles.FirstOrDefault(),
            CreadoEn = DateTimeOffset.UtcNow,
        };

        repositorio.Agregar(solicitud);
        await repositorio.GuardarCambiosAsync(ct);

        return Mapear(solicitud, MapearMateria(materia), esPropia: true);
    }

    public async Task<IReadOnlyList<SolicitudReservaAulaDto>> ListarMiasAsync(CancellationToken ct)
    {
        var (_, docenteId) = await ActorActualAsync(ct);
        var solicitudes = await repositorio.ListarPorDocenteAsync(docenteId, ct);
        var materias = await CatalogoMateriasAsync(ct);
        return solicitudes.Select(s => Mapear(s, ResolverMateria(materias, s.MateriaId), esPropia: true)).ToList();
    }

    public async Task<IReadOnlyList<SolicitudReservaAulaDto>> ListarTodasAsync(CancellationToken ct)
    {
        var solicitudes = await repositorio.ListarTodasAsync(ct);
        var materias = await CatalogoMateriasAsync(ct);
        var personaActual = await PersonaActualOpcionalAsync(ct);
        var personas = (await identity.ObtenerPersonasAsync(
                solicitudes.Select(s => s.DocenteId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);

        return solicitudes.Select(s => Mapear(s, ResolverMateria(materias, s.MateriaId),
            personas.TryGetValue(s.DocenteId, out var persona)
                ? new DocenteSolicitudDto(persona.Id, persona.Nombre, persona.Apellido, persona.Legajo)
                : null,
            esPropia: s.DocenteId == personaActual)).ToList();
    }

    public async Task<SolicitudReservaAulaDto> ObtenerDetalleAsync(Guid id, CancellationToken ct)
    {
        var solicitud = await repositorio.ObtenerPorIdAsync(id, ct) ?? throw NoEncontrada();
        var personaActual = await PersonaActualOpcionalAsync(ct);
        var esPropia = personaActual == solicitud.DocenteId;
        var permisos = usuarioActual.Permissions;

        // Quien no es el dueño ni resuelve solicitudes recibe el mismo 404 que una solicitud
        // inexistente: no se revela que el id existe.
        var puedeVer = permisos.Contains(Permisos.AulasAprobar)
            || (esPropia && permisos.Contains(Permisos.AulasSolicitar));
        if (!puedeVer) throw NoEncontrada();

        var materias = await CatalogoMateriasAsync(ct);
        var persona = await identity.ObtenerPersonaAsync(solicitud.DocenteId, ct);
        var docente = persona is null
            ? null
            : new DocenteSolicitudDto(persona.Id, persona.Nombre, persona.Apellido, persona.Legajo);
        return Mapear(solicitud, ResolverMateria(materias, solicitud.MateriaId), docente, esPropia);
    }

    public async Task<SolicitudReservaAulaDto> CancelarReservaAsync(
        Guid id, CancelarReservaDto datos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datos.Motivo))
        {
            throw ErrorValidacion("motivo", "Debés indicar el motivo de la cancelación.");
        }

        var solicitud = await repositorio.ObtenerPorIdAsync(id, ct) ?? throw NoEncontrada();

        if (solicitud.Estado != EstadosSolicitudAula.Aprobada)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "solicitud-no-aprobada",
                "Sólo se puede cancelar una reserva en estado Aprobada.");
        }

        // The room is released: CHECK solicitudes_reserva_aula_si_aprobada requires NULL outside Aprobada.
        // The notice names it, so it is read before it is cleared.
        var aulaLiberada = solicitud.AulaAsignada;
        solicitud.Estado = EstadosSolicitudAula.Cancelada;
        solicitud.AulaAsignada = null;
        solicitud.MotivoCancelacion = datos.Motivo.Trim();
        await repositorio.GuardarCambiosAsync(ct);

        await AvisarAlDuenoAsync(TiposEventoNotificacion.ReservaCancelada, solicitud,
            materia => AvisosAulas.ReservaCancelada(solicitud, materia, aulaLiberada, datos.Motivo.Trim()));

        var materias = await CatalogoMateriasAsync(ct);
        var personaActual = await PersonaActualOpcionalAsync(ct);
        return Mapear(
            solicitud,
            ResolverMateria(materias, solicitud.MateriaId),
            esPropia: personaActual == solicitud.DocenteId);
    }

    public async Task CancelarAsync(Guid id, CancellationToken ct)
    {
        var solicitud = await repositorio.ObtenerPorIdAsync(id, ct) ?? throw NoEncontrada();
        var (_, docenteId) = await ActorActualAsync(ct);

        if (solicitud.DocenteId != docenteId)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Prohibido,
                "solicitud-ajena",
                "No podés cancelar una solicitud de otro docente.");
        }

        if (solicitud.Estado != EstadosSolicitudAula.Pendiente)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "solicitud-no-pendiente",
                "Sólo se puede cancelar una solicitud en estado Pendiente.");
        }

        solicitud.Estado = EstadosSolicitudAula.Cancelada;
        await repositorio.GuardarCambiosAsync(ct);

        // Whoever resolves requests must know, or a room granted outside the system stays held.
        await EmisionPosterior.IntentarAsync(logger, TiposEventoNotificacion.SolicitudCanceladaDocente, async () =>
        {
            var materia = await NombreMateriaAsync(solicitud.MateriaId, CancellationToken.None);
            var docente = await identity.ObtenerPersonaAsync(solicitud.DocenteId, CancellationToken.None);
            foreach (var rol in new[] { "administrativo", "secretaria" })
            {
                await notificador.NotificarAsync(
                    AvisosAulas.SolicitudCanceladaPorDocente(solicitud, materia, docente, rol),
                    [new DestinoNotificacion.RolEnAmbito(rol)], CancellationToken.None);
            }
        });
    }

    public async Task<SolicitudReservaAulaDto> AsignarAulaAsync(Guid id, AsignarAulaDto datos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datos.AulaAsignada))
        {
            throw ErrorValidacion("aulaAsignada", "Debés indicar el aula a asignar.");
        }

        var solicitud = await repositorio.ObtenerPorIdAsync(id, ct) ?? throw NoEncontrada();

        // Pendiente: asigna y aprueba. Aprobada: sólo actualiza el aula (el Administrativo se
        // equivocó o cambió de opinión), sin reabrir el circuito. Cancelada/Rechazada: terminales,
        // se rechaza la operación.
        if (solicitud.Estado is EstadosSolicitudAula.Cancelada or EstadosSolicitudAula.Rechazada)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "solicitud-no-asignable",
                "No se puede asignar ni actualizar el aula de una solicitud cancelada o rechazada.");
        }

        var eraAprobada = solicitud.Estado == EstadosSolicitudAula.Aprobada;
        var aulaAnterior = solicitud.AulaAsignada;
        solicitud.Estado = EstadosSolicitudAula.Aprobada;
        solicitud.AulaAsignada = datos.AulaAsignada.Trim();
        await repositorio.GuardarCambiosAsync(ct);

        if (!eraAprobada)
        {
            await AvisarAlDuenoAsync(TiposEventoNotificacion.AulaAsignada, solicitud,
                materia => AvisosAulas.AulaAsignada(solicitud, materia, datos.AulaAsignada.Trim()));
        }
        else if (aulaAnterior != solicitud.AulaAsignada)
        {
            // An update that changes nothing is not worth a notice.
            await AvisarAlDuenoAsync(TiposEventoNotificacion.AulaActualizada, solicitud,
                materia => AvisosAulas.AulaActualizada(solicitud, materia, aulaAnterior ?? "", datos.AulaAsignada.Trim()));
        }

        var materias = await CatalogoMateriasAsync(ct);
        return Mapear(solicitud, ResolverMateria(materias, solicitud.MateriaId));
    }

    public async Task<SolicitudReservaAulaDto> RechazarAsync(
        Guid id, RechazarSolicitudDto datos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datos.Motivo))
        {
            throw ErrorValidacion("motivo", "Debés indicar el motivo del rechazo.");
        }

        var solicitud = await repositorio.ObtenerPorIdAsync(id, ct) ?? throw NoEncontrada();

        if (solicitud.Estado != EstadosSolicitudAula.Pendiente)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "solicitud-no-pendiente",
                "Sólo se puede rechazar una solicitud en estado Pendiente.");
        }

        solicitud.Estado = EstadosSolicitudAula.Rechazada;
        solicitud.MotivoRechazo = datos.Motivo.Trim();
        await repositorio.GuardarCambiosAsync(ct);

        await AvisarAlDuenoAsync(TiposEventoNotificacion.SolicitudRechazada, solicitud,
            materia => AvisosAulas.SolicitudRechazada(solicitud, materia, datos.Motivo.Trim()));

        var materias = await CatalogoMateriasAsync(ct);
        return Mapear(solicitud, ResolverMateria(materias, solicitud.MateriaId));
    }

    /// <summary>
    /// Notifies the teacher who owns the request, after the change committed. Best effort: building
    /// the notice (it needs the materia name) and delivering it can fail without touching the action.
    /// </summary>
    private Task AvisarAlDuenoAsync(string evento, SolicitudReservaAula solicitud, Func<string, AvisoNuevo> construir) =>
        EmisionPosterior.IntentarAsync(logger, evento, async () =>
        {
            var materia = await NombreMateriaAsync(solicitud.MateriaId, CancellationToken.None);
            await notificador.NotificarAsync(
                construir(materia), [new DestinoNotificacion.Persona(solicitud.DocenteId)], CancellationToken.None);
        });

    private async Task<string> NombreMateriaAsync(Guid materiaId, CancellationToken ct) =>
        ResolverMateria(await CatalogoMateriasAsync(ct), materiaId).Nombre;

    private async Task<(Guid UsuarioId, Guid PersonaId)> ActorActualAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(usuarioActual.UserId, out var usuarioId))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoAutenticado, "unauthenticated", "Se requiere autenticación.");
        }

        var personaId = (await identity.ObtenerUsuarioAsync(usuarioId, ct))?.PersonaId;

        return personaId is null
            ? throw new ExcepcionAplicacion(
                TipoErrorAplicacion.NoEncontrado,
                "persona-not-found",
                "No se encontró la persona asociada al usuario autenticado.")
            : (usuarioId, personaId.Value);
    }

    private async Task<Guid?> PersonaActualOpcionalAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(usuarioActual.UserId, out var usuarioId)) return null;
        var usuarios = await identity.ListarUsuariosAsync(ct);
        return usuarios.FirstOrDefault(u => u.Id == usuarioId)?.PersonaId;
    }

    /// <summary>
    /// Materias asignadas al docente en su rol activo (el que trae la sesión). No filtra por
    /// "jefe_catedra" a diferencia de <c>ResolutorActor</c> de Designaciones: acá cualquier rol con
    /// <c>aulas.solicitar</c> (Docente o Jefe de Cátedra) puede tener materias asignadas en
    /// <c>identity.user_roles</c>, y todas cuentan.
    /// </summary>
    private async Task<IReadOnlyList<Materia>> MateriasPropiasAsync(Guid usuarioId, CancellationToken ct)
    {
        var rolActivo = usuarioActual.Roles.FirstOrDefault();
        if (rolActivo is null) return [];

        var materiaIds = await identity.ObtenerMateriasDeRolAsync(usuarioId, rolActivo, ct);
        if (materiaIds.Count == 0) return [];

        var catalogo = await identity.ListarMateriasAsync(ct);
        return catalogo.Where(m => materiaIds.Contains(m.Id)).ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, Materia>> CatalogoMateriasAsync(CancellationToken ct) =>
        (await identity.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);

    private static MateriaOpcionDto ResolverMateria(IReadOnlyDictionary<Guid, Materia> catalogo, Guid materiaId) =>
        catalogo.TryGetValue(materiaId, out var materia)
            ? MapearMateria(materia)
            : new MateriaOpcionDto(materiaId, "", "(materia no encontrada)");

    private static MateriaOpcionDto MapearMateria(Materia materia) =>
        new(materia.Id, materia.Codigo, materia.Nombre);

    private static ExcepcionAplicacion ErrorValidacion(string campo, string mensaje) =>
        new(TipoErrorAplicacion.Validacion, "solicitud-invalida", mensaje,
            new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static ExcepcionAplicacion NoEncontrada() =>
        new(TipoErrorAplicacion.NoEncontrado, "solicitud-not-found", "No se encontró la solicitud.");

    private static SolicitudReservaAulaDto Mapear(
        SolicitudReservaAula s, MateriaOpcionDto materia, DocenteSolicitudDto? docente = null,
        bool esPropia = false) =>
        new(s.Id, s.Dia, s.HorarioDesde, s.HorarioHasta, s.CantidadAlumnosAprox, materia, s.Comision,
            s.Estado, s.AulaAsignada, s.MotivoRechazo, s.CreadoEn, docente, s.MotivoCancelacion, esPropia);
}
