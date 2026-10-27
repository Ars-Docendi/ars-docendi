namespace Modules.Asistente.Application;

/// <summary>
/// Why a turn failed or came back empty (design D2 of asistente-metricas-de-calidad).
/// </summary>
public enum CausaDelTurno
{
    /// <summary>The breaker is open, or the availability pre-check found the provider down.</summary>
    ProveedorNoDisponible,

    /// <summary>Gate full or 429/503 from the shared service. Busy, not an outage: the breaker stays closed.</summary>
    ProveedorSaturado,

    /// <summary>The provider call timed out, or a client-side cancellation not from the request token.</summary>
    TimeoutProveedor,

    /// <summary>HTTP 401/403 from the provider: the configured credential was rejected.</summary>
    CredencialRechazada,

    /// <summary>HTTP 404 from the provider: the model is not pulled or was retired.</summary>
    ModeloNoEncontrado,

    /// <summary>Any other HTTP failure of the provider (transport, 5xx, 400/422).</summary>
    ErrorDelProveedor,

    /// <summary>The turn hit its ceiling of model calls.</summary>
    TechoDeLlamadas,

    /// <summary>The per-turn budget expired, distinct from a per-call timeout.</summary>
    PresupuestoDelTurno,

    /// <summary>The generation hit its token ceiling before closing its object.</summary>
    GeneracionCortada,

    /// <summary>The generation could not be interpreted.</summary>
    RespuestaIninteligible,

    /// <summary>The SQL validator rejected the generated query.</summary>
    ValidadorRechazo,

    /// <summary>The compiled plan lane abstained: invalid sample, rejected compiled SQL or an impossible percentage.</summary>
    PlanInvalido,

    /// <summary>The engine refused the query for lack of privilege.</summary>
    SinPrivilegioDeDatos,

    /// <summary>The engine rejected the query, other than a statement timeout.</summary>
    ErrorDelMotor,

    /// <summary>The engine cancelled the query (SQLSTATE 57014).</summary>
    TimeoutDeSentencia,

    /// <summary>An unhandled exception, recorded as <see cref="EstadoDelTurno.Fallo"/>.</summary>
    Excepcion,

    /// <summary>The query ran and returned no rows. A signal, not a failure.</summary>
    ResultadoVacio,

    /// <summary>The actor's daily quota ran out.</summary>
    CupoAgotado,

    /// <summary>The organization-wide spending cap ran out.</summary>
    TopeOrganizacional,

    /// <summary>The module is in maintenance mode.</summary>
    Mantenimiento,

    /// <summary>The actor's access was revoked.</summary>
    AccesoRevocado,

    /// <summary>The turn lock was refused: another turn of the actor is running.</summary>
    TurnoConcurrente,
}

/// <summary>The family a <see cref="CausaDelTurno"/> belongs to (design D1/D2).</summary>
internal enum FamiliaDeCausa
{
    /// <summary>Says something about the model or its provider.</summary>
    Proveedor,

    /// <summary>Says something about the generated query.</summary>
    Consulta,

    /// <summary>Informative only: not counted as a failure.</summary>
    Senal,

    /// <summary>Says nothing about a model: excluded from every rate.</summary>
    Politica,
}

/// <summary>Wire values and families of <see cref="CausaDelTurno"/>.</summary>
internal static class CausasDelTurno
{
    private static readonly IReadOnlyDictionary<CausaDelTurno, (string Valor, FamiliaDeCausa Familia, EstadoDelTurno Estado)> Tabla =
        new Dictionary<CausaDelTurno, (string, FamiliaDeCausa, EstadoDelTurno)>
        {
            [CausaDelTurno.ProveedorNoDisponible] = ("proveedor_no_disponible", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.ProveedorSaturado] = ("proveedor_saturado", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.TimeoutProveedor] = ("timeout_proveedor", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.CredencialRechazada] = ("credencial_rechazada", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.ModeloNoEncontrado] = ("modelo_no_encontrado", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.ErrorDelProveedor] = ("error_del_proveedor", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.TechoDeLlamadas] = ("techo_de_llamadas", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.PresupuestoDelTurno] = ("presupuesto_del_turno", FamiliaDeCausa.Proveedor, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.GeneracionCortada] = ("generacion_cortada", FamiliaDeCausa.Proveedor, EstadoDelTurno.NoContestable),
            [CausaDelTurno.RespuestaIninteligible] = ("respuesta_ininteligible", FamiliaDeCausa.Proveedor, EstadoDelTurno.NoContestable),
            [CausaDelTurno.ValidadorRechazo] = ("validador_rechazo", FamiliaDeCausa.Consulta, EstadoDelTurno.NoContestable),
            [CausaDelTurno.PlanInvalido] = ("plan_invalido", FamiliaDeCausa.Consulta, EstadoDelTurno.NoContestable),
            [CausaDelTurno.SinPrivilegioDeDatos] = ("sin_privilegio_de_datos", FamiliaDeCausa.Consulta, EstadoDelTurno.NoContestable),
            [CausaDelTurno.ErrorDelMotor] = ("error_del_motor", FamiliaDeCausa.Consulta, EstadoDelTurno.NoContestable),
            [CausaDelTurno.TimeoutDeSentencia] = ("timeout_de_sentencia", FamiliaDeCausa.Consulta, EstadoDelTurno.NoContestable),
            [CausaDelTurno.Excepcion] = ("excepcion", FamiliaDeCausa.Consulta, EstadoDelTurno.Fallo),
            [CausaDelTurno.ResultadoVacio] = ("resultado_vacio", FamiliaDeCausa.Senal, EstadoDelTurno.Respondida),
            [CausaDelTurno.CupoAgotado] = ("cupo_agotado", FamiliaDeCausa.Politica, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.TopeOrganizacional] = ("tope_organizacional", FamiliaDeCausa.Politica, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.Mantenimiento] = ("mantenimiento", FamiliaDeCausa.Politica, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.AccesoRevocado] = ("acceso_revocado", FamiliaDeCausa.Politica, EstadoDelTurno.ServicioDegradado),
            [CausaDelTurno.TurnoConcurrente] = ("turno_concurrente", FamiliaDeCausa.Politica, EstadoDelTurno.ServicioDegradado),
        };

    /// <summary>Every code, in declaration order.</summary>
    public static IReadOnlyCollection<CausaDelTurno> Todas => Tabla.Keys.ToArray();

    /// <summary>The value written to <c>registro_analitico.causa</c>.</summary>
    public static string ValorDeCable(CausaDelTurno causa) => Tabla[causa].Valor;

    /// <summary>The family of the cause.</summary>
    public static FamiliaDeCausa Familia(CausaDelTurno causa) => Tabla[causa].Familia;

    /// <summary>The only outcome state a turn with this cause may have (design D2 table).</summary>
    public static EstadoDelTurno EstadoEsperado(CausaDelTurno causa) => Tabla[causa].Estado;

    /// <summary>The cause of an HTTP failure of the provider, split by status code.</summary>
    public static CausaDelTurno DeHttp(System.Net.HttpStatusCode? estado) => estado switch
    {
        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => CausaDelTurno.CredencialRechazada,
        System.Net.HttpStatusCode.NotFound => CausaDelTurno.ModeloNoEncontrado,
        _ => CausaDelTurno.ErrorDelProveedor,
    };

    /// <summary>The cause of an engine rejection: SQLSTATE 57014 is a statement timeout.</summary>
    public static CausaDelTurno DeMotor(string? sqlState) =>
        string.Equals(sqlState, "57014", StringComparison.Ordinal)
            ? CausaDelTurno.TimeoutDeSentencia
            : CausaDelTurno.ErrorDelMotor;

    /// <summary>The policy or provider cause of a "no model" verdict.</summary>
    public static CausaDelTurno? DeMotivoSinModelo(MotivoSinModelo motivo) => motivo switch
    {
        MotivoSinModelo.CuotaAgotada => CausaDelTurno.CupoAgotado,
        MotivoSinModelo.TopeOrganizacionalAgotado => CausaDelTurno.TopeOrganizacional,
        MotivoSinModelo.Mantenimiento => CausaDelTurno.Mantenimiento,
        MotivoSinModelo.SinAcceso => CausaDelTurno.AccesoRevocado,
        MotivoSinModelo.TurnoConcurrente => CausaDelTurno.TurnoConcurrente,
        MotivoSinModelo.ProveedorCaido => CausaDelTurno.ProveedorNoDisponible,
        _ => null,
    };
}
