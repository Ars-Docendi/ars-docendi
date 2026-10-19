using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Infrastructure.Guarani;

/// <summary>
/// Único punto que conoce el contrato de la API de Guaraní. Traduce toda falla a una
/// <see cref="ExcepcionGuarani"/> con causa genérica. Los logs registran causa, status y
/// período: nunca credenciales ni el cuerpo de la respuesta, que trae datos de docentes.
/// </summary>
public sealed class ClienteGuarani(
    HttpClient http,
    IOptions<OpcionesGuarani> opciones,
    ILogger<ClienteGuarani> logger)
{
    public bool EstaConfigurado => opciones.Value.EstaConfigurada;

    public async Task<IReadOnlyList<PeriodoLectivoGuarani>> ListarPeriodosLectivosAsync(CancellationToken ct)
    {
        using var json = await ObtenerArregloAsync(opciones.Value.RutaPeriodosLectivos, null, ct);
        var periodos = new List<PeriodoLectivoGuarani>();
        foreach (var fila in json.RootElement.EnumerateArray())
        {
            var id = Texto(fila, "periodo_lectivo");
            var nombre = Texto(fila, "periodo_lectivo_nombre");
            if (id is null || nombre is null || !int.TryParse(Texto(fila, "periodo_lectivo_anio"), out var anio))
                throw Invalida("Período lectivo incompleto en la respuesta.", null);
            periodos.Add(new PeriodoLectivoGuarani(id, nombre, anio));
        }
        return periodos;
    }

    public async Task<IReadOnlyList<FilaComisionGuarani>> ListarComisionesAsync(
        string periodoLectivoId, CancellationToken ct)
    {
        var ruta = opciones.Value.RutaComisiones.Replace(
            OpcionesGuarani.MarcadorPeriodo, Uri.EscapeDataString(periodoLectivoId), StringComparison.Ordinal);
        using var json = await ObtenerArregloAsync(ruta, periodoLectivoId, ct);
        return json.RootElement.EnumerateArray().Select(f => new FilaComisionGuarani(
            Texto(f, "comision"),
            Texto(f, "asignacion"),
            Texto(f, "comision_nombre"),
            Texto(f, "comision_ubicacion"),
            Texto(f, "elemento_codigo"),
            Texto(f, "elemento_nombre"),
            Texto(f, "propuesta_nombre"),
            Texto(f, "propuesta_codigo"),
            Texto(f, "dia_semana"),
            Texto(f, "hora_inicio"),
            Texto(f, "hora_finalizacion"),
            Texto(f, "aula"),
            Texto(f, "modalidad_codigo"),
            Texto(f, "modalidad_descripcion"),
            Texto(f, "docente_nombre"),
            Texto(f, "docente_tipo_documento"),
            Texto(f, "docente_documento"),
            Texto(f, "docente_responsabilidad"))).ToArray();
    }

    private async Task<JsonDocument> ObtenerArregloAsync(string ruta, string? periodo, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{opciones.Value.Usuario}:{opciones.Value.Contrasena}")));
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw Fallo(CausasImportacion.Timeout, "Guaraní no respondió a tiempo.", null, periodo);
        }
        catch (HttpRequestException error)
        {
            throw Fallo(CausasImportacion.NoDisponible, "No se pudo contactar a Guaraní.", (int?)error.StatusCode, periodo);
        }

        using (respuesta)
        {
            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw Fallo(CausasImportacion.Autenticacion, "Guaraní rechazó las credenciales configuradas.",
                    (int)respuesta.StatusCode, periodo);
            if (!respuesta.IsSuccessStatusCode)
                throw Fallo(CausasImportacion.NoDisponible, "Guaraní respondió con error.",
                    (int)respuesta.StatusCode, periodo);
            try
            {
                await using var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
                var json = await JsonDocument.ParseAsync(cuerpo, cancellationToken: ct);
                if (json.RootElement.ValueKind != JsonValueKind.Array
                    || json.RootElement.EnumerateArray().Any(f => f.ValueKind != JsonValueKind.Object))
                {
                    json.Dispose();
                    throw Invalida("La respuesta no es una lista de registros.", periodo);
                }
                return json;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw Fallo(CausasImportacion.Timeout, "Guaraní no respondió a tiempo.", null, periodo);
            }
            catch (JsonException)
            {
                throw Invalida("La respuesta de Guaraní no es JSON válido.", periodo);
            }
        }
    }

    private ExcepcionGuarani Invalida(string mensaje, string? periodo) =>
        Fallo(CausasImportacion.RespuestaInvalida, mensaje, null, periodo);

    private ExcepcionGuarani Fallo(string causa, string mensaje, int? status, string? periodo)
    {
        logger.LogWarning(
            "Integración Guaraní falló con causa {Causa} (status {Status}) para el período {Periodo}",
            causa, status, periodo);
        return new ExcepcionGuarani(causa, mensaje);
    }

    /// <summary>Lee una propiedad como texto recortado; números se toman literales y vacío es null.</summary>
    private static string? Texto(JsonElement fila, string propiedad)
    {
        if (!fila.TryGetProperty(propiedad, out var valor)) return null;
        var texto = valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => throw new ExcepcionGuarani(CausasImportacion.RespuestaInvalida,
                "La respuesta de Guaraní tiene un campo con formato inesperado."),
        };
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
