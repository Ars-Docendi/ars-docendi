namespace Modules.Asistente.Application;

/// <summary>
/// Una de las opciones que el asistente le ofrece al usuario para desambiguar.
/// </summary>
/// <param name="Etiqueta">
/// Lo que se le muestra al usuario y lo que el reconocedor considera la respuesta
/// canónica. Nunca el texto que el usuario escribió.
/// </param>
/// <param name="PreguntaResuelta">
/// La pregunta autocontenida que queda si el usuario elige esta opción. La calcula
/// el detector en el momento de armar el menú, y no el reconocedor: el detector es
/// el único que sabe qué término de la pregunta era el ambiguo.
/// </param>
public sealed record OpcionDeAclaracion(string Etiqueta, string PreguntaResuelta);

/// <summary>
/// Una aclaración pendiente en el hilo.
/// </summary>
/// <remarks>
/// Es el tercer estado del sistema, y el que justifica toda la pieza: no es «no
/// puedo responder», es «puedo en cuanto elijas». Colapsarlo contra «no
/// contestable» hace que el asistente diga «no puedo» cuando corresponde «¿cuál de
/// estas?».
/// </remarks>
public sealed class Aclaracion(
    string terminoAmbiguo,
    string preguntaOriginal,
    IReadOnlyList<OpcionDeAclaracion> opciones)
{
    /// <summary>El valor de la pregunta que colisionó.</summary>
    public string TerminoAmbiguo { get; } = terminoAmbiguo;

    /// <summary>La pregunta tal como llegó, antes de desambiguar.</summary>
    public string PreguntaOriginal { get; } = preguntaOriginal;

    /// <summary>Las opciones ofrecidas, en el orden en que se muestran.</summary>
    public IReadOnlyList<OpcionDeAclaracion> Opciones { get; } = opciones;

    /// <summary>Cuántas veces se reofreció el menú sin reconocer la respuesta.</summary>
    public int Reintentos { get; private set; }

    /// <summary>Registra que la respuesta no se reconoció.</summary>
    public void Fallo() => Reintentos++;

    /// <summary>Si ya se agotaron los reintentos permitidos.</summary>
    public bool Agotada(int maximo) => Reintentos >= maximo;

    /// <summary>El texto del menú, en español.</summary>
    public string Texto()
    {
        var lineas = Opciones
            .Select((opcion, indice) => $"{indice + 1}. {opcion.Etiqueta}");

        return $"«{TerminoAmbiguo}» puede referirse a más de una cosa. ¿Cuál te interesa?\n"
            + string.Join("\n", lineas);
    }
}
