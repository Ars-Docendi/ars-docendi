using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Proveedor de modelo con guion: devuelve respuestas fijas y guarda lo que le
/// pidieron.
/// </summary>
/// <remarks>
/// Es lo que hace testeable el carril sin proveedor real. Devolver una consulta
/// fija ejercita todo el pipeline —validación, ejecución, guard, redacción— salvo
/// la calidad de la traducción, que es exactamente lo que mide la épica de
/// evaluación y lo único que necesita una clave.
///
/// Guardar las solicitudes no es un detalle: buena parte de lo que hay que probar
/// —que la temperatura es cero, que el prefijo no cambia entre turnos, que la
/// fecha viaja en el mensaje y no en el prefijo— solo se puede afirmar mirando lo
/// que se le mandó al modelo.
/// </remarks>
public sealed class ProveedorGuionado(params string[] respuestas) : IProveedorDeModelo
{
    private readonly List<SolicitudAlModelo> _recibidas = [];
    private int _entregadas;

    public string Nombre => "guionado";

    public bool EsSimulado => true;

    /// <summary>Todo lo que se le pidió, en orden.</summary>
    public IReadOnlyList<SolicitudAlModelo> Recibidas => _recibidas;

    /// <summary>Cuántas veces se lo llamó.</summary>
    public int Llamadas => _recibidas.Count;

    /// <summary>Excepción que lanza en vez de responder, si se le pone una.</summary>
    public Exception? Falla { get; init; }

    public Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ct.ThrowIfCancellationRequested();

        _recibidas.Add(solicitud);

        if (Falla is not null)
        {
            throw Falla;
        }

        // Después de agotar el guion repite la última: un turno que llama de más
        // debe fallar por el techo, no por quedarse sin respuestas.
        var texto = respuestas.Length == 0
            ? "{}"
            : respuestas[Math.Min(_entregadas, respuestas.Length - 1)];

        _entregadas++;

        return Task.FromResult(new RespuestaDelModelo(texto, 100, 50, EsSimulada: true));
    }

    /// <summary>Arma la respuesta JSON de una generación contestable.</summary>
    public static string Generacion(string sql, string razonamiento = "Interpreté la pregunta.") =>
        $$"""
        {"es_contestable": true, "sql": {{System.Text.Json.JsonSerializer.Serialize(sql)}},
         "razonamiento": {{System.Text.Json.JsonSerializer.Serialize(razonamiento)}},
         "categoria": "cruce_de_tablas"}
        """;

    /// <summary>Arma la respuesta JSON de una generación que se abstiene.</summary>
    public static string NoContestable(string razonamiento = "La pregunta excede lo que puedo consultar.") =>
        $$"""
        {"es_contestable": false, "sql": null,
         "razonamiento": {{System.Text.Json.JsonSerializer.Serialize(razonamiento)}},
         "categoria": "no_contestable"}
        """;
}
