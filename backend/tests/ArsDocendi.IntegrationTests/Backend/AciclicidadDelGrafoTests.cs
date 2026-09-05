namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// El invariante #2 —grafo dirigido acíclico— verificado en vez de leído.
/// </summary>
/// <remarks>
/// El test contra el grafo real pasa hoy, así que por sí solo no prueba nada: un
/// detector roto que no encuentra ningún ciclo pasaría igual. Por eso viene en par
/// con un ciclo sintético, igual que los guards del asistente.
/// </remarks>
public sealed class AciclicidadDelGrafoTests
{
    [Fact]
    public void El_grafo_real_de_backend_src_es_aciclico()
    {
        var grafo = LectorDeAristas.LeerBackendSrc();

        var ciclos = DetectorDeCiclos.Detectar(grafo);

        Assert.True(ciclos.Count == 0, "Ciclos detectados: " + string.Join(" | ", ciclos));
    }

    [Fact]
    public void Una_arista_que_cierra_un_ciclo_se_detecta_y_se_enumera()
    {
        var grafo = LectorDeAristas.LeerBackendSrc();
        var conCiclo = grafo with
        {
            // Shared volviendo a un módulo: el ciclo más plausible del repo, porque
            // Shared hospeda identity y alguien podría querer ahí un tipo del módulo.
            Aristas = [.. grafo.Aristas, new AristaReal("ArsDocendi.Shared", "Modules.Designaciones")],
        };

        var ciclos = DetectorDeCiclos.Detectar(conCiclo);

        var ciclo = Assert.Single(ciclos);
        Assert.Contains("Modules.Designaciones", ciclo.Proyectos);
        Assert.Contains("ArsDocendi.Shared", ciclo.Proyectos);

        // Enumerar los proyectos es la mitad del valor: un «hay un ciclo» sin
        // nombres obliga a reconstruir a mano cuál de las diecisiete aristas lo cerró.
        Assert.Equal(ciclo.Proyectos[0], ciclo.Proyectos[^1]);
    }

    [Fact]
    public void Un_ciclo_en_el_codigo_se_detecta_aunque_el_manifiesto_no_lo_declare()
    {
        var manifiesto = ManifiestoDeAristas.Cargar();
        var grafo = LectorDeAristas.LeerBackendSrc();
        var conCiclo = grafo with
        {
            Aristas = [.. grafo.Aristas, new AristaReal("Modules.Aulas.Contracts", "Modules.Aulas")],
        };

        // El manifiesto sigue declarando un conjunto sin ciclos, y eso no salva a
        // nadie: el invariante #2 es una propiedad de lo que compila.
        Assert.Empty(DetectorDeCiclos.Detectar(
            grafo with { Aristas = [.. manifiesto.Aristas.Select(a => new AristaReal(a.Origen, a.Destino))] }));
        Assert.NotEmpty(DetectorDeCiclos.Detectar(conCiclo));
    }
}
