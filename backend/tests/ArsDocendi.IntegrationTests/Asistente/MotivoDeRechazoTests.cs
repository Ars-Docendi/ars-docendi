using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la interpretación tolerante del motivo de rechazo (design.md D2 de
/// asistente-rechazos-dinamicos).
/// </summary>
public sealed class MotivoDeRechazoTests
{
    [Theory]
    [InlineData("fuera_de_tema", MotivoDeRechazo.FueraDeTema)]
    [InlineData("otro_sistema", MotivoDeRechazo.OtroSistema)]
    [InlineData("muy_general", MotivoDeRechazo.MuyGeneral)]
    [InlineData("no_cubierto", MotivoDeRechazo.NoCubierto)]
    public void Cada_valor_de_cable_redondea(string valorDeCable, MotivoDeRechazo esperado)
    {
        var interpretado = MotivosDeRechazo.Interpretar(valorDeCable);

        Assert.Equal(esperado, interpretado);
        Assert.Equal(valorDeCable, MotivosDeRechazo.ValorDeCable(interpretado));
    }

    [Fact]
    public void Un_valor_fuera_del_conjunto_resuelve_no_cubierto()
    {
        Assert.Equal(MotivoDeRechazo.NoCubierto, MotivosDeRechazo.Interpretar("clima"));
    }

    [Fact]
    public void Una_cadena_vacia_resuelve_no_cubierto()
    {
        Assert.Equal(MotivoDeRechazo.NoCubierto, MotivosDeRechazo.Interpretar(""));
    }

    [Fact]
    public void Nulo_resuelve_no_cubierto()
    {
        Assert.Equal(MotivoDeRechazo.NoCubierto, MotivosDeRechazo.Interpretar(null));
    }

    [Fact]
    public void Mayusculas_y_un_espacio_de_mas_no_matchean_y_resuelven_no_cubierto()
    {
        // El conjunto es cerrado y la comparación es literal, a propósito: un
        // "casi igual" no es un valor válido más que uno inventado.
        Assert.Equal(MotivoDeRechazo.NoCubierto, MotivosDeRechazo.Interpretar("FUERA_DE_TEMA "));
    }
}
