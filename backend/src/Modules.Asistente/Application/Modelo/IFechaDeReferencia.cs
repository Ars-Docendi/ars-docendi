namespace Modules.Asistente.Application;

/// <summary>
/// Resuelve el «hoy» del turno (RF-18).
/// </summary>
/// <remarks>
/// La fecha entra como <b>parámetro del turno</b> y no como función de reloj
/// dentro de la consulta. Eso arregla dos cosas a la vez.
///
/// Del lado de la evaluación: con una fecha fija inyectada, el dataset es
/// determinista. Un dataset cuyo resultado esperado cambia con el calendario
/// mide qué día lo corriste.
///
/// Del lado de la seguridad: si la consulta nunca necesita saber la hora, el
/// validador puede rechazar el reloj entero sin romper ningún caso legítimo, y
/// una prohibición que vivía como regla del prompt pasa a estar impuesta.
///
/// Mismo código, distinto input: en producción la real, en evaluación la fija.
/// </remarks>
public interface IFechaDeReferencia
{
    /// <summary>El día que el turno considera «hoy».</summary>
    DateOnly Hoy();
}

/// <summary>Fecha de referencia tomada del reloj del sistema.</summary>
/// <remarks>
/// Es el único lugar del carril que mira un reloj. Todo lo que está aguas abajo
/// recibe la fecha ya resuelta, así que un turno que empieza a las 23:59:59 no
/// puede cambiar de día a la mitad.
/// </remarks>
public sealed class FechaDeReferenciaDelSistema : IFechaDeReferencia
{
    public DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}

/// <summary>Fecha de referencia fija, para evaluación y para tests.</summary>
public sealed class FechaDeReferenciaFija(DateOnly fecha) : IFechaDeReferencia
{
    public DateOnly Hoy() => fecha;
}
