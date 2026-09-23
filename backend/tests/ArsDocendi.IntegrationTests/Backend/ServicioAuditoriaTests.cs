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
            new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria([registro], 1)));

        var pagina = await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None);
        var evento = Assert.Single(pagina.Elementos);

        Assert.Empty(evento.ColumnasCambiadas);
        Assert.Empty(evento.Cambios);
    }

    private sealed class RepositorioAuditoriaEnMemoria(ResultadoPaginaAuditoria pagina) : IRepositorioAuditoria
    {
        public Task<ResultadoPaginaAuditoria> ListarAsync(ConsultaAuditoriaDto filtros, CancellationToken ct) =>
            Task.FromResult(pagina);
    }
}
