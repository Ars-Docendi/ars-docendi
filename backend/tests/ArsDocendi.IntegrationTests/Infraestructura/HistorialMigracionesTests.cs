using System.Reflection;
using ArsDocendi.Shared.Persistencia;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class HistorialMigracionesTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("001,002,003", "")]
    [InlineData("001,002,003", "001,002")]
    [InlineData("001,002", "001,002")]
    public void Historial_prefijo_valido_es_aceptado(string disponibles, string aplicadas)
    {
        Invocar(disponibles, aplicadas);
    }

    [Theory]
    [InlineData("001,002", "anterior")]
    [InlineData("001,002,003", "001,003")]
    [InlineData("001,002", "002")]
    [InlineData("001,002", "001,001")]
    public void Historial_desconocido_o_discontinuo_se_rechaza(string disponibles, string aplicadas)
    {
        var error = Assert.Throws<TargetInvocationException>(() => Invocar(disponibles, aplicadas));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static void Invocar(string disponibles, string aplicadas)
    {
        var tipo = typeof(IMigradorModulo).Assembly.GetType(
            "ArsDocendi.Shared.Persistencia.ValidadorHistorialMigraciones");
        Assert.NotNull(tipo);
        var metodo = tipo.GetMethod("Validar", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(metodo);
        metodo.Invoke(null, [Separar(disponibles), Separar(aplicadas)]);
    }

    private static string[] Separar(string valor) =>
        valor.Split(',', StringSplitOptions.RemoveEmptyEntries);
}
