namespace Modules.Asistente.Application;

/// <summary>Un turno ya resuelto del hilo.</summary>
/// <param name="Pregunta">La pregunta interpretada, autocontenida.</param>
/// <param name="Cuando">Cuándo se resolvió.</param>
/// <param name="SqlEjecutado">
/// La consulta que produjo la respuesta que el usuario vio, o <c>null</c> si el
/// turno no llegó a devolver filas.
/// </param>
/// <param name="ClaveDelCliente">
/// La <c>Idempotency-Key</c> con la que se mandó este turno, si es de esta
/// sesión. <c>null</c> en un turno sembrado por <see cref="Sembrar"/> — ésos no
/// tienen clave de esta sesión, y se identifican por <see cref="TurnoHistoricoId"/>.
/// </param>
/// <param name="TurnoHistoricoId">
/// El id de <c>asistente.turno_historico</c> que este turno sembró. Lo fija
/// <see cref="Sembrar"/>; <c>null</c> en un turno vivo de esta sesión.
/// </param>
/// <param name="InicioDeSegmentoAntes">
/// <see cref="InicioDeSegmento"/> tal como estaba justo ANTES de resolver este
/// turno. Reemplazar este turno restaura este valor, para que la pregunta
/// nueva se resuelva contra el mismo contexto que tuvo la reemplazada
/// (design.md D9).
/// </param>
/// <param name="AclaracionPendienteAntes">
/// <see cref="AclaracionPendiente"/> tal como estaba justo ANTES de resolver
/// este turno. Mismo motivo que <see cref="InicioDeSegmentoAntes"/>.
/// </param>
/// <remarks>
/// <b>Nulo no es «no se generó consulta»: es «no hay nada que continuar».</b> Un
/// turno vacío, abstenido o degradado no anota consulta a propósito — ofrecerle al
/// modelo una consulta que no encontró nada lo invita a repetirla.
///
/// <b>Sólo el carril SQL agrega un <see cref="TurnoDelHilo"/></b> —ver
/// <see cref="HiloConversacional.Agregar"/>, llamado únicamente ahí—: es el
/// CONTEXTO que se le manda al reescritor, y un saludo o un menú de
/// aclaración no aportan nada que continuar. Por eso este tipo NO es lo que
/// identifica «la última pregunta» para «Editar y reenviar»
/// (asistente-edicion-de-la-ultima-pregunta): eso lo hace
/// <see cref="IdentidadDelUltimoRegistrado"/>, aparte.
/// </remarks>
public sealed record TurnoDelHilo(
    string Pregunta,
    DateTimeOffset Cuando,
    string? SqlEjecutado = null,
    string? ClaveDelCliente = null,
    Guid? TurnoHistoricoId = null,
    int InicioDeSegmentoAntes = 0,
    Aclaracion? AclaracionPendienteAntes = null);

/// <summary>
/// La identidad del último turno que se registró al historial del hilo —sea
/// cual sea su carril—, junto con el token de retroalimentación que se le
/// haya minteado y el segmento/aclaración que el hilo tenía justo ANTES de
/// resolverlo.
/// </summary>
/// <remarks>
/// <b>SEPARADA DE <see cref="TurnoDelHilo"/>/<see cref="HiloConversacional.Turnos"/>
/// A PROPÓSITO</b> (design.md D9 de asistente-rediseno-v3, ARS-147). Un turno
/// social, una meta-pregunta, un menú de aclaración o una degradación
/// pre-carril-SQL nunca entran al contexto que se le manda al reescritor
/// —<see cref="HiloConversacional.Agregar"/> sólo lo llama el carril SQL—,
/// pero para el usuario siguen siendo «la última pregunta»: la que «Editar y
/// reenviar» tiene que poder nombrar. Sin este registro aparte, reemplazar la
/// pregunta que sigue a un «hola», una meta-pregunta, un menú de aclaración o
/// una degradación devolvía <c>409</c> porque la única identidad que se
/// chequeaba era la del contexto SQL — ese turno nunca había llegado a
/// <see cref="HiloConversacional.Agregar"/>, aunque SÍ quedó persistido en
/// <c>turno_historico</c>.
/// </remarks>
/// <param name="ClaveDelCliente">Ver <see cref="TurnoDelHilo.ClaveDelCliente"/>.</param>
/// <param name="TurnoHistoricoId">Ver <see cref="TurnoDelHilo.TurnoHistoricoId"/>.</param>
/// <param name="ClaveDeRetroalimentacion">
/// El token de retroalimentación que se minteó para este turno, o <c>null</c>
/// si no se minteó ninguno. Es la única forma de encontrarlo para revocarlo
/// (<see cref="IValidezDeRetroalimentacion.Revocar"/>) cuando este turno se
/// reemplaza: el <see cref="ResultadoDelTurno"/> original ya no existe.
/// </param>
/// <param name="InicioDeSegmentoAntes">Ver <see cref="TurnoDelHilo.InicioDeSegmentoAntes"/>.</param>
/// <param name="AclaracionPendienteAntes">Ver <see cref="TurnoDelHilo.AclaracionPendienteAntes"/>.</param>
internal sealed record IdentidadDelUltimoRegistrado(
    string? ClaveDelCliente,
    Guid? TurnoHistoricoId,
    Guid? ClaveDeRetroalimentacion,
    int InicioDeSegmentoAntes,
    Aclaracion? AclaracionPendienteAntes);

/// <summary>
/// Lo que <see cref="HiloConversacional.QuitarUltimoParaReemplazo"/> saca del
/// hilo, para poder reponerlo tal cual si el reemplazo termina en
/// <see cref="EstadoDelTurno.Fallo"/>.
/// </summary>
/// <param name="DelContexto">
/// El turno que se sacó de <see cref="HiloConversacional.Turnos"/>, o
/// <c>null</c> si el último turno registrado nunca había entrado ahí —un
/// saludo, una meta-pregunta, un menú de aclaración o una degradación
/// pre-SQL no tienen nada que sacar de ese lado.
/// </param>
/// <param name="Identidad">La identidad completa del turno que se reemplaza.</param>
/// <param name="InicioDeSegmentoAlSacar">
/// El <see cref="HiloConversacional.InicioDeSegmento"/> que el hilo tenía AL
/// MOMENTO de sacarlo —no el que tenía antes de ese turno—, para poder
/// reponerlo exactamente como estaba.
/// </param>
/// <param name="AclaracionAlSacar">Mismo motivo que <see cref="InicioDeSegmentoAlSacar"/>.</param>
internal sealed record TurnoSacado(
    TurnoDelHilo? DelContexto,
    IdentidadDelUltimoRegistrado Identidad,
    int InicioDeSegmentoAlSacar,
    Aclaracion? AclaracionAlSacar);

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
    /// El id de la conversación persistida (<c>asistente.hilo_historico.id</c>)
    /// que este hilo efímero alimenta, o <c>null</c> si todavía no escribió
    /// ningún turno al historial.
    /// </summary>
    /// <remarks>
    /// Deliberadamente independiente de <see cref="Id"/> (design.md D1 de
    /// asistente-historial-conversaciones): éste vive 120 minutos y se pierde
    /// en cada redespliegue; la conversación persistida tiene que sobrevivir
    /// 180 días y ser resumible mucho después de que este hilo haya vencido.
    /// El escritor del historial lo fija la primera vez que este hilo escribe
    /// una fila; los turnos siguientes del mismo hilo lo reusan en vez de
    /// abrir una conversación nueva.
    /// </remarks>
    public Guid? HiloHistorico { get; set; }

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

    /// <summary>
    /// La identidad del último turno registrado al historial, sea cual sea su
    /// carril, o <c>null</c> si este hilo todavía no registró ninguno. Ver
    /// <see cref="IdentidadDelUltimoRegistrado"/>.
    /// </summary>
    internal IdentidadDelUltimoRegistrado? UltimoRegistrado { get; private set; }

    /// <summary>Agrega un turno resuelto por el carril SQL y renueva la vigencia.</summary>
    /// <param name="pregunta">La pregunta interpretada.</param>
    /// <param name="cuando">Cuándo se resolvió.</param>
    /// <param name="sqlEjecutado">
    /// La consulta que respondió, o <c>null</c> si el turno no devolvió filas.
    /// <b>Nunca un valor leído de la base</b>: lo que entra acá son literales que
    /// vinieron de la pregunta del usuario, y ésa ya se guardaba.
    /// </param>
    /// <param name="claveDelCliente">
    /// La <c>Idempotency-Key</c> de este turno, si es de esta sesión. Ver
    /// <see cref="TurnoDelHilo.ClaveDelCliente"/>.
    /// </param>
    /// <param name="turnoHistoricoId">
    /// El id de <c>turno_historico</c> que sembró este turno, sólo para
    /// <see cref="Sembrar"/>. Ver <see cref="TurnoDelHilo.TurnoHistoricoId"/>.
    /// </param>
    /// <param name="inicioDeSegmentoAntes">
    /// El <see cref="InicioDeSegmento"/> ANTES de resolver este turno, o
    /// <c>null</c> para usar el valor actual (el caso normal: quien llama no
    /// mutó el segmento entre capturarlo y llamar acá). Ver
    /// <see cref="TurnoDelHilo.InicioDeSegmentoAntes"/>.
    /// </param>
    /// <param name="aclaracionPendienteAntes">
    /// La <see cref="AclaracionPendiente"/> ANTES de resolver este turno,
    /// «indicador de que no se pasó ninguna» aparte —se distingue con
    /// <paramref name="huboAclaracionAntes"/>, porque <c>null</c> es un valor
    /// legítimo de «no había ninguna pendiente»—.
    /// </param>
    /// <param name="huboAclaracionAntes">
    /// Si <paramref name="aclaracionPendienteAntes"/> se pasó explícitamente.
    /// En falso, se usa <see cref="AclaracionPendiente"/> actual.
    /// </param>
    public void Agregar(
        string pregunta,
        DateTimeOffset cuando,
        string? sqlEjecutado = null,
        string? claveDelCliente = null,
        Guid? turnoHistoricoId = null,
        int? inicioDeSegmentoAntes = null,
        Aclaracion? aclaracionPendienteAntes = null,
        bool huboAclaracionAntes = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        _turnos.Add(new TurnoDelHilo(
            pregunta,
            cuando,
            sqlEjecutado,
            claveDelCliente,
            turnoHistoricoId,
            inicioDeSegmentoAntes ?? InicioDeSegmento,
            huboAclaracionAntes ? aclaracionPendienteAntes : AclaracionPendiente));
        UltimaActividad = cuando;
    }

    /// <summary>
    /// Marca este turno como el último que se registró al historial del hilo,
    /// sea cual sea su carril (design.md D9 de asistente-rediseno-v3). Se
    /// llama SIEMPRE que un turno se registra —<c>Estado != Fallo</c>—, lo
    /// haya agregado <see cref="Agregar"/> al contexto SQL o no.
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

    /// <summary>
    /// Las consultas del segmento vigente, de la más vieja a la más reciente.
    /// </summary>
    /// <remarks>
    /// Se deriva de <see cref="HistorialVigente"/> y no de <see cref="Turnos"/>:
    /// así el pivote suelta las consultas por el mismo mecanismo con que suelta las
    /// preguntas, sin código propio que pueda quedar desincronizado.
    /// </remarks>
    public IReadOnlyList<string> ConsultasVigentes(int tope) =>
        [.. HistorialVigente(tope)
            .Select(turno => turno.SqlEjecutado)
            .Where(sql => !string.IsNullOrWhiteSpace(sql))
            .Select(sql => sql!)];

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

    /// <summary>
    /// Saca el turno que <see cref="UltimoRegistrado"/> nombra, para poder
    /// reemplazarlo: si ese turno también había llegado al carril SQL —está en
    /// <see cref="Turnos"/>—, lo saca de ahí también; si no —un saludo, una
    /// meta-pregunta, un menú de aclaración o una degradación pre-SQL—, no hay
    /// nada que sacar de ese lado. Restaura el segmento y la aclaración que el
    /// hilo tenía justo ANTES de resolver ese turno, sea cual sea su carril
    /// (design.md D9, punto 2).
    /// </summary>
    /// <returns>
    /// Lo que se sacó, para poder reponerlo exactamente como estaba si el
    /// reemplazo termina en <see cref="EstadoDelTurno.Fallo"/> (ver
    /// <see cref="ReponerTrasFallo"/>).
    /// </returns>
    /// <remarks>
    /// Precondición: <see cref="UltimoRegistrado"/> no es <c>null</c> y nombra
    /// el objetivo pedido — el llamador ya lo validó.
    /// </remarks>
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
/// Se pidió reemplazar un turno que no es el último del hilo del actor, o un
/// hilo que ya venció (design.md D9 de asistente-rediseno-v3). El controlador
/// lo mapea a <c>409</c>: no cambia nada.
/// </summary>
internal sealed class ReemplazoInvalido(string reemplaza)
    : Exception($"El turno '{reemplaza}' no es el último turno vigente del hilo.")
{
    /// <summary>El identificador de reemplazo que se pidió.</summary>
    public string Reemplaza { get; } = reemplaza;
}

/// <summary>
/// Se quiso usar un hilo que pertenece a otro actor.
/// </summary>
/// <remarks>
/// Falla en vez de devolver un hilo nuevo en silencio. Un identificador ajeno no es
/// un caso normal que valga la pena tolerar: o es un error de programación del
/// cliente, o es alguien probando identificadores. Las dos cosas se quieren ver.
/// </remarks>
internal sealed class HiloAjeno(Guid hilo)
    : Exception($"El hilo '{hilo}' pertenece a otro actor.")
{
    /// <summary>El hilo que se intentó usar.</summary>
    public Guid Hilo { get; } = hilo;
}
