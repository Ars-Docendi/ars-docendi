namespace Modules.Aulas.Domain;

/// <summary>
/// Solicitud de reserva de aula o laboratorio para una mesa de examen.
/// <para>
/// <see cref="DocenteId"/> apunta a <c>identity.personas</c> con FK real en la base,
/// pero sin navegación en este contexto: identity lo modela <c>IdentityDbContext</c>,
/// y mezclar entidades de dos contextos acopla los módulos.
/// </para>
/// <para>
/// <see cref="MateriaId"/> apunta a <c>identity.materias</c> (catálogo transversal, no de
/// Designaciones) con FK real en la base, sin navegación acá por la misma razón que
/// <see cref="DocenteId"/>. El backend la acota a las materias que el docente solicitante
/// tiene asignadas — ver <see cref="Services.ServicioSolicitudesAula"/> y design.md
/// (decisión 3) del change reserva-aulas. <see cref="Comision"/> sigue siendo texto libre:
/// no existe catálogo de comisiones.
/// </para>
/// </summary>
public sealed class SolicitudReservaAula
{
    public Guid Id { get; set; }
    public Guid DocenteId { get; set; }

    public DateOnly Dia { get; set; }
    public TimeOnly HorarioDesde { get; set; }
    public TimeOnly HorarioHasta { get; set; }
    public int CantidadAlumnosAprox { get; set; }
    public Guid MateriaId { get; set; }
    public required string Comision { get; set; }

    public required string Estado { get; set; }
    /// <summary>Sólo con <c>Estado == Aprobada</c>: identificador de aula en texto libre.</summary>
    public string? AulaAsignada { get; set; }
    /// <summary>Sólo con <c>Estado == Rechazada</c>: motivo ingresado por el Administrativo.</summary>
    public string? MotivoRechazo { get; set; }

    public DateTimeOffset CreadoEn { get; set; }
}

/// <summary>Estados de la solicitud. Coincide con el CHECK <c>solicitudes_reserva_estado_valido</c>.</summary>
public static class EstadosSolicitudAula
{
    public const string Pendiente = "pendiente";
    public const string Aprobada = "aprobada";
    public const string Rechazada = "rechazada";
    public const string Cancelada = "cancelada";
}
