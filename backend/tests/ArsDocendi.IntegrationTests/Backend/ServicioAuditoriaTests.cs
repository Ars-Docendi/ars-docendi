using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Identity;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class ServicioAuditoriaTests
{
    [Fact]
    public async Task Evento_presenta_actor_legible_y_resumen_sin_valores_personales()
    {
        var registro = new RegistroCambio
        {
            Id = 1,
            NombreSchema = "identity",
            NombreTabla = "personas",
            ClaveFila = "persona-1",
            Accion = "INSERT",
            FilaNueva = "{\"nombre\":\"Dato privado\",\"estado\":\"activo\"}",
            ColumnasCambiadas = ["nombre", "estado"],
            CambiadoEn = DateTimeOffset.UnixEpoch,
        };
        var repositorio = new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
        [
            new RegistroAuditoria(registro, "Ernesto Vidal", "Ernesto", "Vidal"),
        ], 1));
        var servicio = new ServicioAuditoria(repositorio);

        var evento = Assert.Single((await servicio.ListarAsync(
            new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);

        Assert.Equal("Vidal, Ernesto", evento.Actor);
        Assert.Equal("Alta", evento.AccionEtiqueta);
        Assert.Equal("Identidad", evento.Modulo);
        Assert.Equal("Persona", evento.Objeto);
        Assert.Equal("Alta de persona · Estado, Nombre", evento.Resumen);
        Assert.DoesNotContain("Dato privado", System.Text.Json.JsonSerializer.Serialize(evento));
    }

    [Theory]
    [InlineData("INSERT", "Alta", "Alta de tabla nueva")]
    [InlineData("UPDATE", "Actualización", "Actualización de tabla nueva")]
    [InlineData("DELETE", "Eliminación física", "Eliminación física de tabla nueva")]
    public async Task Accion_y_fallbacks_de_actor_y_schema_son_explicitos(
        string accion,
        string etiqueta,
        string resumen)
    {
        var registro = new RegistroCambio
        {
            Id = 2,
            NombreSchema = "schema_futuro",
            NombreTabla = "tabla_nueva",
            ClaveFila = "fila-2",
            Accion = accion,
            CambiadoEn = DateTimeOffset.UnixEpoch,
        };
        var repositorio = new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
        [
            new RegistroAuditoria(registro, "Cuenta visible", null, null),
        ], 1));

        var evento = Assert.Single((await new ServicioAuditoria(repositorio).ListarAsync(
            new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);

        Assert.Equal("Cuenta visible", evento.Actor);
        Assert.Equal(etiqueta, evento.AccionEtiqueta);
        Assert.Equal("Schema futuro", evento.Modulo);
        Assert.Equal("Tabla nueva", evento.Objeto);
        Assert.Equal(resumen, evento.Resumen);
    }

    [Fact]
    public async Task Actor_sin_cuenta_se_presenta_como_no_identificado()
    {
        var registro = new RegistroCambio
        {
            Id = 3,
            NombreSchema = "portal",
            NombreTabla = "perfiles",
            ClaveFila = "fila-3",
            Accion = "UPDATE",
            CambiadoEn = DateTimeOffset.UnixEpoch,
        };
        var repositorio = new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
        [
            new RegistroAuditoria(registro, null, null, null),
        ], 1));

        var evento = Assert.Single((await new ServicioAuditoria(repositorio).ListarAsync(
            new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);

        Assert.Equal("Actor no identificado", evento.Actor);
        Assert.Equal("Portal", evento.Modulo);
    }

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

    [Theory]
    [InlineData("schema_futuro", "tabla_nueva")]
    [InlineData("identity", "personas")]
    public async Task Valor_legacy_no_clasificado_en_esa_tabla_se_oculta(string schema, string tabla)
    {
        var registro = new RegistroCambio
        {
            Id = 8,
            NombreSchema = schema,
            NombreTabla = tabla,
            ClaveFila = "8",
            Accion = "UPDATE",
            FilaNueva = "{\"code\":\"secreto-sintetico\"}",
            ColumnasCambiadas = ["code"]
        };
        var servicio = new ServicioAuditoria(new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(
            [new RegistroAuditoria(registro, null, null, null)], 1)));
        var evento = Assert.Single((await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);
        Assert.True(Assert.Single(evento.Cambios).Oculto);
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
