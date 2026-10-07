using ArsDocendi.Shared.Persistencia;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class ContratoMigracionesTests
{
    [Fact]
    public void Contrato_expone_consulta_y_preview_sin_exponer_contextos()
    {
        var metodos = typeof(IMigradorModulo).GetMethods();
        Assert.Contains(metodos, metodo => metodo.Name == "ConsultarAsync");
        Assert.Contains(metodos, metodo => metodo.Name == "GenerarScriptAsync");
        Assert.DoesNotContain(metodos, metodo => metodo.ReturnType.Name.Contains("DbContext"));
    }

    [Fact]
    public void Adaptador_ef_no_vive_en_shared()
    {
        Assert.Null(typeof(IMigradorModulo).Assembly.GetType("ArsDocendi.Shared.Persistencia.MigradorEfSql`1"));
        var ensamblado = System.Reflection.Assembly.Load("ArsDocendi.Migraciones");
        Assert.NotNull(ensamblado.GetType("ArsDocendi.Migraciones.MigradorEfSql`1"));
    }
}
