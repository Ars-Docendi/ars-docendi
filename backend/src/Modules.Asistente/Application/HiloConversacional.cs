namespace Modules.Asistente.Application;

/// <summary>Un turno ya resuelto, tal como el hilo lo recuerda.</summary>
/// <param name="Pregunta">
/// La pregunta autocontenida del turno. <b>Nunca las filas que devolvió</b>: ver la
/// nota de <see cref="HiloConversacional"/>.
/// </param>
/// <param name="Cuando">Cuándo se resolvió.</param>
public sealed record TurnoDelHilo(string Pregunta, DateTimeOffset Cuando);

/// <summary>
/// El estado conversacional de una charla: sus turnos, dónde arranca el segmento
/// vigente y si hay una aclaración esperando respuesta.
/// </summary>
/// <remarks>
/// <b>Guarda preguntas y nunca filas.</b> Sería cómodo guardar los resultados para
/// darle más contexto al reescritor, y es exactamente lo que no hay que hacer: el
/// enmascarador sacó los datos personales del camino de salida hacia el proveedor,
/// y guardarlos acá los devolvería al prompt por la puerta del historial. Además
/// contradiría «las filas nunca se persisten», que ya está verificado por test.
///
/// <b>No se persiste</b>, y es una decisión tomada: el hilo se pierde en cada
/// redespliegue y eso se acepta. Se revisa si aparece evidencia medida de que la
/// pérdida molesta en uso real.
/// </remarks>
public sealed class HiloConversacional(Guid id, Guid actor)
{
    private readonly List<TurnoDelHilo> _turnos = [];

    /// <summary>Identificador del hilo.</summary>
    public Guid Id { get; } = id;

    /// <summary>El actor que lo abrió. Ningún otro puede usarlo.</summary>
    public Guid Actor { get; } = actor;

    /// <summary>Cuándo se lo tocó por última vez.</summary>
    public DateTimeOffset UltimaActividad { get; private set; }

    /// <summary>
    /// Índice del primer turno del segmento vigente.
    /// </summary>
    /// <remarks>
    /// El recorte del historial ancla acá y no en cero. Anclar para siempre el
    /// primer turno arrastra contexto muerto: una conversación que cambió de tema
    /// tres veces seguiría mandándole al reescritor el tema original.
    /// </remarks>
    public int InicioDeSegmento { get; private set; }

    /// <summary>La aclaración esperando respuesta, si hay alguna.</summary>
    public Aclaracion? AclaracionPendiente { get; private set; }

    /// <summary>Todos los turnos, incluidos los de segmentos ya soltados.</summary>
    public IReadOnlyList<TurnoDelHilo> Turnos => _turnos;

    /// <summary>Agrega un turno resuelto y renueva la vigencia.</summary>
    public void Agregar(string pregunta, DateTimeOffset cuando)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        _turnos.Add(new TurnoDelHilo(pregunta, cuando));
        UltimaActividad = cuando;
    }

    /// <summary>Renueva la vigencia sin agregar un turno.</summary>
    public void Tocar(DateTimeOffset cuando) => UltimaActividad = cuando;

    /// <summary>
    /// Los turnos del segmento vigente, a lo sumo <paramref name="tope"/>, del más
    /// viejo al más reciente.
    /// </summary>
    public IReadOnlyList<TurnoDelHilo> HistorialVigente(int tope)
    {
        if (tope <= 0 || InicioDeSegmento >= _turnos.Count)
        {
            return [];
        }

        var delSegmento = _turnos.Count - InicioDeSegmento;
        var desde = InicioDeSegmento + Math.Max(0, delSegmento - tope);

        return _turnos.GetRange(desde, _turnos.Count - desde);
    }

    /// <summary>
    /// Suelta el tema: el segmento vigente arranca en el próximo turno.
    /// </summary>
    /// <remarks>
    /// No borra nada. El historial anterior sigue en <see cref="Turnos"/> y deja de
    /// contar para <see cref="HistorialVigente"/>, que es lo único que se le manda
    /// al modelo.
    /// </remarks>
    public void SoltarElTema() => InicioDeSegmento = _turnos.Count;

    /// <summary>Deja una aclaración esperando respuesta.</summary>
    public void Pendiente(Aclaracion aclaracion) => AclaracionPendiente = aclaracion;

    /// <summary>Cierra la aclaración pendiente, con o sin respuesta reconocida.</summary>
    public void CerrarAclaracion() => AclaracionPendiente = null;
}

/// <summary>
/// Se quiso usar un hilo que pertenece a otro actor.
/// </summary>
/// <remarks>
/// Falla en vez de devolver un hilo nuevo en silencio. Un identificador ajeno no es
/// un caso normal que valga la pena tolerar: o es un error de programación del
/// cliente, o es alguien probando identificadores. Las dos cosas se quieren ver.
/// </remarks>
public sealed class HiloAjeno(Guid hilo)
    : Exception($"El hilo '{hilo}' pertenece a otro actor.")
{
    /// <summary>El hilo que se intentó usar.</summary>
    public Guid Hilo { get; } = hilo;
}
