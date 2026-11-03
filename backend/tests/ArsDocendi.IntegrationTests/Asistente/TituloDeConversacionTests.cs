using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>El auto-título de una conversación nueva, derivado de su primera pregunta.</summary>
public sealed class TituloDeConversacionTests
{

    [Fact]
    public void Una_pregunta_vacia_o_de_solo_espacios_no_es_valida()
    {
        Assert.Throws<ArgumentException>(() => TituloDeConversacion.Derivar("   "));
    }
}
