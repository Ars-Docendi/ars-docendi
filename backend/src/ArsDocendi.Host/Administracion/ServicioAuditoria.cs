using System.Text.Json;
using ArsDocendi.Shared.Aplicacion;

namespace ArsDocendi.Host.Administracion;

public sealed class ServicioAuditoria(IRepositorioAuditoria repositorio)
{
    private const int TamanoPaginaPredeterminado = 50;
    private const int TamanoPaginaMaximo = 100;
    private static readonly HashSet<string> Acciones = ["INSERT", "UPDATE", "DELETE"];
    private static readonly HashSet<string> CamposConValorSeguro = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "codigo", "scope", "estado", "status", "action", "novedad", "tipo_baja",
        "activo", "is_active", "es_sistema", "orden", "horas", "horas_investigacion",
        "horas_externas", "vigente_desde", "vigente_hasta", "created_at", "deleted_at",
    };
    private static readonly HashSet<string> CamposPersonalesOSecretos = new(StringComparer.OrdinalIgnoreCase)
    {
        "documento", "cuil", "legajo", "nombre", "apellido", "fecha_nacimiento", "telefono",
        "upn", "display_name", "azure_oid", "email", "mail", "correo", "client_ip",
        "password", "token", "access_token", "refresh_token", "secret", "uri",
    };

    public async Task<PaginaAuditoriaDto> ListarAsync(ConsultaAuditoriaDto filtros, CancellationToken ct)
    {
        Validar(filtros);
        var normalizados = filtros with
        {
            Accion = filtros.Accion?.Trim().ToUpperInvariant(),
            Schema = filtros.Schema?.Trim().ToLowerInvariant(),
            Tabla = filtros.Tabla?.Trim().ToLowerInvariant(),
            RowPk = filtros.RowPk?.Trim(),
            TamanoPagina = filtros.TamanoPagina == 0 ? TamanoPaginaPredeterminado : filtros.TamanoPagina,
        };
        var pagina = await repositorio.ListarAsync(normalizados, ct);
        return new PaginaAuditoriaDto(
            pagina.Registros.Select(Mapear).ToArray(),
            normalizados.Pagina,
            normalizados.TamanoPagina,
            pagina.Total);
    }

    private static void Validar(ConsultaAuditoriaDto filtros)
    {
        var errores = new Dictionary<string, string[]>();
        if (filtros.Desde is { } desde && filtros.Hasta is { } hasta && desde > hasta)
            errores["hasta"] = ["Debe ser igual o posterior a desde."];
        if (filtros.Pagina < 1)
            errores["pagina"] = ["Debe ser un entero positivo."];
        if (filtros.TamanoPagina is < 0 or > TamanoPaginaMaximo)
            errores["tamanoPagina"] = [$"Debe estar entre 1 y {TamanoPaginaMaximo}."];
        if (filtros.TamanoPagina == 0 && errores.Count == 0)
            filtros = filtros with { TamanoPagina = TamanoPaginaPredeterminado };
        if ((long)(filtros.Pagina - 1) * Math.Max(filtros.TamanoPagina, TamanoPaginaPredeterminado) > int.MaxValue)
            errores["pagina"] = ["La página solicitada excede el límite consultable."];
        if (filtros.Accion is { Length: > 0 } accion && !Acciones.Contains(accion.Trim().ToUpperInvariant()))
            errores["accion"] = ["La acción debe ser INSERT, UPDATE o DELETE."];
        if (filtros.Schema?.Trim().Length > 63)
            errores["schema"] = ["No puede superar 63 caracteres."];
        if (filtros.Tabla?.Trim().Length > 63)
            errores["tabla"] = ["No puede superar 63 caracteres."];
        if (filtros.RowPk?.Trim().Length > 200)
            errores["rowPk"] = ["No puede superar 200 caracteres."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "Revisá los filtros de auditoría.",
                errores);
        }
    }

    private static EventoAuditoriaDto Mapear(ArsDocendi.Shared.Identity.RegistroCambio registro)
    {
        using var anterior = Parsear(registro.FilaAnterior);
        using var nueva = Parsear(registro.FilaNueva);
        var columnas = registro.ColumnasCambiadas?.Distinct(StringComparer.Ordinal).ToArray()
            ?? ObtenerClaves(anterior, nueva);
        var cambios = columnas
            .Order(StringComparer.Ordinal)
            .Select(campo => MapearCambio(campo, anterior, nueva))
            .ToArray();
        return new EventoAuditoriaDto(
            registro.Id,
            registro.NombreSchema,
            registro.NombreTabla,
            registro.ClaveFila,
            registro.Accion,
            registro.CambiadoEn,
            registro.CambiadoPor,
            registro.RequestId,
            columnas.Order(StringComparer.Ordinal).ToArray(),
            cambios);
    }

    private static CambioAuditoriaDto MapearCambio(
        string campo,
        JsonDocument? anterior,
        JsonDocument? nueva)
    {
        if (CamposPersonalesOSecretos.Contains(campo) || !CamposConValorSeguro.Contains(campo))
            return new CambioAuditoriaDto(campo, null, null, true);
        if (!IntentarObtenerValor(anterior, campo, out var valorAnterior)
            || !IntentarObtenerValor(nueva, campo, out var valorNuevo))
            return new CambioAuditoriaDto(campo, null, null, true);
        return new CambioAuditoriaDto(campo, valorAnterior, valorNuevo, false);
    }

    private static bool IntentarObtenerValor(JsonDocument? documento, string campo, out string? resultado)
    {
        resultado = null;
        if (documento is null || !documento.RootElement.TryGetProperty(campo, out var valor)) return true;
        switch (valor.ValueKind)
        {
            case JsonValueKind.String:
                var texto = valor.GetString();
                if (texto is null) return true;
                if (texto.Length > 120) return false;
                resultado = texto;
                return true;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                resultado = valor.GetRawText();
                return true;
            default:
                return false;
        }
    }

    private static string[] ObtenerClaves(JsonDocument? anterior, JsonDocument? nueva) =>
        (Claves(anterior).Concat(Claves(nueva))).Distinct(StringComparer.Ordinal).ToArray();

    private static IEnumerable<string> Claves(JsonDocument? documento) =>
        documento?.RootElement.ValueKind == JsonValueKind.Object
            ? documento.RootElement.EnumerateObject().Select(p => p.Name)
            : [];

    private static JsonDocument? Parsear(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);
}
