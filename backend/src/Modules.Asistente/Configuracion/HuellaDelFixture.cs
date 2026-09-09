namespace Modules.Asistente;

/// <summary>
/// Huella del fixture sintético contra el que se graban los cassettes.
/// </summary>
/// <remarks>
/// La registra quien sabe cuál es —el evaluador, que ya la recalcula en cada
/// corrida para sellar sus reportes—, y no el módulo: el módulo no referencia al
/// núcleo de evaluación ni debe hacerlo.
///
/// <b>Si no está registrada, no se graba ni se sirve ningún cassette.</b> Es lo
/// que hace mecánica la garantía de que ningún cassette lleva filas reales: sin
/// con qué comparar, un cassette es indistinguible de uno grabado contra una base
/// de desarrollo con datos importados.
/// </remarks>
/// <param name="Valor">La huella, en hexadecimal.</param>
public sealed record HuellaDelFixture(string Valor);
