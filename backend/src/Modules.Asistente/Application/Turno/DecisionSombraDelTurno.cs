namespace Modules.Asistente.Application;

/// <summary>
/// Lleva la decisión del enrutador sombra desde el paso 5 hasta el registro.
/// </summary>
/// <remarks>
/// Tiene la forma de <see cref="ContadorDeLlamadasDelTurno"/> y no es casualidad:
/// es el mismo problema. La decisión se toma dentro del pipeline y el registro se
/// escribe afuera, <b>incluidas las dos ramas de <c>catch</c></b> —presupuesto
/// vencido y excepción no prevista—, donde no hay ningún resultado del pipeline del
/// que leerla. Un valor de retorno no llega hasta ahí; un objeto con alcance de
/// turno, sí.
///
/// De esa forma hereda la semántica útil: un turno que se cayó <b>después</b> del
/// paso 5 conserva en su fila la decisión que alcanzó a tomarse, exactamente como
/// conserva las llamadas que alcanzó a emitir.
///
/// <b>No viaja en <see cref="ResultadoDelTurno"/> a propósito.</b> Ese es el valor
/// de retorno del carril y llega al controller: telemetría que viaja en el objeto
/// de la respuesta está a un mapeo de distancia de aparecer en el cuerpo HTTP.
///
/// Vive con el alcance del request, así que un turno no hereda la decisión de otro.
/// </remarks>
public sealed class DecisionSombraDelTurno
{
    /// <summary>
    /// La intención que el enrutador de dominio eligió, o nulo si no capturó.
    /// </summary>
    /// <remarks>
    /// <b>Nulo es el caso normal y no un dato faltante.</b> Un catálogo de cinco
    /// intenciones no cubre la mayoría de las preguntas y no pretende hacerlo; un
    /// turno que ni siquiera llegó al paso 5 —un saludo, una meta-pregunta— también
    /// queda en nulo, y por el mismo motivo: no hubo decisión que registrar.
    /// </remarks>
    public string? Intencion { get; private set; }

    /// <summary>Anota qué intención habría capturado el turno.</summary>
    public void Anotar(string intencion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intencion);

        Intencion = intencion;
    }
}
