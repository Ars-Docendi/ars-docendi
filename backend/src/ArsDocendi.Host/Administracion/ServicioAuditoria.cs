using System.Text.Json;
using ArsDocendi.Shared.Aplicacion;

namespace ArsDocendi.Host.Administracion;

public sealed class ServicioAuditoria(IRepositorioAuditoria repositorio)
{
    private const int TamanoPaginaPredeterminado = 50;
    private const int TamanoPaginaMaximo = 100;
    private static readonly HashSet<string> Acciones = ["INSERT", "UPDATE", "DELETE"];
    private static readonly HashSet<string> CamposPersonalesOSecretos = new(StringComparer.OrdinalIgnoreCase)
    {
        "documento", "cuil", "legajo", "nombre", "apellido", "fecha_nacimiento", "telefono",
        "upn", "display_name", "azure_oid", "azure_tid", "email", "mail", "correo", "client_ip",
        "password", "token", "access_token", "refresh_token", "secret", "uri",
    };
    private static readonly Dictionary<string, string> EtiquetasModulos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity"] = "Identidad",
        ["designaciones"] = "Designaciones",
        ["portal"] = "Portal",
    };
    private static readonly Dictionary<string, string> EtiquetasObjetos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity.personas"] = "Persona",
        ["identity.users"] = "Cuenta de usuario",
        ["identity.roles"] = "Rol",
        ["identity.permisos"] = "Permiso",
        ["identity.user_roles"] = "Asignación de rol",
        ["identity.rol_permisos"] = "Permiso de rol",
        ["designaciones.pedidos"] = "Solicitud",
        ["designaciones.designaciones"] = "Designación",
        ["designaciones.periodos"] = "Período",
        ["designaciones.cargos"] = "Cargo",
        ["designaciones.dedicaciones"] = "Dedicación",
        ["designaciones.pedido_historial"] = "Historial de solicitud",
        ["portal.perfiles"] = "Perfil",
        ["portal.contactos"] = "Contacto",
        ["portal.cvs"] = "Currículum",
        ["portal.experiencias"] = "Experiencia",
        ["portal.educaciones"] = "Educación",
        ["portal.certificaciones"] = "Certificación",
        ["portal.proyectos"] = "Proyecto",
        ["portal.proyecto_documentos"] = "Documento de proyecto",
        ["portal.habilidades"] = "Habilidad",
        ["portal.docente_habilidades"] = "Habilidad docente",
    };
    private static readonly Dictionary<string, string> EtiquetasCampos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "Código",
        ["codigo"] = "Código",
        ["scope"] = "Ámbito",
        ["estado"] = "Estado",
        ["status"] = "Estado",
        ["action"] = "Acción",
        ["novedad"] = "Novedad",
        ["tipo_baja"] = "Tipo de baja",
        ["activo"] = "Activo",
        ["is_active"] = "Activo",
        ["es_sistema"] = "Rol de sistema",
        ["orden"] = "Orden",
        ["horas"] = "Horas",
        ["horas_investigacion"] = "Horas de investigación",
        ["horas_externas"] = "Horas externas",
        ["vigente_desde"] = "Vigente desde",
        ["vigente_hasta"] = "Vigente hasta",
        ["created_at"] = "Fecha de creación",
        ["deleted_at"] = "Fecha de baja",
        ["documento"] = "Documento",
        ["cuil"] = "CUIL",
        ["legajo"] = "Legajo",
        ["nombre"] = "Nombre",
        ["apellido"] = "Apellido",
        ["fecha_nacimiento"] = "Fecha de nacimiento",
        ["telefono"] = "Teléfono",
        ["description"] = "Descripción",
        ["descripcion"] = "Descripción",
        ["name"] = "Nombre",
        ["email"] = "Correo electrónico",
        ["upn"] = "Identificador de cuenta",
        ["persona_id"] = "Persona vinculada",
        ["user_id"] = "Usuario",
        ["role_id"] = "Rol",
        ["request_id"] = "Solicitud",
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
            Actor = filtros.Actor?.Trim(),
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
        if (filtros.RowPk?.TrimStart().StartsWith('{') == true)
        {
            try
            {
                using var clave = JsonDocument.Parse(filtros.RowPk);
                var campos = clave.RootElement.EnumerateObject().ToArray();
                if (campos.Length == 0 || campos.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != campos.Length
                    || campos.Any(c => c.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)))
                    errores["rowPk"] = ["La clave completa requiere un objeto JSON no vacío de componentes escalares."];
            }
            catch (JsonException)
            {
                errores["rowPk"] = ["La clave completa debe ser un objeto JSON válido."];
            }
        }
        if (filtros.Actor?.Trim().Length > 100)
            errores["actor"] = ["No puede superar 100 caracteres."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "Revisá los filtros de auditoría.",
                errores);
        }
    }

    private static EventoAuditoriaDto Mapear(RegistroAuditoria evento)
    {
        var registro = evento.Registro;
        using var anterior = Parsear(registro.FilaAnterior);
        using var nueva = Parsear(registro.FilaNueva);
        var columnas = registro.ColumnasCambiadas?.Distinct(StringComparer.Ordinal).ToArray()
            ?? ObtenerClaves(anterior, nueva);
        var cambios = columnas
            .Order(StringComparer.Ordinal)
            .Select(campo => MapearCambio(registro.NombreSchema, registro.NombreTabla, campo, anterior, nueva))
            .ToArray();
        var accionEtiqueta = registro.Accion switch
        {
            "INSERT" => "Alta",
            "UPDATE" => "Actualización",
            "DELETE" => "Eliminación física",
            _ => registro.Accion,
        };
        var modulo = EtiquetasModulos.TryGetValue(registro.NombreSchema, out var etiquetaModulo)
            ? etiquetaModulo
            : Humanizar(registro.NombreSchema);
        var objeto = EtiquetasObjetos.TryGetValue($"{registro.NombreSchema}.{registro.NombreTabla}", out var etiquetaObjeto)
            ? etiquetaObjeto
            : Humanizar(registro.NombreTabla);
        var resumen = cambios.Length == 0
            ? $"{accionEtiqueta} de {objeto.ToLowerInvariant()}"
            : $"{accionEtiqueta} de {objeto.ToLowerInvariant()} · {string.Join(", ", cambios.Select(cambio => cambio.EtiquetaCampo))}";

        if (evento.FormatoClave == "legacy-group")
            resumen += " · Agrupación histórica (clave parcial; no es una relación precisa)";
        else if (evento.FormatoClave == "legacy-recovered")
            resumen += " · Clave legada recuperada en lectura";

        return new EventoAuditoriaDto(
            registro.Id,
            registro.NombreSchema,
            registro.NombreTabla,
            evento.FormatoClave is "composite-v1" or "legacy-recovered"
                ? evento.ClaveCompleta ?? registro.ClaveFila
                : registro.ClaveFila,
            registro.Accion,
            registro.CambiadoEn,
            registro.CambiadoPor,
            registro.RequestId,
            columnas.Order(StringComparer.Ordinal).ToArray(),
            cambios,
            ResolverActor(evento),
            accionEtiqueta,
            modulo,
            objeto,
            resumen);
    }

    private static string ResolverActor(RegistroAuditoria evento)
    {
        string actor;
        if (!string.IsNullOrWhiteSpace(evento.ApellidoPersona)
            && !string.IsNullOrWhiteSpace(evento.NombrePersona))
            actor = $"{evento.ApellidoPersona.Trim()}, {evento.NombrePersona.Trim()}";
        else if (!string.IsNullOrWhiteSpace(evento.ApellidoPersona)) actor = evento.ApellidoPersona.Trim();
        else if (!string.IsNullOrWhiteSpace(evento.NombrePersona)) actor = evento.NombrePersona.Trim();
        else actor = string.IsNullOrWhiteSpace(evento.NombreCuenta)
            ? "Actor no identificado"
            : evento.NombreCuenta.Trim();
        // Un display_name legado puede ser el UPN; no convertirlo en correo visible.
        return actor.Contains('@') ? "Actor no identificado" : actor;
    }

    private static CambioAuditoriaDto MapearCambio(
        string schema,
        string tabla,
        string campo,
        JsonDocument? anterior,
        JsonDocument? nueva)
    {
        var etiquetaCampo = EtiquetasCampos.TryGetValue(campo, out var etiqueta)
            ? etiqueta
            : Humanizar(campo);
        if (CamposPersonalesOSecretos.Contains(campo) || !PoliticaValoresAuditoria.Permite(schema, tabla, campo))
            return new CambioAuditoriaDto(campo, null, null, true, etiquetaCampo);
        if (!IntentarObtenerValor(anterior, campo, out var valorAnterior)
            || !IntentarObtenerValor(nueva, campo, out var valorNuevo))
            return new CambioAuditoriaDto(campo, null, null, true, etiquetaCampo);
        return new CambioAuditoriaDto(campo, valorAnterior, valorNuevo, false, etiquetaCampo);
    }

    private static string Humanizar(string valor)
    {
        var palabras = valor
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (palabras.Length == 0) return valor;
        palabras[0] = char.ToUpperInvariant(palabras[0][0]) + palabras[0][1..];
        return string.Join(' ', palabras);
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

    private static IEnumerable<string> Claves(JsonDocument? documento)
    {
        if (documento is null || documento.RootElement.ValueKind != JsonValueKind.Object) return [];
        return documento.RootElement.EnumerateObject().Select(p => p.Name);
    }

    private static JsonDocument? Parsear(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);
}
