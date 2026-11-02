using System.Reflection;
using ArsDocendi.Migraciones;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class SolicitudMigracionTests
{
    [Theory]
    [InlineData("--migrate", "--estado-migraciones")]
    [InlineData("--script-migraciones", "")]
    [InlineData("--script-migraciones", "../fuera")]
    public void Argumentos_ambiguos_o_ruta_insegura_se_rechazan(string primero, string segundo)
    {
        var args = segundo.Length == 0 ? new[] { primero } : new[] { primero, segundo };
        var error = Assert.Throws<TargetInvocationException>(() => Parsear(args));
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    private static object Parsear(string[] args)
    {
        var tipo = typeof(MigradorEfSql<>).Assembly.GetType("ArsDocendi.Migraciones.SolicitudMigracion");
        Assert.NotNull(tipo);
        return tipo.GetMethod("Parsear")!.Invoke(null, [args])!;
    }
}
