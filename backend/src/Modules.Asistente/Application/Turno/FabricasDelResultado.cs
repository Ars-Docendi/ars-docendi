namespace Modules.Asistente.Application;

/// <summary>Los cuatro resultados de turno que la capa arma sin pasar por el carril.</summary>
internal static class FabricasDelResultado
{
    public static ResultadoDelTurno NecesitaAclaracion(
        HiloConversacional conversacion,
        Aclaracion aclaracion,
        string interpretada,
        string mensaje) =>
        new(EstadoDelTurno.NecesitaAclaracion,
            aclaracion.Texto(),
            Razonamiento: string.Empty,
            string.Equals(interpretada, mensaje, StringComparison.Ordinal) ? null : interpretada,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            LlamadasAlModelo: 0,
            conversacion.Id,
            aclaracion.Opciones);

    /// <summary>El turno que se cayó, sólo para el registro.</summary>
    public static ResultadoDelTurno Caido(HiloConversacional conversacion, int llamadas) =>
        new(EstadoDelTurno.Fallo,
            string.Empty,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            CategoriaDelFallo,
            // Las que alcanzó a emitir, que son exactamente las que la cuota le va a cobrar al
            // actor en el `finally`.
            llamadas,
            conversacion.Id,
            Causa: CausaDelTurno.Excepcion);

    /// <summary>Categoría con que el registro analítico marca un turno caído.</summary>
    internal const string CategoriaDelFallo = "fallo";

    /// <summary>Un turno que termina sin modelo: cero llamadas al proveedor.</summary>
    public static ResultadoDelTurno Degradado(
        HiloConversacional conversacion, string texto, CausaDelTurno? causa) =>
        new(EstadoDelTurno.ServicioDegradado,
            texto,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            LlamadasAlModelo: 0,
            conversacion.Id,
            Causa: causa);

    /// <summary>Un turno del carril sin datos: cero llamadas al modelo.</summary>
    public static ResultadoDelTurno SinDatos(HiloConversacional conversacion, string texto) =>
        new(EstadoDelTurno.Respondida,
            texto,
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            GeneracionDeSql.CategoriaNoContestable,
            LlamadasAlModelo: 0,
            conversacion.Id,
            // Respondida, same as any other case of this state: this turn gets a feedback token
            // too, even though it never touched the model or the database — a greeting or a "what
            // can you do?" answer is still an answer to rate.
            ClaveDeRetroalimentacion: Guid.NewGuid());
}
