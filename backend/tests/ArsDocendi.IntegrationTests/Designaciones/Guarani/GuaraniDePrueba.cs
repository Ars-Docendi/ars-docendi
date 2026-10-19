using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Infrastructure.Guarani;

namespace ArsDocendi.IntegrationTests.Designaciones.Guarani;

/// <summary>
/// Guaraní falso a nivel HTTP: el cliente real arma la solicitud, autentica y parsea, así
/// que los tests ejercitan el mapeo verdadero sin red. Responde por ruta exacta.
/// </summary>
public sealed class GuaraniDePrueba : HttpMessageHandler
{
    public const string RutaPeriodos = "api/periodos-lectivos";
    public const string RutaComisiones = "api/comisiones?periodo_lectivo={periodoLectivo}";

    private readonly ConcurrentDictionary<string, Func<HttpResponseMessage>> _respuestas = new();
    public ConcurrentQueue<HttpRequestMessage> Solicitudes { get; } = new();

    public static string PeriodosJson => Leer("periodos_lectivos.json");
    public static string ComisionesJson => Leer("comisiones_periodo.json");

    public static GuaraniDePrueba ConFixtures() => new GuaraniDePrueba()
        .Responder(RutaPeriodos, PeriodosJson)
        .Responder(Comisiones("152"), ComisionesJson);

    public static string Comisiones(string periodo) =>
        RutaComisiones.Replace(OpcionesGuarani.MarcadorPeriodo, periodo, StringComparison.Ordinal);

    public GuaraniDePrueba Responder(string ruta, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _respuestas[ruta] = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return this;
    }

    public GuaraniDePrueba Fallar(string ruta, Func<HttpResponseMessage> respuesta)
    {
        _respuestas[ruta] = respuesta;
        return this;
    }

    public int Llamadas(string ruta) => Solicitudes.Count(s => s.RequestUri!.PathAndQuery.TrimStart('/') == ruta);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken ct)
    {
        Solicitudes.Enqueue(solicitud);
        var ruta = solicitud.RequestUri!.PathAndQuery.TrimStart('/');
        return _respuestas.TryGetValue(ruta, out var respuesta)
            ? Task.FromResult(respuesta())
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public static OpcionesGuarani Opciones(int timeoutSegundos = 60) => new()
    {
        UrlBase = "https://guarani.prueba.invalid/",
        Usuario = "usuario-tecnico",
        Contrasena = "secreto-de-prueba",
        RutaPeriodosLectivos = RutaPeriodos,
        RutaComisiones = RutaComisiones,
        TimeoutSegundos = timeoutSegundos,
    };

    public ClienteGuarani Cliente(OpcionesGuarani? opciones = null, ILogger<ClienteGuarani>? logger = null)
    {
        var valores = opciones ?? Opciones();
        var http = new HttpClient(this, disposeHandler: false)
        {
            BaseAddress = Uri.TryCreate(valores.UrlBase, UriKind.Absolute, out var url) ? url : null,
            Timeout = TimeSpan.FromSeconds(valores.TimeoutSegundos),
        };
        return new ClienteGuarani(http, Options.Create(valores), logger ?? new LoggerCapturado<ClienteGuarani>());
    }

    private static string Leer(string archivo)
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Join(directorio.FullName, "AGENTS.md")))
            directorio = directorio.Parent;
        if (directorio is null) throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return File.ReadAllText(Path.Join(
            directorio.FullName, "backend", "tests", "ArsDocendi.IntegrationTests", "Designaciones", "Guarani", archivo));
    }
}

/// <summary>Logger que guarda los mensajes formateados para verificar que no filtran secretos.</summary>
public sealed class LoggerCapturado<T> : ILogger<T>
{
    public ConcurrentQueue<string> Mensajes { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Mensajes.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception));
}
