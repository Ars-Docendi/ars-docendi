namespace Modules.Asistente.Application;

/// <summary>
/// Cupo de llamadas al modelo por actor y ventana de tiempo (RF-20).
/// </summary>
/// <remarks>
/// Con una sola clave de API por ambiente, el proveedor factura al ambiente entero
/// y no puede atribuir consumo a ningún usuario. Si la cuota no vive acá, no vive
/// en ningún lado.
///
/// Se mide en <b>llamadas al modelo</b> y no en requests HTTP, y la diferencia no
/// es de matiz: un turno con reescritor cuesta tres llamadas, así que contar
/// requests subestimaría el consumo por un factor de tres.
///
/// El cupo se acota por identidad autenticada y nunca por dirección de origen:
/// todo el tráfico del sistema entra por un túnel, así que un departamento tras
/// NAT compartiría cupo con sus vecinos.
/// </remarks>
public interface ICuotaDelActor
{
    /// <summary>Si al actor le queda cupo en la ventana vigente.</summary>
    bool HayCupo(Guid actor);

    /// <summary>
    /// Anota lo que consumió un turno.
    /// </summary>
    /// <remarks>
    /// Se anota el turno completo al terminar y no cada llamada por separado. La
    /// consecuencia hay que decirla: un turno que arranca con cupo lo puede
    /// exceder por hasta <c>MaximoDeLlamadasPorTurno - 1</c> llamadas antes de
    /// que el exceso se vea. El desbalance está acotado por el techo del turno
    /// —cuatro— y se aceptó a cambio de no tener estado implícito de request en
    /// el decorador del proveedor.
    /// </remarks>
    void Anotar(Guid actor, int llamadas);

    /// <summary>
    /// Cuándo vuelve a haber cupo, o <c>null</c> si ya hay.
    /// </summary>
    /// <remarks>
    /// Lo necesita el mensaje al usuario. «Alcanzaste tu límite» sin decir hasta
    /// cuándo deja a quien pregunta sin nada que hacer más que reintentar.
    /// </remarks>
    DateTimeOffset? CupoVuelveA(Guid actor);
}
