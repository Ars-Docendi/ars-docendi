namespace Modules.Asistente.Application;

/// <summary>Un turno ya resuelto del hilo.</summary>
public sealed record TurnoDelHilo(
    string Pregunta,
    DateTimeOffset Cuando,
    string? SqlEjecutado = null,
    string? ClaveDelCliente = null,
    Guid? TurnoHistoricoId = null,
    int InicioDeSegmentoAntes = 0,
    Aclaracion? AclaracionPendienteAntes = null,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? Referencias = null,
    EstadoDelTurno? Estado = null);

/// <summary>
/// La identidad del último turno que se registró al historial del hilo —sea cual sea su carril—,
/// junto con el token de retroalimentación que se le haya minteado y el segmento/aclaración que el
/// hilo tenía justo ANTES de resolverlo.
/// </summary>
internal sealed record IdentidadDelUltimoRegistrado(
    string? ClaveDelCliente,
    Guid? TurnoHistoricoId,
    Guid? ClaveDeRetroalimentacion,
    int InicioDeSegmentoAntes,
    Aclaracion? AclaracionPendienteAntes);

/// <summary>
/// Lo que <see cref="HiloConversacional.QuitarUltimoParaReemplazo"/> saca del hilo, para poder
/// reponerlo tal cual si el reemplazo termina en <see cref="EstadoDelTurno.Fallo"/>.
/// </summary>
internal sealed record TurnoSacado(
    TurnoDelHilo? DelContexto,
    IdentidadDelUltimoRegistrado Identidad,
    int InicioDeSegmentoAlSacar,
    Aclaracion? AclaracionAlSacar);

/// <summary>
/// El estado conversacional de una charla: sus turnos, dónde arranca el segmento vigente y si hay
/// una aclaración esperando respuesta.
/// </summary>
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
    /// El id de la conversación persistida (<c>asistente.hilo_historico.id</c>) que este hilo
    /// efímero alimenta, o <c>null</c> si todavía no escribió ningún turno al historial.
    /// </summary>
    public Guid? HiloHistorico { get; set; }

    /// <summary>Índice del primer turno del segmento vigente.</summary>
    public int InicioDeSegmento { get; private set; }

    /// <summary>La aclaración esperando respuesta, si hay alguna.</summary>
    public Aclaracion? AclaracionPendiente { get; private set; }

    /// <summary>Todos los turnos, incluidos los de segmentos ya soltados.</summary>
    public IReadOnlyList<TurnoDelHilo> Turnos => _turnos;

    /// <summary>
    /// La identidad del último turno registrado al historial, sea cual sea su carril, o <c>null</c>
    /// si este hilo todavía no registró ninguno.
    /// </summary>
    internal IdentidadDelUltimoRegistrado? UltimoRegistrado { get; private set; }

    /// <summary>Agrega un turno resuelto por el carril SQL y renueva la vigencia.</summary>
    public void Agregar(
        string pregunta,
        DateTimeOffset cuando,
        string? sqlEjecutado = null,
        string? claveDelCliente = null,
        Guid? turnoHistoricoId = null,
        int? inicioDeSegmentoAntes = null,
        Aclaracion? aclaracionPendienteAntes = null,
        bool huboAclaracionAntes = false,
        IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? referencias = null,
        EstadoDelTurno? estado = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        _turnos.Add(new TurnoDelHilo(
            pregunta,
            cuando,
            sqlEjecutado,
            claveDelCliente,
            turnoHistoricoId,
            inicioDeSegmentoAntes ?? InicioDeSegmento,
            huboAclaracionAntes ? aclaracionPendienteAntes : AclaracionPendiente,
            referencias,
            estado));
        UltimaActividad = cuando;
    }

    /// <summary>
    /// Cuántos turnos de <b>toda</b> la conversación —no del segmento vigente— terminaron <see
    /// cref="EstadoDelTurno.NoContestable"/> (design.md D5 de asistente-rechazos-dinamicos).
    /// </summary>
    public int RechazosPrevios() => _turnos.Count(turno => turno.Estado == EstadoDelTurno.NoContestable);

    /// <summary>
    /// Marca este turno como el último que se registró al historial del hilo, sea cual sea su
    /// carril (design.md D9 de asistente-rediseno-v3).
    /// </summary>
    internal void MarcarUltimoRegistrado(
        string? claveDelCliente,
        Guid? turnoHistoricoId,
        Guid? claveDeRetroalimentacion,
        int inicioDeSegmentoAntes,
        Aclaracion? aclaracionPendienteAntes) =>
        UltimoRegistrado = new IdentidadDelUltimoRegistrado(
            claveDelCliente,
            turnoHistoricoId,
            claveDeRetroalimentacion,
            inicioDeSegmentoAntes,
            aclaracionPendienteAntes);

    /// <summary>Las consultas del segmento vigente, de la más vieja a la más reciente.</summary>
    public IReadOnlyList<string> ConsultasVigentes(int tope) =>
        [.. HistorialVigente(tope)
            .Select(turno => turno.SqlEjecutado)
            .Where(sql => !string.IsNullOrWhiteSpace(sql))
            .Select(sql => sql!)];

    /// <summary>
    /// Los marcadores <c>$refN</c> que trae el segmento vigente, de todos sus turnos combinados
    /// (design.md D11 de asistente-rediseno-v3), para que un seguimiento que edita o anida una
    /// consulta anterior pueda reusar su marcador.
    /// </summary>
    public IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)> ReferenciasVigentes(int tope)
    {
        var combinadas = new Dictionary<string, (TipoDeMencion Tipo, Guid Id)>(StringComparer.Ordinal);

        foreach (var turno in HistorialVigente(tope))
        {
            if (turno.Referencias is null)
            {
                continue;
            }

            foreach (var (marcador, referencia) in turno.Referencias)
            {
                combinadas[marcador] = referencia;
            }
        }

        return combinadas;
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

    /// <summary>Suelta el tema: el segmento vigente arranca en el próximo turno.</summary>
    public void SoltarElTema() => InicioDeSegmento = _turnos.Count;

    /// <summary>Deja una aclaración esperando respuesta.</summary>
    public void Pendiente(Aclaracion aclaracion) => AclaracionPendiente = aclaracion;

    /// <summary>Cierra la aclaración pendiente, con o sin respuesta reconocida.</summary>
    public void CerrarAclaracion() => AclaracionPendiente = null;

    /// <summary>
    /// Saca el turno que <see cref="UltimoRegistrado"/> nombra, para poder reemplazarlo: si ese
    /// turno también había llegado al carril SQL —está en <see cref="Turnos"/>—, lo saca de ahí
    /// también; si no —un saludo, una meta-pregunta, un menú de aclaración o una degradación
    /// pre-SQL—, no hay nada que sacar de ese lado.
    /// </summary>
    internal TurnoSacado QuitarUltimoParaReemplazo()
    {
        var identidad = UltimoRegistrado!;
        var inicioDeSegmentoAlSacar = InicioDeSegmento;
        var aclaracionAlSacar = AclaracionPendiente;

        TurnoDelHilo? delContexto = null;
        if (_turnos.Count > 0 && EsElMismoTurno(_turnos[^1], identidad))
        {
            delContexto = _turnos[^1];
            _turnos.RemoveAt(_turnos.Count - 1);
        }

        InicioDeSegmento = identidad.InicioDeSegmentoAntes;
        AclaracionPendiente = identidad.AclaracionPendienteAntes;

        return new TurnoSacado(delContexto, identidad, inicioDeSegmentoAlSacar, aclaracionAlSacar);
    }

    /// <summary>
    /// Repone lo que <see cref="QuitarUltimoParaReemplazo"/> había sacado,
    /// porque el reemplazo terminó en <see cref="EstadoDelTurno.Fallo"/>
    /// (design.md D9, punto 4: un fallo no cambia nada).
    /// </summary>
    internal void ReponerTrasFallo(TurnoSacado sacado)
    {
        if (sacado.DelContexto is { } turno)
        {
            _turnos.Add(turno);
        }

        InicioDeSegmento = sacado.InicioDeSegmentoAlSacar;
        AclaracionPendiente = sacado.AclaracionAlSacar;
        UltimoRegistrado = sacado.Identidad;
    }

    /// <summary>
    /// Si <paramref name="turno"/> —una entrada de <see cref="Turnos"/>— es el
    /// mismo turno que nombra <paramref name="identidad"/>.
    /// </summary>
    private static bool EsElMismoTurno(TurnoDelHilo turno, IdentidadDelUltimoRegistrado identidad) =>
        turno.ClaveDelCliente == identidad.ClaveDelCliente
        && turno.TurnoHistoricoId == identidad.TurnoHistoricoId;
}

/// <summary>
/// Se pidió reemplazar un turno que no es el último del hilo del actor, o un hilo que ya venció
/// (design.md D9 de asistente-rediseno-v3).
/// </summary>
internal sealed class ReemplazoInvalido(string reemplaza)
    : Exception($"El turno '{reemplaza}' no es el último turno vigente del hilo.")
{
    /// <summary>El identificador de reemplazo que se pidió.</summary>
    public string Reemplaza { get; } = reemplaza;
}

/// <summary>Se quiso usar un hilo que pertenece a otro actor.</summary>
internal sealed class HiloAjeno(Guid hilo)
    : Exception($"El hilo '{hilo}' pertenece a otro actor.")
{
    /// <summary>El hilo que se intentó usar.</summary>
    public Guid Hilo { get; } = hilo;
}
