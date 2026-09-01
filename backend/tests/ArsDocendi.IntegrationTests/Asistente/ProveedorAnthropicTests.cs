using System.Globalization;
using System.Net;
using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el adaptador real del proveedor contra el cable, sin clave ni red.
/// </summary>
/// <remarks>
/// Todo lo que el adaptador tiene que garantizar es observable en el JSON que sale
/// o en la excepción que entra. Nada de esto necesita una cuenta ni gasta un token.
/// </remarks>
public sealed class ProveedorAnthropicTests
{
    private const string Prefijo = "Esquema de identity y designaciones. Respondé solo con SQL.";

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = Prefijo,
        Mensaje = "¿Qué docentes dictan Bases de Datos?",
        Temperatura = 0.0m,
        MaximoDeTokens = 512,
    };

    // ------------------------------------------------------------ lo que sale

    [Fact]
    public async Task El_prefijo_estable_viaja_como_bloque_de_sistema_marcado_para_cachear()
    {
        using var transporte = TransporteFalso.QueResponde();

        await Armar(transporte).CompletarAsync(Solicitud, TestContext.Current.CancellationToken);

        var sistema = Cuerpo(transporte).GetProperty("system");

        // Sin la marca, el diseño sigue siendo correcto y el ahorro simplemente no
        // ocurre: el esquema es el bloque más grande del prompt y se repite idéntico
        // turno a turno. Nada falla, y la diferencia aparece solo en la factura.
        Assert.Equal(Prefijo, sistema[0].GetProperty("text").GetString());
        Assert.Equal(
            "ephemeral", sistema[0].GetProperty("cache_control").GetProperty("type").GetString());
    }

    [Fact]
    public async Task La_pregunta_del_turno_no_entra_en_el_bloque_de_sistema()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = new TransporteFalso(_ => TransporteFalso.Exito("SELECT 1"));
        var proveedor = Armar(transporte);

        await proveedor.CompletarAsync(Solicitud, ct);
        await proveedor.CompletarAsync(Solicitud with { Mensaje = "¿Cuántos pedidos hay?" }, ct);

        var primero = Cuerpo(transporte, 0);
        var segundo = Cuerpo(transporte, 1);

        // La caché es un match de prefijo: un solo byte distinto en el bloque de
        // sistema invalida todo lo que viene después. Que la pregunta se filtre ahí
        // adentro rompería el ahorro sin romper ninguna respuesta.
        Assert.Equal(
            primero.GetProperty("system").GetRawText(), segundo.GetProperty("system").GetRawText());
        Assert.NotEqual(
            primero.GetProperty("messages").GetRawText(),
            segundo.GetProperty("messages").GetRawText());
    }

    [Fact]
    public async Task El_request_no_lleva_temperatura()
    {
        using var transporte = TransporteFalso.QueResponde();

        // La solicitud SÍ declara temperatura: el puerto la conserva porque otros
        // proveedores la usan. Lo que se afirma es que este adaptador la absorbe,
        // porque los modelos Claude actuales la rechazan con 400.
        await Armar(transporte).CompletarAsync(
            Solicitud with { Temperatura = 0.7m }, TestContext.Current.CancellationToken);

        Assert.False(Cuerpo(transporte).TryGetProperty("temperature", out _));
    }

    [Fact]
    public async Task El_esfuerzo_configurado_viaja_en_el_request()
    {
        using var transporte = TransporteFalso.QueResponde();

        await Armar(transporte, esfuerzo: "low").CompletarAsync(
            Solicitud, TestContext.Current.CancellationToken);

        // Es lo que reemplaza a la temperatura. Si no viajara, el carril SQL se
        // quedaría sin ninguna palanca de determinación.
        Assert.Equal(
            "low",
            Cuerpo(transporte).GetProperty("output_config").GetProperty("effort").GetString());
    }

    // ------------------------------------------------------------ lo que vuelve

    [Fact]
    public async Task Los_conteos_de_tokens_son_los_que_informo_el_proveedor()
    {
        using var transporte = TransporteFalso.QueResponde(
            tokensDeEntrada: 120, tokensDeSalida: 37, tokensDeCache: 4000);

        var respuesta = await Armar(transporte).CompletarAsync(
            Solicitud, TestContext.Current.CancellationToken);

        // Los servidos desde caché suman: son tokens de prompt reales y lo único
        // distinto es lo que cuestan. Contar solo los no cacheados haría que, el día
        // que la caché empiece a funcionar, el prefijo del esquema desapareciera del
        // registro y pareciera que los prompts se achicaron.
        Assert.Equal(4120, respuesta.TokensDeEntrada);
        Assert.Equal(37, respuesta.TokensDeSalida);
        Assert.False(respuesta.EsSimulada);
    }

    [Fact]
    public async Task Una_respuesta_sin_texto_devuelve_texto_vacio_y_no_falla()
    {
        using var transporte = new TransporteFalso(_ => TransporteFalso.Exito(texto: null));

        var respuesta = await Armar(transporte).CompletarAsync(
            Solicitud, TestContext.Current.CancellationToken);

        // No es una caída: el modelo contestó. Tratarlo como falla de transporte
        // abriría el corte por algo que no es una falla de servicio y le diría al
        // usuario «servicio degradado» cuando el servicio anduvo. Con texto vacío el
        // validador rechaza la SQL y el turno abstiene, que es lo correcto.
        Assert.Equal(string.Empty, respuesta.Texto);
    }

    [Fact]
    public async Task Un_rehuso_del_modelo_no_es_una_falla_y_queda_anotado()
    {
        using var transporte = new TransporteFalso(
            _ => TransporteFalso.Exito(texto: null, motivoDeCorte: "refusal"));
        var registro = new RegistroDeCapturas();

        var respuesta = await Armar(transporte, registro: registro).CompletarAsync(
            Solicitud, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, respuesta.Texto);

        // Warning y no Error: el proveedor funcionó. Saber que se rehusó —y no que
        // devolvió algo inválido— cambia qué se investiga.
        Assert.Contains(
            registro.DeNivel(LogLevel.Warning),
            linea => linea.Contains("rehusó", StringComparison.Ordinal));
        Assert.Empty(registro.DeNivel(LogLevel.Error));
    }

    // ------------------------------------------------------- traducción de fallas

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Una_falla_del_proveedor_llega_como_falla_de_transporte_del_modulo(
        HttpStatusCode estado)
    {
        using var transporte = TransporteFalso.QueFalla(estado);

        // HttpRequestException es una de las DOS formas de fallo que
        // ProveedorConBreaker cuenta. Cualquier otro tipo lo atraviesa sin contarse,
        // así que dejar escapar una excepción del SDK haría que el corte no abriera
        // nunca: un proveedor caído al cien por ciento seguiría recibiendo llamadas.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => Armar(transporte).CompletarAsync(Solicitud, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task El_adaptador_hace_un_solo_intento_ante_una_falla_de_transporte()
    {
        using var transporte = TransporteFalso.QueFalla(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Armar(transporte).CompletarAsync(Solicitud, TestContext.Current.CancellationToken));

        // El SDK reintenta por defecto ante 5xx. El módulo YA reintenta, en
        // ReintentoDeTransporte, y documenta el peor caso de un turno como
        // llamadas × intentos = 4 × 3 = 12 requests. Con los dos reintentos
        // encendidos ese número pasa a 36 y nada falla: el sistema hace el triple de
        // requests que su propia documentación declara, en silencio.
        Assert.Equal(1, transporte.Intentos);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Una_credencial_rechazada_degrada_el_turno_y_queda_registrada_como_error(
        HttpStatusCode estado)
    {
        using var transporte = TransporteFalso.QueFalla(estado);
        var registro = new RegistroDeCapturas();

        // Se degrada —el usuario ve lo que el contrato promete, no un 500— y además
        // se grita. Como falla de transporte a secas parecería intermitencia normal
        // del proveedor, y una clave mal cargada podría quedar así días.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => Armar(transporte, registro: registro).CompletarAsync(
                Solicitud, TestContext.Current.CancellationToken));

        var errores = registro.DeNivel(LogLevel.Error);

        Assert.Contains(errores, linea => linea.Contains("credencial", StringComparison.Ordinal));
        Assert.Contains(
            errores, linea => linea.Contains("ClaveDelProveedor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_request_mal_armado_se_registra_como_error_del_adaptador()
    {
        using var transporte = TransporteFalso.QueFalla(HttpStatusCode.BadRequest);
        var registro = new RegistroDeCapturas();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Armar(transporte, registro: registro).CompletarAsync(
                Solicitud, TestContext.Current.CancellationToken));

        // Ningún reintento lo arregla: el defecto está de este lado. Anotarlo como
        // falla del proveedor mandaría a investigar al lugar equivocado.
        Assert.Contains(
            registro.DeNivel(LogLevel.Error),
            linea => linea.Contains("adaptador", StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_cancelacion_llega_como_cancelacion_y_no_como_falla_de_transporte()
    {
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        using var transporte = TransporteFalso.QueResponde();

        // Es lo que hace que el timeout por llamada funcione: ProveedorConBreaker
        // reconoce la cancelación de su propio token y la convierte en
        // TimeoutDelProveedor. Traducirla a HttpRequestException le sacaría el
        // fallo de las manos y el corte nunca abriría por timeout.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Armar(transporte).CompletarAsync(Solicitud, cancelado.Token));
    }

    // ------------------------------------------------------------------ identidad

    [Fact]
    public async Task Una_respuesta_cortada_por_presupuesto_se_informa_y_se_grita()
    {
        // EL MODO DE FALLAR MÁS CARO DEL MÓDULO, si no se hace visible. Una
        // generación cortada deja un JSON incompleto, el intérprete no lo puede
        // leer, y el turno responde «no pude interpretar la pregunta» — palabra por
        // palabra lo que devuelve una pregunta genuinamente incontestable. Sin este
        // dato, un presupuesto mal dimensionado se ve igual que un asistente
        // prudente, y no hay nada en la respuesta que permita distinguirlos.
        var ct = TestContext.Current.CancellationToken;
        using var transporte = new TransporteFalso(
            _ => TransporteFalso.Exito("""{"sql": "SELECT * FROM""", motivoDeCorte: "max_tokens"));

        var registro = new RegistroDeCapturas();
        var respuesta = await Armar(transporte, registro: registro)
            .CompletarAsync(Solicitud, ct);

        Assert.True(respuesta.SeQuedoSinTokens);

        // Y en los logs, con el número que hay que subir. Warning y no Error: el
        // proveedor funcionó; lo que quedó chico es la configuración.
        var avisos = registro.DeNivel(LogLevel.Warning);

        Assert.Contains(avisos, linea =>
            linea.Contains(
                Solicitud.MaximoDeTokens.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Una_respuesta_completa_no_se_declara_cortada()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = new TransporteFalso(_ => TransporteFalso.Exito("SELECT 1"));

        var registro = new RegistroDeCapturas();
        var respuesta = await Armar(transporte, registro: registro)
            .CompletarAsync(Solicitud, ct);

        Assert.False(respuesta.SeQuedoSinTokens);
        Assert.Empty(registro.DeNivel(LogLevel.Warning));
    }
    [Fact]
    public void El_adaptador_no_se_declara_simulado_y_nombra_su_modelo()
    {
        using var transporte = TransporteFalso.QueResponde();

        var proveedor = Armar(transporte, modelo: "claude-sonnet-5");

        // El nombre lleva el modelo porque comparar costo contra calidad entre
        // modelos es para lo que existe el evaluador, y un reporte que solo dijera
        // «anthropic» no permitiría saber cuál de los dos corrió.
        Assert.False(proveedor.EsSimulado);
        Assert.Equal("anthropic/claude-sonnet-5", proveedor.Nombre);
    }

    // ------------------------------------------------------------------ apoyo

    private static ProveedorAnthropic Armar(
        TransporteFalso transporte,
        string modelo = "claude-opus-5",
        string esfuerzo = "high",
        RegistroDeCapturas? registro = null) =>
        new(
            new HttpClient(transporte, disposeHandler: false),
            "clave-de-prueba",
            modelo,
            esfuerzo,
            (registro ?? new RegistroDeCapturas()).Logger<ProveedorAnthropic>());

    private static JsonElement Cuerpo(TransporteFalso transporte, int cual = 0) =>
        JsonDocument.Parse(transporte.Cuerpos[cual]).RootElement;
}
