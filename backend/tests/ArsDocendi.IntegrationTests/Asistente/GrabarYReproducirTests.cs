using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Grabar y reproducir, punta a punta, sin clave y sin red.
/// </summary>
/// <remarks>
/// Arma el <b>mismo pipeline</b> que el módulo —grabador más reintento— con un
/// handler terminal que responde con la forma exacta de la API de mensajes, graba
/// a un directorio temporal, y después arma un segundo pipeline en modo
/// reproducción cuyo terminal <b>falla si alguien lo llama</b>. Si el cassette se
/// lee, la respuesta traducida sale igual; si no se lee, el test se entera porque
/// el terminal explota.
///
/// La alternativa era levantar un servidor local que impersonara la API. Se
/// descarta por lo que arrastra: una opción de configuración nueva cuyo único
/// consumidor son los tests, un puerto que ligar en CI, y una perilla que redirige
/// a dónde viaja la credencial —el módulo ya tiene un guard dedicado a que la
/// clave no se filtre—.
///
/// Y esto es lo que el mecanismo existe para cubrir: el parseo del adaptador
/// corriendo sobre el cuerpo <b>grabado</b>, no sobre uno que armamos nosotros.
/// </remarks>
public sealed class GrabarYReproducirTests : IDisposable
{
    private const string Fixture = "hash-del-fixture-vigente";

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Esquema de identity y designaciones. Respondé solo con SQL.",
        Mensaje = "¿Qué docentes dictan Bases de Datos?",
        Temperatura = 0.0m,
        Esfuerzo = EsfuerzoDelModelo.Medio,
        MaximoDeTokens = 512,
    };

    private readonly List<IDisposable> _abiertos = [];

    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(), "cassettes-" + Guid.NewGuid().ToString("n"));

    // ------------------------------------------------------------------ grabar

    [Fact]
    public async Task Grabar_contra_un_transporte_que_impersona_la_API_deja_el_cassette()
    {
        using var transporte = TransporteFalso.QueResponde("SELECT 42");

        await Armar(transporte, regrabar: true).CompletarAsync(
            Solicitud, TestContext.Current.CancellationToken);

        var cassettes = new AlmacenDeCassettes(_directorio).Todos();

        Assert.Single(cassettes);
        Assert.Contains("SELECT 42", cassettes[0].Cuerpo, StringComparison.Ordinal);
        Assert.Equal("claude-opus-5", cassettes[0].Sello.Modelo);
    }

    // ------------------------------------------------------------- reproducir

    [Fact]
    public async Task Reproducir_devuelve_la_misma_respuesta_sin_tocar_el_transporte()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = TransporteFalso.QueResponde(
            "SELECT 42", tokensDeEntrada: 120, tokensDeSalida: 37, tokensDeCache: 4000);

        var grabada = await Armar(transporte, regrabar: true).CompletarAsync(Solicitud, ct);

        using var explosivo = new TerminalQueExplota();
        var reproducida = await Armar(explosivo, regrabar: false).CompletarAsync(Solicitud, ct);

        // El adaptador no recibe ninguna señal de que la respuesta vino de disco:
        // es la misma en los cinco campos.
        Assert.Equal(grabada, reproducida);
    }

    [Fact]
    public async Task El_corte_por_techo_de_tokens_sobrevive_el_viaje_a_disco()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = new TransporteFalso(
            _ => TransporteFalso.Exito("""{"sql": "SELECT * FROM""", motivoDeCorte: "max_tokens"));

        await Armar(transporte, regrabar: true).CompletarAsync(Solicitud, ct);

        using var explosivo = new TerminalQueExplota();
        var reproducida = await Armar(explosivo, regrabar: false).CompletarAsync(Solicitud, ct);

        // Es el dato que distingue «el modelo se abstuvo» de «el presupuesto quedó
        // corto», y sin él las dos cosas se ven exactamente igual —en el registro y,
        // peor, en la métrica—. Que sobreviva al viaje a disco es lo que permite que
        // un cassette real ejercite ese camino.
        Assert.True(reproducida.SeQuedoSinTokens);
    }

    [Fact]
    public async Task Los_tres_conteos_de_tokens_salen_del_cuerpo_grabado()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = TransporteFalso.QueResponde(
            "SELECT 42", tokensDeEntrada: 120, tokensDeSalida: 37, tokensDeCache: 8000);

        await Armar(transporte, regrabar: true).CompletarAsync(Solicitud, ct);

        using var explosivo = new TerminalQueExplota();
        var reproducida = await Armar(explosivo, regrabar: false).CompletarAsync(Solicitud, ct);

        // Los servidos por caché suman a los de entrada y además se informan aparte:
        // sin el número separado no hay forma de saber si la caché pega.
        Assert.Equal(8120, reproducida.TokensDeEntrada);
        Assert.Equal(37, reproducida.TokensDeSalida);
        Assert.Equal(8000, reproducida.TokensDeCache);
        Assert.False(reproducida.EsSimulada);
    }

    [Fact]
    public async Task Un_cuerpo_sin_bloque_de_texto_se_reproduce_como_texto_vacio()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = new TransporteFalso(_ => TransporteFalso.Exito(texto: null));

        await Armar(transporte, regrabar: true).CompletarAsync(Solicitud, ct);

        using var explosivo = new TerminalQueExplota();
        var reproducida = await Armar(explosivo, regrabar: false).CompletarAsync(Solicitud, ct);

        // No es una caída: el modelo contestó. Tratarlo como falla de transporte
        // abriría el corte por algo que no es una falla de servicio.
        Assert.Equal(string.Empty, reproducida.Texto);
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>El adaptador real, sobre el pipeline real, con el terminal que se le dé.</summary>
    private ProveedorAnthropic Armar(
        HttpMessageHandler terminal, bool regrabar, string? directorio = null)
    {
        var reintento = new ReintentoDeTransporte(
            maximoDeIntentos: 3,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMilliseconds(4),
            new Random(20260905))
        {
            InnerHandler = terminal,
        };

        var grabador = new GrabadorDeCassettes(
            new AlmacenDeCassettes(directorio ?? _directorio),
            regrabar,
            Fixture,
            TimeProvider.System,
            new RegistroDeCapturas().Logger<GrabadorDeCassettes>())
        {
            InnerHandler = reintento,
        };

        var proveedor = new ProveedorAnthropic(
            new HttpClient(grabador, disposeHandler: false),
            "clave-de-prueba",
            "claude-opus-5",
            new RegistroDeCapturas().Logger<ProveedorAnthropic>());

        _abiertos.Add(reintento);
        _abiertos.Add(grabador);
        _abiertos.Add(proveedor);

        return proveedor;
    }

    /// <summary>Un terminal que revienta si alguien lo llama.</summary>
    /// <remarks>
    /// Es lo que convierte «se leyó el cassette» en algo verificable. Un terminal
    /// que respondiera bien haría que el test pasara igual sin haber leído nada.
    /// </remarks>
    private sealed class TerminalQueExplota : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken ct) =>
            throw new InvalidOperationException(
                "La reproducción llegó al transporte: el cassette no se leyó.");
    }

    public void Dispose()
    {
        foreach (var abierto in _abiertos)
        {
            abierto.Dispose();
        }

        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }
}
