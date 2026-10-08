using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El auto-título de una conversación nueva, derivado de su primera pregunta.
/// </summary>
public sealed class TituloDeConversacionTests
{
    [Fact]
    public void Una_pregunta_corta_se_usa_tal_cual()
    {
        Assert.Equal(
            "¿Cuántos pedidos tiene pendientes la materia Álgebra?",
            TituloDeConversacion.Derivar(
                "¿Cuántos pedidos tiene pendientes la materia Álgebra?"));
    }

    [Fact]
    public void El_espacio_al_borde_se_recorta()
    {
        Assert.Equal("¿y Pérez?", TituloDeConversacion.Derivar("   ¿y Pérez?   "));
    }

    [Fact]
    public void Una_pregunta_larga_se_trunca_con_elipsis()
    {
        var larga = new string('a', 200);

        var titulo = TituloDeConversacion.Derivar(larga);

        Assert.True(titulo.Length <= TituloDeConversacion.LongitudMaxima + 1);
        Assert.EndsWith("…", titulo, StringComparison.Ordinal);
    }

    [Fact]
    public void El_truncado_no_corta_una_palabra_al_medio_si_puede_evitarlo()
    {
        var larga = string.Join(' ', Enumerable.Repeat("palabra", 20));

        var titulo = TituloDeConversacion.Derivar(larga);

        Assert.DoesNotContain("palabr…", titulo, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_pregunta_vacia_o_de_solo_espacios_no_es_valida()
    {
        Assert.Throws<ArgumentException>(() => TituloDeConversacion.Derivar("   "));
    }
}
