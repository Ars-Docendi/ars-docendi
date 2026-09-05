namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// Una excepción a un invariante es una fila con motivo y ticket, no un párrafo.
/// </summary>
/// <remarks>
/// El referente es Metabase: su grafo de módulos no es un DAG y <c>:friends</c> es
/// un bypass total, pero es un DATO que el linter lee y que falla el build. Una
/// excepción documentada en prosa es una excepción que en seis meses nadie sabe si
/// sigue vigente ni si alguien la ensanchó.
///
/// Misma forma que <c>Toda_denegacion_explicita_lleva_motivo_escrito</c> en el
/// manifiesto de privilegios.
/// </remarks>
public sealed class ExcepcionesDeclaradasTests
{
    [Fact]
    public void Una_excepcion_sin_ticket_falla_nombrando_la_arista()
    {
        var (manifiesto, grafo) = Reales();
        var sinTicket = ReescribirExcepciones(manifiesto, e => e with { Ticket = string.Empty });

        var desviaciones = ComparadorDeAristas.Comparar(sinTicket, grafo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.ExcepcionSinTicket);
        Assert.Equal("ArsDocendi.Evaluacion.Nucleo -> Modules.Asistente", detectada.Objeto);
    }

    [Fact]
    public void Una_excepcion_sin_motivo_falla_nombrando_la_arista()
    {
        var (manifiesto, grafo) = Reales();
        var sinMotivo = manifiesto with
        {
            Aristas =
            [
                .. manifiesto.Aristas.Select(a =>
                    a.EsExcepcion ? a with { Motivo = string.Empty } : a),
            ],
        };

        // El ticket dice quién la aprobó; el motivo, por qué. Sin el segundo, la fila
        // registra que alguien firmó una excepción cuyo contenido no está escrito.
        var desviaciones = ComparadorDeAristas.Comparar(sinMotivo, grafo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.ExcepcionSinMotivo);
        Assert.Equal("ArsDocendi.Evaluacion.Nucleo -> Modules.Asistente", detectada.Objeto);
    }

    [Fact]
    public void Una_excepcion_sin_invariante_falla_nombrando_la_arista()
    {
        var (manifiesto, grafo) = Reales();
        var sinInvariante = ReescribirExcepciones(manifiesto, e => e with { Invariante = string.Empty });

        // Sin decir a qué invariante excede, la fila no es una excepción: es una
        // arista con una etiqueta que no compromete a nada.
        var desviaciones = ComparadorDeAristas.Comparar(sinInvariante, grafo);

        var detectada = Assert.Single(
            desviaciones, d => d.Tipo == TipoDesviacionDeArista.ExcepcionSinInvariante);
        Assert.Equal("ArsDocendi.Evaluacion.Nucleo -> Modules.Asistente", detectada.Objeto);
    }

    [Fact]
    public void La_unica_excepcion_del_manifiesto_lleva_invariante_ticket_y_motivo()
    {
        var manifiesto = ManifiestoDeAristas.Cargar();

        var excepcion = Assert.Single(manifiesto.Aristas, a => a.EsExcepcion);

        // `ArsDocendi.Evaluacion.Nucleo` es un proyecto que no es módulo referenciando
        // el proyecto INTERNO de uno. El motivo estaba escrito en un comentario de su
        // .csproj y ningún test lo alcanzaba; ahora es una fila que el CI defiende.
        Assert.Equal("ArsDocendi.Evaluacion.Nucleo -> Modules.Asistente", excepcion.ToString());
        Assert.False(string.IsNullOrWhiteSpace(excepcion.Excepcion!.Invariante));
        Assert.False(string.IsNullOrWhiteSpace(excepcion.Excepcion!.Ticket));
        Assert.False(string.IsNullOrWhiteSpace(excepcion.Motivo));
    }

    [Fact]
    public void Una_arista_comun_sin_excepcion_no_produce_desviaciones_de_excepcion()
    {
        var (manifiesto, grafo) = Reales();

        // La otra mitad del guard: si el comparador marcara toda arista sin ticket,
        // las dieciséis filas comunes estarían en rojo y la salida sería quitarle el
        // guard, no escribir tickets.
        var desviaciones = ComparadorDeAristas.Comparar(manifiesto, grafo);

        Assert.DoesNotContain(desviaciones, d => d.Tipo == TipoDesviacionDeArista.ExcepcionSinTicket);
    }

    private static (ManifiestoDeAristas Manifiesto, GrafoDeProyectos Grafo) Reales() =>
        (ManifiestoDeAristas.Cargar(), LectorDeAristas.LeerBackendSrc());

    private static ManifiestoDeAristas ReescribirExcepciones(
        ManifiestoDeAristas manifiesto, Func<ExcepcionDeclarada, ExcepcionDeclarada> reescritura) =>
        manifiesto with
        {
            Aristas =
            [
                .. manifiesto.Aristas.Select(a =>
                    a.Excepcion is null ? a : a with { Excepcion = reescritura(a.Excepcion) }),
            ],
        };
}
