using System.ComponentModel.DataAnnotations;
using Modules.Asistente.Application;

namespace Modules.Asistente.Api;

/// <summary>Lo que el cliente manda en un turno.</summary>
public sealed record ConsultaDelAsistente(
    // Las anotaciones van en el PARÁMETRO y no en la propiedad. Con `[property: ...]` sobre un
    // parámetro del constructor primario, ASP.NET Core levanta excepción al validar el modelo —no
    // las ignora en silencio— y todo request al endpoint termina en 500.
    [Required(AllowEmptyStrings = false)]
    [MaxLength(2000)]
    string Mensaje,
    Guid? Hilo,
    // Sin `[Required]`: ausente es «turno nuevo cualquiera», el caso de siempre.
    string? Reemplaza = null,
    // `[MaxLength(5)]` sobre una colección cuenta elementos, no caracteres: con `[ApiController]`,
    // una sexta referencia nunca llega a la acción — el filtro de validación del modelo devuelve
    // 400 antes (escenario «Too many references are rejected» de asistente-menciones).
    [MaxLength(5)]
    IReadOnlyList<ReferenciaDto>? Referencias = null);

/// <summary>Una mención tal como la manda el cliente en el turno (design.md D11).</summary>
public sealed record ReferenciaDto(string Tipo, Guid Id);

/// <summary>Una opción del menú de aclaración.</summary>
public sealed record OpcionDto(string Etiqueta, string PreguntaResuelta);

/// <summary>
/// Una materia o un docente encontrado por <c>GET /api/asistente/menciones</c> (design.md D10 de
/// asistente-rediseno-v3).
/// </summary>
public sealed record MencionDto(Guid Id, string Nombre, string? Carrera, string? Codigo, string? Cargo)
{
    internal static MencionDto De(ResultadoDeMencion resultado) => new(
        resultado.Id, resultado.Nombre, resultado.Carrera, resultado.Codigo, resultado.Cargo);
}

/// <summary>La respuesta de <c>GET /api/asistente/menciones</c>.</summary>
public sealed record MencionesDto(IReadOnlyList<MencionDto> Resultados, bool HayMas)
{
    internal static MencionesDto De(BusquedaDeMenciones busqueda) =>
        new([.. busqueda.Resultados.Select(MencionDto.De)], busqueda.HayMas);
}

/// <summary>What the client sends to rate an already-answered turn.</summary>
public sealed record PedidoDeRetroalimentacion(
    Guid Token, bool Voto, IReadOnlyList<string>? Razones, string? Comentario);

/// <summary>Un vínculo a la pantalla que muestra lo que una celda identifica.</summary>
public sealed record VinculoDto(int Fila, int Columna, string Tipo, string Id);

/// <summary>
/// Dónde, dentro del texto de la respuesta, aparece un valor que el resultado ya vinculó
/// (<c>vinculosDelTexto</c>).
/// </summary>
public sealed record VinculoDelTextoDto(int Desde, int Largo, string Tipo, string Id);

/// <summary>El elemento sobre el que trata la respuesta, con su ficha (<c>principal</c>).</summary>
public sealed record PrincipalDto(string Tipo, string Id, IReadOnlyDictionary<string, string?> Datos);

/// <summary>Una columna del resultado, con su marca de sensibilidad.</summary>
public sealed record ColumnaDto(string Nombre, bool Sensible);

/// <summary>Lo que el cliente recibe de un turno (§4.6).</summary>
public sealed record RespuestaDelAsistente
{
    /// <summary>Uno de los cuatro: respondida, no contestable, necesita aclaración, degradado.</summary>
    public required string Estado { get; init; }

    /// <summary>El texto que lee el usuario.</summary>
    public required string Respuesta { get; init; }

    /// <summary>El hilo, para mandarlo en el turno siguiente.</summary>
    public required Guid Hilo { get; init; }

    /// <summary>Cómo se interpretó la pregunta. Presente solo si difiere del mensaje.</summary>
    public string? PreguntaInterpretada { get; init; }

    /// <summary>Cómo el asistente llegó a la consulta, tal como lo devolvió la generación.</summary>
    public string? Razonamiento { get; init; }

    /// <summary>Las opciones de una aclaración. <b>Bloquean</b> el turno.</summary>
    public IReadOnlyList<OpcionDto> Opciones { get; init; } = [];

    /// <summary>Las columnas del resultado, con su marca de sensibilidad.</summary>
    public IReadOnlyList<ColumnaDto> Columnas { get; init; } = [];

    /// <summary>Las filas, con los valores reales.</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Filas { get; init; } = [];

    /// <summary>
    /// Si hubo más filas que el tope. Booleano y nunca un conteo: cuántas quedaron
    /// afuera es un canal de inferencia sobre datos que el usuario no puede ver.
    /// </summary>
    public bool Truncado { get; init; }

    /// <summary>La consulta que se ejecutó. Presente solo con el permiso correspondiente.</summary>
    public string? Sql { get; init; }

    /// <summary>The feedback token: present only when <c>estado</c> is <c>respondida</c>.</summary>
    public Guid? ClaveDeRetroalimentacion { get; init; }

    /// <summary>
    /// Las celdas que identifican algo que el actor <b>puede abrir</b>. Vacío si no hay ninguna.
    /// </summary>
    public IReadOnlyList<VinculoDto> Vinculos { get; init; } = [];

    /// <summary>
    /// Las apariciones, dentro de <c>respuesta</c>, de los valores de celda que tienen vínculo.
    /// </summary>
    public IReadOnlyList<VinculoDelTextoDto> VinculosDelTexto { get; init; } = [];

    /// <summary>
    /// El elemento sobre el que trata la respuesta, con su ficha. Nulo salvo que el resultado sea
    /// una sola fila con un solo vínculo y el módulo dueño haya entregado la ficha.
    /// </summary>
    public PrincipalDto? Principal { get; init; }

    /// <summary>
    /// De qué áreas salen los datos: códigos de schema (<c>designaciones</c>, <c>identity</c>,
    /// <c>portal</c>), sin repetir y en orden estable.
    /// </summary>
    public IReadOnlyList<string> Origenes { get; init; } = [];

    /// <summary>Lo que costó el turno.</summary>
    public required MetricasDto Metricas { get; init; }

    /// <summary>El cupo diario del actor, ya cobrado este turno (asistente-cupo-visible).</summary>
    public int? CupoRestante { get; init; }

    /// <summary>
    /// El id de la conversación persistida (<c>asistente.hilo_historico.id</c>) en la que este
    /// turno quedó registrado (design.md D13 de asistente-rediseno-v3), para que el rail la resalte
    /// y titule el encabezado.
    /// </summary>
    public Guid? Conversacion { get; init; }

    /// <summary>Arma la respuesta HTTP a partir del resultado del turno.</summary>
    internal static RespuestaDelAsistente De(ResultadoDelTurno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return new RespuestaDelAsistente
        {
            Estado = Nombrar(turno.Estado),
            Respuesta = turno.Respuesta,
            Hilo = turno.Hilo,
            PreguntaInterpretada = turno.PreguntaInterpretada,
            Razonamiento = string.IsNullOrWhiteSpace(turno.Razonamiento) ? null : turno.Razonamiento,
            Opciones = [.. (turno.Opciones ?? []).Select(o => new OpcionDto(o.Etiqueta, o.PreguntaResuelta))],
            Columnas = [.. turno.Columnas.Select((nombre, i) =>
                new ColumnaDto(nombre, turno.Sensibilidad.Count > i && turno.Sensibilidad[i].Tapa))],
            Filas = turno.Filas,
            Truncado = turno.Truncado,
            Sql = turno.Sql,
            Vinculos = [.. (turno.Vinculos ?? []).Select(
                v => new VinculoDto(v.Fila, v.Columna, v.Tipo, v.Id))],
            VinculosDelTexto = [.. (turno.VinculosDelTexto ?? []).Select(
                v => new VinculoDelTextoDto(v.Desde, v.Largo, v.Tipo, v.Id))],
            Principal = turno.Principal is { } principal
                ? new PrincipalDto(principal.Tipo, principal.Id, principal.Datos)
                : null,
            Origenes = [.. turno.Origenes ?? []],
            Metricas = new MetricasDto(turno.LlamadasAlModelo, turno.Categoria),
            ClaveDeRetroalimentacion = turno.ClaveDeRetroalimentacion,
            CupoRestante = turno.CupoRestante,
            Conversacion = turno.Conversacion,
        };
    }

    /// <summary>Traduce el estado a la forma del contrato HTTP.</summary>
    private static string Nombrar(EstadoDelTurno estado) => estado switch
    {
        EstadoDelTurno.Respondida => "respondida",
        EstadoDelTurno.NoContestable => "no_contestable",
        EstadoDelTurno.NecesitaAclaracion => "necesita_aclaracion",
        EstadoDelTurno.ServicioDegradado => "servicio_degradado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado desconocido."),
    };
}

/// <summary>Lo que costó el turno.</summary>
public sealed record MetricasDto(int LlamadasAlModelo, string Categoria);

/// <summary>
/// Lo que se manda a <c>PATCH /api/asistente/administracion/mantenimiento</c>
/// (asistente-modo-mantenimiento).
/// </summary>
public sealed record PedidoDeMantenimientoDto(bool Activo, string? Razon);

/// <summary>El modo mantenimiento, tal como lo ve cualquier consultante.</summary>
public sealed record MantenimientoDto(bool Activo, string? Razon)
{
    internal static MantenimientoDto De(EstadoDeMantenimiento estado) =>
        new(estado.Activo, estado.Razon);
}

/// <summary>
/// El cupo diario del actor, tal como lo cuenta <c>capacidades</c> y el resultado del propio turno
/// (asistente-cupo-visible).
/// </summary>
public sealed record CupoDto(int Restante, bool Bloqueado, string? Motivo, DateTimeOffset? VuelveA);

/// <summary>
/// Un agregado de uso: por usuario, por rol, u organizacional (asistente-panel-de-uso).
/// </summary>
public sealed record UsoAgregadoDto(
    string Clave,
    string? NombreParaMostrar,
    int Turnos,
    IReadOnlyDictionary<string, int> PorEstado,
    int LlamadasAlModelo,
    long TokensDeEntrada,
    long TokensDeSalida,
    long TokensDeCache,
    double LatenciaPromedioMs,
    double LatenciaP95Ms,
    IReadOnlyList<string> Proveedores,
    decimal CostoEstimado,
    bool EsEstimado,
    int TurnosSinPrecio,
    IReadOnlyList<string> CodigosDeRol,
    int? CupoEfectivo,
    string? OrigenDeCupo,
    bool? AccesoEfectivo,
    string? OrigenDeAcceso)
{
    internal static UsoAgregadoDto De(UsoAgregado agregado) => new(
        agregado.Clave,
        agregado.NombreParaMostrar,
        agregado.Turnos,
        agregado.PorEstado,
        agregado.LlamadasAlModelo,
        agregado.TokensDeEntrada,
        agregado.TokensDeSalida,
        agregado.TokensDeCache,
        agregado.LatenciaPromedioMs,
        agregado.LatenciaP95Ms,
        agregado.Proveedores,
        agregado.CostoEstimado,
        EsEstimado: true,
        agregado.TurnosSinPrecio,
        agregado.CodigosDeRol,
        CupoEfectivo: null,
        OrigenDeCupo: null,
        AccesoEfectivo: null,
        OrigenDeAcceso: null);

    /// <summary>
    /// Como <see cref="De"/>, pero además resuelve <see cref="CupoEfectivo"/> y <see
    /// cref="OrigenDeCupo"/> con <see cref="ReglaDeCupoEfectivo"/> — sólo tiene sentido para una
    /// fila de <c>PorUsuario</c>: una fila de rol o la de organización ya ES su propio cupo, sin
    /// necesidad de resolver nada (se lee directo de <c>GET …/presupuestos</c>).
    /// </summary>
    internal static UsoAgregadoDto DeUsuario(UsoAgregado agregado, EstadoDePresupuestos presupuestos)
    {
        var esActor = Guid.TryParse(agregado.Clave, out var actorId);
        var overrideDeUsuario = esActor
            ? presupuestos.OverridesPorUsuario.FirstOrDefault(o => o.ActorId == actorId)?.CupoDiarioTurnos
            : null;

        var cuposPorRol = presupuestos.CuposPorRol
            .ToDictionary(c => c.Rol, c => c.CupoDiarioTurnos, StringComparer.Ordinal);

        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(overrideDeUsuario, agregado.CodigosDeRol, cuposPorRol);

        var accesoPorRol = presupuestos.CuposPorRol
            .ToDictionary(c => c.Rol, c => c.AccesoHabilitado, StringComparer.Ordinal);
        var revocado = esActor && presupuestos.AccesosRevocados.Contains(actorId);
        var (acceso, origenDeAcceso) = ReglaDeAccesoEfectivo.Resolver(revocado, agregado.CodigosDeRol, accesoPorRol);

        return De(agregado) with
        {
            CupoEfectivo = cupo,
            OrigenDeCupo = origen,
            AccesoEfectivo = acceso,
            OrigenDeAcceso = origenDeAcceso,
        };
    }
}

/// <summary>El panel de uso completo (<c>GET /api/asistente/administracion/uso</c>).</summary>
public sealed record UsoDto(
    IReadOnlyList<UsoAgregadoDto> PorUsuario,
    IReadOnlyList<UsoAgregadoDto> PorRol,
    UsoAgregadoDto Organizacion)
{
    internal static UsoDto De(PanelDeUso panel, EstadoDePresupuestos presupuestos) => new(
        [.. panel.PorUsuario.Select(a => UsoAgregadoDto.DeUsuario(a, presupuestos))],
        [.. panel.PorRol.Select(UsoAgregadoDto.De)],
        UsoAgregadoDto.De(panel.Organizacion));
}

/// <summary>Lo que se manda a editar un cupo (de rol o de usuario).</summary>
public sealed record PedidoDeCupoDto(int Cupo);

/// <summary>
/// Lo que se manda a cambiar el acceso de un rol o de un usuario (asistente-acceso-granular).
/// </summary>
public sealed record PedidoDeAccesoDto(bool Habilitado);

/// <summary>Lo que se manda a editar el tope organizacional.</summary>
public sealed record PedidoDeTopeDto(decimal TopeMensualUsd);

/// <summary>
/// El cupo diario default de UN rol y su acceso operativo, tal como están
/// persistidos (tarea 12.8; acceso: asistente-acceso-granular).
/// </summary>
public sealed record CupoDeRolDto(string Rol, int CupoDiarioTurnos, bool AccesoHabilitado)
{
    internal static CupoDeRolDto De(CupoDeRolVigente cupo) =>
        new(cupo.Rol, cupo.CupoDiarioTurnos, cupo.AccesoHabilitado);
}

/// <summary>El override de cupo diario de un usuario puntual, tal como está persistido (tarea 12.8).</summary>
public sealed record OverrideDeUsuarioDto(Guid ActorId, string? NombreParaMostrar, int CupoDiarioTurnos)
{
    internal static OverrideDeUsuarioDto De(OverrideDeUsuarioVigente over) =>
        new(over.ActorId, over.NombreParaMostrar, over.CupoDiarioTurnos);
}

/// <summary>
/// El estado persistido de los presupuestos (<c>GET /api/asistente/administracion/presupuestos</c>,
/// tarea 12.8 de sistema-seccion-unificada).
/// </summary>
public sealed record PresupuestosDto(
    decimal TopeMensualUsd,
    decimal GastoEstimadoDelMes,
    bool EsEstimado,
    IReadOnlyList<CupoDeRolDto> CuposPorRol,
    IReadOnlyList<OverrideDeUsuarioDto> OverridesPorUsuario,
    IReadOnlyList<Guid> AccesosRevocados)
{
    internal static PresupuestosDto De(EstadoDePresupuestos estado, decimal gastoEstimadoDelMes) => new(
        estado.TopeMensualUsd,
        gastoEstimadoDelMes,
        EsEstimado: true,
        [.. estado.CuposPorRol.Select(CupoDeRolDto.De)],
        [.. estado.OverridesPorUsuario.Select(OverrideDeUsuarioDto.De)],
        estado.AccesosRevocados);
}

/// <summary>
/// La carga del servidor del modelo propio y de la compuerta (<c>GET
/// /api/asistente/administracion/servidor-local</c>, asistente-optimizaciones- modelo-local D8).
/// </summary>
public sealed record ServidorLocalDto(
    bool Configurado,
    bool Alcanzable,
    string? Motor,
    int? EnCurso,
    int? EnEspera,
    double? UsoDeKvCache,
    double? AciertosDeCacheDePrefijo,
    CompuertaDto? Compuerta)
{
    internal static ServidorLocalDto De(Infrastructure.EstadoDelServidorLocal estado) => new(
        estado.Configurado,
        estado.Alcanzable,
        estado.Motor,
        estado.EnCurso,
        estado.EnEspera,
        estado.UsoDeKvCache,
        estado.AciertosDeCacheDePrefijo,
        estado.Compuerta is { } c ? new CompuertaDto(c.Capacidad, c.EnCurso, c.EnEspera) : null);
}

/// <summary>La compuerta de concurrencia del backend hacia el modelo.</summary>
public sealed record CompuertaDto(int Capacidad, int EnCurso, int EnEspera);

/// <summary>Un área que el actor puede consultar.</summary>
public sealed record AreaDto(string Nombre, string? Descripcion, int Columnas);

/// <summary>El catálogo de capacidades del actor.</summary>
public sealed record CapacidadesDto
{
    /// <summary>Las áreas, con sus conteos.</summary>
    public required IReadOnlyList<AreaDto> Cubre { get; init; }

    /// <summary>Cuántas tablas puede consultar.</summary>
    public required int Tablas { get; init; }

    /// <summary>Cuántas columnas puede leer.</summary>
    public required int Columnas { get; init; }

    /// <summary>Preguntas ejecutables, verificadas contra sus privilegios.</summary>
    public required IReadOnlyList<string> Ejemplos { get; init; }

    /// <summary>Los límites del asistente.</summary>
    public required IReadOnlyList<string> NoPuede { get; init; }

    /// <summary>Qué filas ve, dicho aparte de los conteos.</summary>
    public required string Alcance { get; init; }

    /// <summary>Por qué cosas suele venir a preguntar este actor, según su rol.</summary>
    public required string Presentacion { get; init; }

    /// <summary>El modo mantenimiento, global (asistente-modo-mantenimiento).</summary>
    public required MantenimientoDto Mantenimiento { get; init; }

    /// <summary>El cupo diario de este actor (asistente-cupo-visible).</summary>
    public required CupoDto Cupo { get; init; }

    /// <summary>
    /// Si conviene pedir el turno a <c>POST /consultas/flujo</c> para ver la
    /// redacción mientras se escribe (asistente-optimizaciones-modelo-local, D9).
    /// </summary>
    public bool RedaccionEnFlujo { get; init; }

    /// <summary>Arma el DTO a partir del catálogo.</summary>
    internal static CapacidadesDto De(CapacidadesDelActor capacidades)
    {
        ArgumentNullException.ThrowIfNull(capacidades);

        return new CapacidadesDto
        {
            Cubre = [.. capacidades.Cubre.Select(a => new AreaDto(a.Nombre, a.Descripcion, a.Columnas))],
            Tablas = capacidades.Tablas,
            Columnas = capacidades.Columnas,
            Ejemplos = capacidades.Ejemplos,
            NoPuede = capacidades.NoPuede,
            Alcance = capacidades.Alcance,
            Presentacion = capacidades.Presentacion,
            Mantenimiento = MantenimientoDto.De(capacidades.Mantenimiento),
            Cupo = new CupoDto(
                capacidades.Cupo.Restante,
                capacidades.Cupo.Bloqueado,
                capacidades.Cupo.Motivo,
                capacidades.Cupo.VuelveA),
        };
    }
}
