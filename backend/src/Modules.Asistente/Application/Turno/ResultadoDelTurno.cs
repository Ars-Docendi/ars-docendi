namespace Modules.Asistente.Application;

/// <summary>
/// Los cuatro estados en que puede terminar un turno (RF-14), más el que no sale.
/// </summary>
public enum EstadoDelTurno
{
    /// <summary>Se respondió con datos.</summary>
    Respondida,

    /// <summary>La pregunta no se puede responder con lo que el asistente ve.</summary>
    NoContestable,

    /// <summary>Falta un dato para poder responder sin adivinar.</summary>
    NecesitaAclaracion,

    /// <summary>El proveedor no está disponible o el turno se quedó sin cupo.</summary>
    ServicioDegradado,

    /// <summary>
    /// El turno terminó en una excepción no prevista. <b>Sólo para el registro</b>:
    /// nunca viaja al cliente, que ve la excepción y no un cuerpo.
    /// </summary>
    Fallo,
}

/// <summary>Lo que devuelve el carril SQL.</summary>
public sealed record ResultadoDelTurno(
    EstadoDelTurno Estado,
    string Respuesta,
    string Razonamiento,
    string? PreguntaInterpretada,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<object?>> Filas,
    bool Truncado,
    IReadOnlyList<SensibilidadDeColumna> Sensibilidad,
    string Categoria,
    int LlamadasAlModelo,
    Guid Hilo = default,
    IReadOnlyList<OpcionDeAclaracion>? Opciones = null,
    string? Sql = null,
    string? SqlEjecutado = null,
    IReadOnlyList<VinculoDelResultado>? Vinculos = null,
    IReadOnlyList<VinculoDelTexto>? VinculosDelTexto = null,
    FichaDelElemento? Principal = null,
    IReadOnlyList<string>? Origenes = null,
    Guid? ClaveDeRetroalimentacion = null,
    int? CupoRestante = null,
    Guid? Conversacion = null,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? ReferenciasEjecutadas = null,
    MotivoDeRechazo? MotivoDeRechazo = null,
    CausaDelTurno? Causa = null);
