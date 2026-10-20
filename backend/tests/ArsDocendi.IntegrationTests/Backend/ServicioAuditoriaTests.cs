using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Identity;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class ServicioAuditoriaTests
{
    [Fact]
    public async Task Evento_sin_snapshots_ni_columnas_devuelve_cambios_vacios()
    {
        var registro = new RegistroCambio
        {
            Id = 1,
            NombreSchema = "identity",
            NombreTabla = "roles",
            ClaveFila = "1",
            Accion = "UPDATE",
            FilaAnterior = null,
            FilaNueva = null,
            ColumnasCambiadas = null,
            CambiadoEn = DateTimeOffset.UnixEpoch,
        };
        var servicio = new ServicioAuditoria(
            new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
                [new RegistroAuditoria(registro, null, null, null)], 1)));

        var pagina = await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None);
        var evento = Assert.Single(pagina.Elementos);

        Assert.Empty(evento.ColumnasCambiadas);
        Assert.Empty(evento.Cambios);
    }

    [Fact]
    public async Task Correo_en_nombre_de_cuenta_no_se_expone_como_actor()
    {
        var registro = new RegistroCambio { Id = 10, NombreSchema = "identity", NombreTabla = "users", ClaveFila = "10", Accion = "UPDATE" };
        var servicio = new ServicioAuditoria(new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
            [new RegistroAuditoria(registro, "actor-sintetico@example.test", null, null)], 1)));
        var evento = Assert.Single((await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);
        Assert.Equal("Actor no identificado", evento.Actor);
    }

    [Fact]
    public async Task Vigencia_expone_solo_valores_tecnicos_de_cargo_y_origen()
    {
        var registro = new RegistroCambio
        {
            Id = 9,
            NombreSchema = "designaciones",
            NombreTabla = "designaciones",
            ClaveFila = "9",
            Accion = "UPDATE",
            FilaAnterior = "{\"cargo_id\":\"cargo-uno\",\"origen_pedido_id\":\"pedido-uno\"}",
            FilaNueva = "{\"cargo_id\":\"cargo-dos\",\"origen_pedido_id\":\"pedido-dos\"}",
            ColumnasCambiadas = ["cargo_id", "origen_pedido_id"]
        };
        var servicio = new ServicioAuditoria(new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
            [new RegistroAuditoria(registro, null, null, null)], 1)));
        var evento = Assert.Single((await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);
        Assert.All(evento.Cambios, c => Assert.False(c.Oculto));
        Assert.Equal("pedido-uno", evento.Cambios.Single(c => c.Campo == "origen_pedido_id").ValorAnterior);
        Assert.Equal("pedido-dos", evento.Cambios.Single(c => c.Campo == "origen_pedido_id").ValorNuevo);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"rol_id\":{\"secreto\":\"valor\"}}")]
    [InlineData("{}")]
    public async Task Clave_json_invalida_se_rechaza_antes_de_consultar(string clave)
    {
        var servicio = new ServicioAuditoria(new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria([], 0)));
        await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() =>
            servicio.ListarAsync(new ConsultaAuditoriaDto(RowPk: clave), CancellationToken.None));
    }

    private sealed class RepositorioAuditoriaEnMemoria(ResultadoPaginaAuditoria pagina) : IRepositorioAuditoria
    {
        public Task<ResultadoPaginaAuditoria> ListarAsync(ConsultaAuditoriaDto filtros, CancellationToken ct) =>
            Task.FromResult(pagina);
    }
}
