using System.Reflection;
using ArsDocendi.Migraciones;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class SolicitudMigracionTests
{
    [Theory]
    [InlineData("--migrate", "Migrar")]
    [InlineData("--estado-migraciones", "Estado")]
    [InlineData("--validar-recursos", "ValidarRecursos")]
    public void Modo_unico_es_reconocido(string argumento, string esperado)
    {
        var resultado = Parsear([argumento]);
        Assert.Equal(esperado, resultado.GetType().GetProperty("Modo")!.GetValue(resultado)!.ToString());
    }

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

    [Fact]
    public void Preview_por_tar_no_depende_de_bind_mounts()
    {
        var resultado = Parsear(["--script-migraciones", "-"]);
        Assert.Equal("-", resultado.GetType().GetProperty("Directorio")!.GetValue(resultado));
    }

    private static object Parsear(string[] args)
    {
        var tipo = typeof(MigradorEfSql<>).Assembly.GetType("ArsDocendi.Migraciones.SolicitudMigracion");
        Assert.NotNull(tipo);
        return tipo.GetMethod("Parsear")!.Invoke(null, [args])!;
    }
}
