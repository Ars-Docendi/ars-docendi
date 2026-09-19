namespace Modules.Asistente.Application;

/// <summary>
/// Los cuatro resultados de turno que la capa arma sin pasar por el carril.
/// </summary>
/// <remarks>
/// Son constructores con nombre: cada uno fija los campos que ESE final del turno
/// exige —cero llamadas al modelo, sin filas, categoría no contestable— y le pone
/// el nombre del caso. Estaban adentro de <see cref="CapaConversacional"/> y no
/// son orquestación: no deciden nada, describen un desenlace.
///
/// <see cref="Caido"/> recibe las llamadas en vez de leerlas de un contador
/// porque acá no hay turno: son exactamente las que la cuota le va a cobrar al
/// actor, y quien las sabe es quien las contó.
/// </remarks>
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
    /// <remarks>
    /// No lo ve nadie: se construye para pasar por el mismo camino de registro que
    /// los demás y que la separación en dos filas siga ocurriendo en un solo lugar.
    /// El texto va vacío a propósito — un turno caído no tiene respuesta, y poner
    /// una haría que el registro sugiriera que el usuario leyó algo.
    /// </remarks>
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
            // Las que alcanzó a emitir, que son exactamente las que la cuota le va a
            // cobrar al actor en el `finally`. Si el registro dijera cero, las dos
            // fuentes discreparían justo en el caso que se está registrando.
            llamadas,
            conversacion.Id);

    /// <summary>Categoría con que el registro analítico marca un turno caído.</summary>
    internal const string CategoriaDelFallo = "fallo";

    /// <summary>Un turno que termina sin modelo: cero llamadas al proveedor.</summary>
    public static ResultadoDelTurno Degradado(HiloConversacional conversacion, string texto) =>
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
            conversacion.Id);

    /// <summary>Un turno del carril sin datos: cero llamadas al modelo.</summary>
    /// <remarks>
    /// Las sugerencias viajan acá aunque el turno esté respondido, y no es una
    /// contradicción con el rechazo cooperativo: las sugerencias no bloquean. Son
    /// los ejemplos ejecutables que acompañan a la meta-pregunta, y es lo que hace
    /// que «¿qué podés hacer?» termine en algo clicable en vez de en un párrafo.
    /// </remarks>
    public static ResultadoDelTurno SinDatos(
        HiloConversacional conversacion,
        string texto,
        IReadOnlyList<string>? sugerencias = null) =>
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
            Sugerencias: sugerencias);
}
