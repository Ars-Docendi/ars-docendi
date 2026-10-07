using System.Text.Json;
using ArsDocendi.Migraciones;
using ArsDocendi.Shared.Persistencia;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class PreviewMigracionesTests
{
    [Fact]
    public async Task Preview_sin_pendientes_solo_genera_manifiesto_no_op()
    {
        IMigradorModulo[] migradores = [new MigradorDePrueba("identity", false)];
        var estados = new[] { await migradores[0].ConsultarAsync(TestContext.Current.CancellationToken) };
        var archivos = await PreviewMigraciones.GenerarAsync(migradores, estados, "sha-prueba", TestContext.Current.CancellationToken);
        Assert.Single(archivos);
        using var json = JsonDocument.Parse(archivos["manifiesto.json"]);
        Assert.True(json.RootElement.GetProperty("noOp").GetBoolean());
        Assert.Equal("sha-prueba", json.RootElement.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task Scripts_preservan_orden_y_tienen_hash()
    {
        IMigradorModulo[] migradores = [new MigradorDePrueba("identity", true), new MigradorDePrueba("storage", true)];
        var estados = new[] { await migradores[0].ConsultarAsync(TestContext.Current.CancellationToken), await migradores[1].ConsultarAsync(TestContext.Current.CancellationToken) };
        var archivos = await PreviewMigraciones.GenerarAsync(migradores, estados, "sha-prueba", TestContext.Current.CancellationToken);
        Assert.Equal(["01_identity.sql", "02_storage.sql", "manifiesto.json"], archivos.Keys.ToArray());
        using var json = JsonDocument.Parse(archivos["manifiesto.json"]);
        Assert.Equal(64, json.RootElement.GetProperty("scripts")[0].GetProperty("sha256").GetString()!.Length);
    }

    [Fact]
    public async Task Cambio_de_historial_durante_preview_se_rechaza()
    {
        var migrador = new MigradorDePrueba("identity", true);
        var estado = await migrador.ConsultarAsync(TestContext.Current.CancellationToken);
        migrador.Aplicada = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PreviewMigraciones.GenerarAsync(
            [migrador], [estado], "sha-prueba", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Exportacion_no_sobrescribe_directorio_ocupado()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "preview_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ruta);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(ruta, "existente"), "conservar", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ArgumentException>(() => PreviewMigraciones.ExportarAsync(
                new Dictionary<string, string> { ["manifiesto.json"] = "{}" }, ruta, TestContext.Current.CancellationToken));
            Assert.Equal("conservar", await File.ReadAllTextAsync(Path.Combine(ruta, "existente"), TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(ruta, true); }
    }

    private sealed class MigradorDePrueba(string contexto, bool pendiente) : IMigradorModulo
    {
        public bool Aplicada { get; set; } = !pendiente;
        public string Contexto => contexto;
        public void ValidarRecursos() { }
        public Task MigrarAsync(CancellationToken ct) { Aplicada = true; return Task.CompletedTask; }
        public Task<string> GenerarScriptAsync(CancellationToken ct) => Task.FromResult(Aplicada ? "" : "SELECT 1;");
        public Task<EstadoMigracionesModulo> ConsultarAsync(CancellationToken ct) => Task.FromResult(
            new EstadoMigracionesModulo(contexto, ["001"], Aplicada ? ["001"] : [], Aplicada ? [] : ["001"]));
    }
}
