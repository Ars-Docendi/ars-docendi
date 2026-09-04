namespace Modules.Designaciones.Domain;

/// <summary>Catálogo único de cargos docentes. <c>Orden</c> registra la jerarquía (1 = mayor).</summary>
public sealed class Cargo
{
    public Guid Id { get; set; }
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public required string Abreviatura { get; set; }
    public short Orden { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

/// <summary>
/// Período de designación. <see cref="CargaHasta"/> es un límite blando: pasada esa
/// fecha se sigue permitiendo cargar, porque el cierre real es manual vía
/// <see cref="Activo"/>. A lo sumo un período activo a la vez (lo garantiza la base).
/// </summary>
public sealed class Periodo
{
    public Guid Id { get; set; }
    public required string Nombre { get; set; }
    public DateOnly CargaDesde { get; set; }
    public DateOnly CargaHasta { get; set; }
    public DateOnly ImpactoDesde { get; set; }
    public DateOnly ImpactoHasta { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public uint Version { get; set; }
}

/// <summary>
/// Fotografía de los datos vigentes del docente al momento de ENVIAR el pedido a
/// revisión. Null mientras el pedido está en borrador: recién ahí se congela.
/// <para>
/// Es lo que le da valor probatorio al trámite. Un pedido aprobado en Decanato tres
/// meses después tiene que seguir diciendo qué cargo tenía el docente el día que se
/// cargó; si se recalculara, el documento reescribiría su propio pasado.
/// </para>
/// </summary>
public sealed record SnapshotPedido(
    string? Cargo,
    string? Dedicacion,
    int? Horas,
    string? Materia,
    int? HorasInvestigacion,
    int? HorasExternas);

/// <summary>
/// El trámite. Cubre EXACTAMENTE UNA materia — la cátedra sobre la que opera el
/// Jefe de Cátedra —, de modo que la carrera se deriva de la materia y resuelve un
/// único Coordinador competente (BR-designaciones-009).
/// <para>
/// <see cref="PersonaId"/> y <see cref="MateriaId"/> apuntan a <c>identity</c> con FK
/// real en la base, pero sin navegación en este contexto: identity lo modela
/// <c>IdentityDbContext</c>, y mezclar entidades de dos contextos acopla los módulos.
/// </para>
/// </summary>
public sealed class Pedido
{
    public Guid Id { get; set; }
    /// <summary>Número de trámite legible. Lo asigna el backend al persistir.</summary>
    public required string Numero { get; set; }
    public Guid PeriodoId { get; set; }
    public Guid PersonaId { get; set; }
    /// <summary>La cátedra del pedido. Determina la carrera y el ámbito del revisor.</summary>
    public Guid MateriaId { get; set; }

    public required string Novedad { get; set; }
    public required string Estado { get; set; }
    public bool Prioritario { get; set; }

    public Guid? CargoSolicitadoId { get; set; }
    public string? DedicacionSolicitada { get; set; }
    public int? Horas { get; set; }

    /// <summary>Del docente, no de la materia (así lo define la spec vigente).</summary>
    public int? HorasInvestigacion { get; set; }
    public int? HorasExternas { get; set; }

    public string? Justificacion { get; set; }
    public string? TipoBaja { get; set; }
    public string? TipoBajaDetalle { get; set; }

    /// <summary>Sólo con <c>Estado == devuelto</c>: etapa a la que vuelve al reenviar.</summary>
    public string? EtapaRetorno { get; set; }
    /// <summary>Sólo con <c>Estado == devuelto</c>: quién debe corregir.</summary>
    public string? PropietarioActual { get; set; }

    public SnapshotPedido? Snapshot { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public uint Version { get; set; }

    public Periodo? Periodo { get; set; }
    public Cargo? CargoSolicitado { get; set; }
    public ICollection<PedidoAdjunto> Adjuntos { get; set; } = [];
    public ICollection<PedidoHistorial> Historial { get; set; } = [];

    /// <summary>Un pedido en estado terminal no admite ninguna acción [BR-designaciones-011].</summary>
    public bool EsTerminal => EstadosPedido.Terminales.Contains(Estado);
}

/// <summary>Documentación respaldatoria. Qué adjuntos son obligatorios lo decide la novedad.</summary>
public sealed class PedidoAdjunto
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public required string Tipo { get; set; }
    public required string Nombre { get; set; }
    public string? Uri { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Pedido? Pedido { get; set; }
}

/// <summary>
/// Evento del historial del trámite. Es dato de dominio, no metadata de auditoría:
/// <see cref="RolId"/> no es derivable de <c>audit.change_log</c> (un usuario puede
/// tener varios roles) y <see cref="Comentario"/> lo exige BR-designaciones-005.
/// </summary>
public sealed class PedidoHistorial
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public required string Accion { get; set; }
    /// <summary>Con qué rol actuó el actor. Explícito, no inferido.</summary>
    public Guid RolId { get; set; }
    public Guid? ActorId { get; set; }
    /// <summary>Estado del pedido al momento de registrar el evento.</summary>
    public required string Etapa { get; set; }
    /// <summary>Justificativo (rechazo) / comentario (devolución) / motivo (prioridad).</summary>
    public string? Comentario { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Pedido? Pedido { get; set; }
}

/// <summary>
/// El estado vigente: qué cargo y cuántas horas tiene hoy un docente en una materia.
/// Es la contracara del pedido — mutable, con vigencia, dice "esto es cierto hoy".
/// <para>
/// <see cref="OrigenPedidoId"/> en null significa carga administrativa directa desde
/// la pantalla de docentes; con valor, es producto de un pedido aprobado. Sin esa
/// distinción la trazabilidad no puede separar los dos caminos de escritura.
/// </para>
/// </summary>
public sealed class Designacion
{
    public Guid Id { get; set; }
    public Guid PersonaId { get; set; }
    public Guid MateriaId { get; set; }
    public Guid CargoId { get; set; }
    public string? Dedicacion { get; set; }
    public int Horas { get; set; }
    public DateOnly VigenteDesde { get; set; }
    /// <summary>Null = vigente. Cerrar una designación es fijar esta fecha, no borrar la fila.</summary>
    public DateOnly? VigenteHasta { get; set; }
    public Guid? OrigenPedidoId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Cargo? Cargo { get; set; }

    public bool EstaVigente => VigenteHasta is null;
}

/// <summary>Respuesta confirmada de un comando con clave de idempotencia.</summary>
public sealed class ComandoIdempotente
{
    public Guid Id { get; set; }
    public Guid Clave { get; set; }
    public Guid ActorId { get; set; }
    public required string Ruta { get; set; }
    public Guid PedidoId { get; set; }
    public required string RequestHash { get; set; }
    public int StatusCode { get; set; }
    public required string ResponseBody { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
