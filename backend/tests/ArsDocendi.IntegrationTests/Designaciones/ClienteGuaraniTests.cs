using System.Net;
using ArsDocendi.IntegrationTests.Designaciones.Guarani;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure.Guarani;
using Modules.Designaciones.Services;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class ClienteGuaraniTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Periodos_toman_el_anio_de_su_campo_aunque_el_nombre_lo_incluya()
    {
        var periodos = await GuaraniDePrueba.ConFixtures().Cliente().ListarPeriodosLectivosAsync(Ct);

        Assert.Equal(3, periodos.Count);
        Assert.Contains(new PeriodoLectivoGuarani("152", "2do Cuatrimestre", 2026), periodos);
        Assert.Contains(new PeriodoLectivoGuarani("151", "1er Cuatrimestre 2026", 2026), periodos);
    }

    [Fact]
    public async Task Comisiones_mapean_alias_numeros_y_nulls_de_left_join()
    {
        var guarani = GuaraniDePrueba.ConFixtures();
        var filas = await guarani.Cliente().ListarComisionesAsync("152", Ct);

        Assert.Equal(10, filas.Count);
        var primera = filas[0];
        Assert.Equal("9001", primera.Comision);
        Assert.Equal("501", primera.Asignacion);
        Assert.Equal("03500", primera.ElementoCodigo);
        Assert.Equal("Presencial", primera.ModalidadDescripcion);
        Assert.Contains(filas, f => f.Comision == "9002" && f.DocenteNombre is null && f.DocenteDocumento is null);
        Assert.Contains(filas, f => f.Comision == "9003" && f.Asignacion is null && f.DiaSemana is null);

        var solicitud = Assert.Single(guarani.Solicitudes);
        Assert.Equal("Basic", solicitud.Headers.Authorization!.Scheme);
        Assert.Equal("/api/comisiones?periodo_lectivo=152", solicitud.RequestUri!.PathAndQuery);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CausasImportacion.Autenticacion)]
    [InlineData(HttpStatusCode.Forbidden, CausasImportacion.Autenticacion)]
    [InlineData(HttpStatusCode.InternalServerError, CausasImportacion.NoDisponible)]
    public async Task Status_de_error_se_traduce_a_causa_generica(HttpStatusCode status, string causa)
    {
        var guarani = new GuaraniDePrueba().Responder(GuaraniDePrueba.RutaPeriodos, "[]", status);

        var error = await Assert.ThrowsAsync<ExcepcionGuarani>(() => guarani.Cliente().ListarPeriodosLectivosAsync(Ct));

        Assert.Equal(causa, error.Causa);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("{\"comision\": 1}")]
    [InlineData("[1, 2]")]
    [InlineData("[{\"comision\": {\"anidado\": true}}]")]
    public async Task Respuesta_inutilizable_es_respuesta_invalida(string cuerpo)
    {
        var guarani = new GuaraniDePrueba().Responder(GuaraniDePrueba.Comisiones("152"), cuerpo);

        var error = await Assert.ThrowsAsync<ExcepcionGuarani>(() => guarani.Cliente().ListarComisionesAsync("152", Ct));

        Assert.Equal(CausasImportacion.RespuestaInvalida, error.Causa);
    }

    [Fact]
    public async Task Sin_respuesta_a_tiempo_es_timeout()
    {
        var guarani = new GuaraniDePrueba().Fallar(GuaraniDePrueba.RutaPeriodos,
            () => throw new TaskCanceledException("timeout", new TimeoutException()));

        var error = await Assert.ThrowsAsync<ExcepcionGuarani>(() => guarani.Cliente().ListarPeriodosLectivosAsync(Ct));

        Assert.Equal(CausasImportacion.Timeout, error.Causa);
    }

    [Fact]
    public async Task Red_caida_es_no_disponible()
    {
        var guarani = new GuaraniDePrueba().Fallar(GuaraniDePrueba.RutaPeriodos,
            () => throw new HttpRequestException("sin ruta"));

        var error = await Assert.ThrowsAsync<ExcepcionGuarani>(() => guarani.Cliente().ListarPeriodosLectivosAsync(Ct));

        Assert.Equal(CausasImportacion.NoDisponible, error.Causa);
    }

    [Fact]
    public async Task Logs_no_contienen_credenciales_ni_documentos()
    {
        var logger = new LoggerCapturado<ClienteGuarani>();
        var guarani = new GuaraniDePrueba()
            .Responder(GuaraniDePrueba.RutaPeriodos, "[]", HttpStatusCode.Unauthorized)
            .Responder(GuaraniDePrueba.Comisiones("152"), GuaraniDePrueba.ComisionesJson.Replace("[", "{", StringComparison.Ordinal));
        var cliente = guarani.Cliente(logger: logger);

        await Assert.ThrowsAsync<ExcepcionGuarani>(() => cliente.ListarPeriodosLectivosAsync(Ct));
        await Assert.ThrowsAsync<ExcepcionGuarani>(() => cliente.ListarComisionesAsync("152", Ct));

        Assert.NotEmpty(logger.Mensajes);
        var opciones = GuaraniDePrueba.Opciones();
        Assert.All(logger.Mensajes, m =>
        {
            Assert.DoesNotContain(opciones.Contrasena, m, StringComparison.Ordinal);
            Assert.DoesNotContain(opciones.Usuario, m, StringComparison.Ordinal);
            Assert.DoesNotContain("28.341.567", m, StringComparison.Ordinal);
            Assert.DoesNotContain("LOPEZ", m, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("", "u", "c", false)]
    [InlineData("https://g.invalid/", "", "c", false)]
    [InlineData("https://g.invalid/", "u", "", false)]
    [InlineData("https://g.invalid/", "u", "c", true)]
    public void Configuracion_incompleta_deshabilita_la_integracion(string url, string usuario, string contrasena, bool esperada)
    {
        var opciones = GuaraniDePrueba.Opciones();
        opciones.UrlBase = url;
        opciones.Usuario = usuario;
        opciones.Contrasena = contrasena;

        Assert.Equal(esperada, opciones.EstaConfigurada);
    }

    [Fact]
    public void Ruta_de_comisiones_sin_marcador_de_periodo_no_esta_configurada()
    {
        var opciones = GuaraniDePrueba.Opciones();
        opciones.RutaComisiones = "api/comisiones";

        Assert.False(opciones.EstaConfigurada);
    }

    [Fact]
    public async Task Fixture_agrupa_en_las_tres_comisiones_esperadas()
    {
        var filas = await GuaraniDePrueba.ConFixtures().Cliente().ListarComisionesAsync("152", Ct);

        var comisiones = AgrupadorComisionesGuarani.Agrupar(filas);

        Assert.Equal(["9001", "9002", "9003"], comisiones.Select(c => c.Id));
    }
}
