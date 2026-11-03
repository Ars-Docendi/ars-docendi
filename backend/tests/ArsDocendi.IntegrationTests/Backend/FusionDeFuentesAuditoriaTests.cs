using ArsDocendi.Host.Administracion;
using static ArsDocendi.Host.Administracion.FusionDeFuentesAuditoria;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// Property test de <see cref="FusionDeFuentesAuditoria"/> contra la
/// referencia ingenua "materializar las dos, ordenar todo, cortar" (design.md
/// D2, tarea 2.5).
/// </summary>
public sealed class FusionDeFuentesAuditoriaTests
{
    [Fact]
    public void Ejemplo_minimo_intercala_por_fecha()
    {
        Marca[] cambios = [new(OrigenCambios, Fecha(3), 30), new(OrigenCambios, Fecha(1), 10)];
        Marca[] asistente = [new(OrigenAsistente, Fecha(2), 20)];

        var fusion = Fusionar(cambios, asistente);

        Assert.Equal([30L, 20L, 10L], fusion.Select(m => m.Id).ToArray());
    }

    [Fact]
    public void Un_empate_exacto_de_instante_pone_cambios_antes_que_asistente()
    {
        var mismoInstante = Fecha(5);
        Marca[] cambios = [new(OrigenCambios, mismoInstante, 1)];
        Marca[] asistente = [new(OrigenAsistente, mismoInstante, 999)];

        var fusion = Fusionar(cambios, asistente);

        Assert.Equal(OrigenCambios, fusion[0].Origen);
        Assert.Equal(OrigenAsistente, fusion[1].Origen);
    }

    [Fact]
    public void Cada_evento_aparece_exactamente_una_vez_a_traves_de_paginas_consecutivas()
    {
        var (cambios, asistente) = GenerarAleatorio(new Random(7), nCambios: 40, nAsistente: 15);
        var referencia = ReferenciaIngenua(cambios, asistente);

        var reconstruido = new List<Marca>();
        const int tamanoPagina = 6;
        for (var offset = 0; offset < referencia.Count; offset += tamanoPagina)
        {
            reconstruido.AddRange(FusionarVentana(cambios, asistente, offset, tamanoPagina));
        }

        Assert.Equal(referencia.Select(m => (m.Origen, m.Id)), reconstruido.Select(m => (m.Origen, m.Id)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Interleavings_aleatorios_coinciden_con_la_referencia_ingenua_en_toda_pagina_y_tamano(int semilla)
    {
        var azar = new Random(semilla);
        var (cambios, asistente) = GenerarAleatorio(
            azar, nCambios: azar.Next(0, 60), nAsistente: azar.Next(0, 60), permitirEmpates: true);
        var referencia = ReferenciaIngenua(cambios, asistente);

        foreach (var tamanoPagina in new[] { 1, 3, 10, 100 })
        {
            for (var offset = 0; offset <= referencia.Count + tamanoPagina; offset += tamanoPagina)
            {
                var esperado = referencia.Skip(offset).Take(tamanoPagina).Select(m => (m.Origen, m.Id));
                var obtenido = FusionarVentana(cambios, asistente, offset, tamanoPagina)
                    .Select(m => (m.Origen, m.Id));
                Assert.Equal(esperado, obtenido);
            }
        }
    }

    [Fact]
    public void Una_pagina_mas_alla_del_final_devuelve_vacio()
    {
        Marca[] cambios = [new(OrigenCambios, Fecha(1), 1)];
        Marca[] asistente = [];

        Assert.Empty(FusionarVentana(cambios, asistente, offset: 50, tamano: 10));
    }

    // ------------------------------------------------------------------ apoyo

    private static DateTimeOffset Fecha(int segundosDesdeEpoch) =>
        DateTimeOffset.UnixEpoch.AddSeconds(segundosDesdeEpoch);

    /// <summary>
    /// La referencia "ingenua" con la que design.md D2 pide contrastar la
    /// fusión: materializa las dos fuentes juntas y ordena todo por el MISMO
    /// orden total, sin ningún atajo de mergesort.
    /// </summary>
    private static IReadOnlyList<Marca> ReferenciaIngenua(
        IReadOnlyList<Marca> cambios, IReadOnlyList<Marca> asistente) =>
        [.. cambios.Concat(asistente)
            .OrderByDescending(m => m.CambiadoEn)
            .ThenBy(m => m.Origen == OrigenCambios ? 0 : 1)
            .ThenByDescending(m => m.Id)];

    private static (IReadOnlyList<Marca> Cambios, IReadOnlyList<Marca> Asistente) GenerarAleatorio(
        Random azar, int nCambios, int nAsistente, bool permitirEmpates = false)
    {
        // Con empates permitidos, varios eventos comparten el mismo segundo —
        // el caso límite que el orden total tiene que resolver por origen y por id.
        var techoDeSegundos = permitirEmpates ? Math.Max(1, (nCambios + nAsistente) / 3) : nCambios + nAsistente + 1;

        var cambios = OrdenarDesc(GenerarMarcas(azar, OrigenCambios, nCambios, techoDeSegundos, idBase: 1));
        var asistente = OrdenarDesc(GenerarMarcas(azar, OrigenAsistente, nAsistente, techoDeSegundos, idBase: 100_000));

        return (cambios, asistente);
    }

    private static List<Marca> GenerarMarcas(Random azar, string origen, int cantidad, int techoDeSegundos, long idBase)
    {
        var marcas = new List<Marca>(cantidad);
        for (var i = 0; i < cantidad; i++)
        {
            marcas.Add(new Marca(origen, Fecha(azar.Next(0, techoDeSegundos + 1)), idBase + i));
        }

        return marcas;
    }

    private static IReadOnlyList<Marca> OrdenarDesc(List<Marca> marcas) =>
        [.. marcas.OrderByDescending(m => m.CambiadoEn).ThenByDescending(m => m.Id)];
}
