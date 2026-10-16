using System.Globalization;
using System.Text.Json;

namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Actor, acción, enmascarado y resumen — compartido por las DOS fuentes del
/// feed unificado (<c>audit.change_log</c> y el rastro de administración del
/// asistente) (sistema-seccion-unificada, design.md D5/D6, tarea 2.1).
/// </summary>
public static class MapeadorEventoAuditoria
{
    public const string TipoActorPersona = "persona";
    public const string TipoActorProceso = "proceso";
    public const string TipoActorNoIdentificado = "no_identificado";

    /// <summary>Zona horaria institucional (design.md D11) — la misma que usa el frontend.</summary>
    public static readonly TimeZoneInfo ZonaInstitucion =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    /// <summary>Campos seguros TIMESTAMPTZ: fecha y hora, «d/m/aaaa HH:mm:ss».</summary>
    private static readonly HashSet<string> CamposDeFechaHora =
        new(StringComparer.OrdinalIgnoreCase) { "created_at", "deleted_at" };

    /// <summary>Campos seguros DATE (sin hora): «d/m/aaaa».</summary>
    private static readonly HashSet<string> CamposDeFecha =
        new(StringComparer.OrdinalIgnoreCase) { "vigente_desde", "vigente_hasta" };

    /// <summary>
    /// El actor legible y su <c>TipoActor</c>, por la tabla de evidencia de
    /// design.md D6: «Proceso automático» exige la AUSENCIA de contexto de
    /// solicitud, no sólo un actor nulo.
    /// </summary>
    public static (string Actor, string TipoActor) ResolverActor(
        Guid? cambiadoPor,
        string? requestId,
        string? nombreCuenta,
        string? nombrePersona,
        string? apellidoPersona)
    {
        if (!string.IsNullOrWhiteSpace(nombrePersona) && !string.IsNullOrWhiteSpace(apellidoPersona))
            return ($"{nombrePersona.Trim()} {apellidoPersona.Trim()}", TipoActorPersona);
        if (!string.IsNullOrWhiteSpace(nombrePersona))
            return (nombrePersona.Trim(), TipoActorPersona);
        if (!string.IsNullOrWhiteSpace(apellidoPersona))
            return (apellidoPersona.Trim(), TipoActorPersona);
        if (!string.IsNullOrWhiteSpace(nombreCuenta))
            return (nombreCuenta.Trim(), TipoActorPersona);

        // Ni persona ni cuenta resolvieron. La evidencia que distingue las dos
        // filas restantes de D6 es el request_id: sin ÉL tampoco, nadie hizo
        // el pedido por HTTP — migración, seed o proceso de fondo.
        return cambiadoPor is null && string.IsNullOrWhiteSpace(requestId)
            ? ("Proceso automático", TipoActorProceso)
            : ("Actor no identificado", TipoActorNoIdentificado);
    }

    /// <summary>Alta / Cambio / Eliminación (design.md D6) — nunca el código crudo salvo lo desconocido.</summary>
    public static string EtiquetaDeAccion(string accion) => accion switch
    {
        "INSERT" => "Alta",
        "UPDATE" => "Cambio",
        "DELETE" => "Eliminación",
        _ => accion,
    };

    public static string ResolverModulo(string schema) =>
        EtiquetasAuditoria.EtiquetasModulos.TryGetValue(schema, out var etiqueta)
            ? etiqueta
            : EtiquetasAuditoria.Humanizar(schema);

    public static string ResolverObjeto(string schema, string tabla) =>
        EtiquetasAuditoria.EtiquetasObjetos.TryGetValue($"{schema}.{tabla}", out var etiqueta)
            ? etiqueta
            : EtiquetasAuditoria.Humanizar(tabla);

    /// <summary>
    /// El resumen de un evento de <c>change_log</c> (design.md D5, tarea 2.3):
    /// una UPDATE de EXACTAMENTE un campo seguro muestra los valores; todo lo
    /// demás —un INSERT o un DELETE, enmascarado, sin clasificar o más de un
    /// campo— se queda genérico. La forma con valores es exclusiva de UPDATE:
    /// un ALTA no tiene «antes» que mostrar, y un DELETE físico no lo necesita.
    /// </summary>
    /// <param name="objeto">
    /// La etiqueta a mostrar cuando SÍ hay valores — puede nombrar al sujeto
    /// (design.md D5, «Humanized identity events», tarea 2.9), p. ej. «Cuenta
    /// de usuario de Paula Gómez».
    /// </param>
    /// <param name="objetoGenerico">
    /// La etiqueta base, SIN nombre, para la forma genérica sin valores —
    /// bajarla a minúsculas es seguro porque nunca lleva un nombre propio
    /// adentro (design.md D5, tarea 2.9).
    /// </param>
    public static string ResumenDeCambio(
        string accion,
        string accionEtiqueta,
        string objeto,
        string objetoGenerico,
        string rowPk,
        IReadOnlyList<CambioAuditoriaDto> cambios)
    {
        if (accion == "UPDATE" && cambios.Count == 1 && !cambios[0].Oculto)
        {
            var unico = cambios[0];
            var sufijoFila = EsNumerico(rowPk) ? $" #{rowPk}" : string.Empty;
            return $"{objeto}{sufijoFila}: {unico.EtiquetaCampo} {unico.ValorAnterior} → {unico.ValorNuevo}";
        }

        return cambios.Count == 0
            ? $"{accionEtiqueta} de {objetoGenerico.ToLowerInvariant()}"
            : $"{accionEtiqueta} de {objetoGenerico.ToLowerInvariant()} · "
                + string.Join(", ", cambios.Select(c => c.EtiquetaCampo));
    }

    private static bool EsNumerico(string valor) => long.TryParse(valor, out _);

    /// <summary>
    /// Un campo es de valor seguro cuando está en la lista blanca Y no está en
    /// la lista de datos personales/secretos — la segunda gana ante cualquier
    /// solapamiento, aunque hoy no lo hay.
    /// </summary>
    public static bool EsValorSeguro(string campo) =>
        !EtiquetasAuditoria.CamposPersonalesOSecretos.Contains(campo)
        && EtiquetasAuditoria.CamposConValorSeguro.Contains(campo);

    private static string EtiquetaDeCampo(string campo) =>
        EtiquetasAuditoria.EtiquetasCampos.TryGetValue(campo, out var etiqueta)
            ? etiqueta
            : EtiquetasAuditoria.Humanizar(campo);

    /// <summary>Enmascara según las dos listas: personal/secreto u OCULTO; el resto muestra valor.</summary>
    public static CambioAuditoriaDto MapearCambio(string campo, JsonDocument? anterior, JsonDocument? nueva)
    {
        var etiquetaCampo = EtiquetaDeCampo(campo);
        if (!EsValorSeguro(campo))
            return new CambioAuditoriaDto(campo, null, null, true, etiquetaCampo);
        if (!IntentarObtenerValor(anterior, campo, out var valorAnterior)
            || !IntentarObtenerValor(nueva, campo, out var valorNuevo))
            return new CambioAuditoriaDto(campo, null, null, true, etiquetaCampo);
        return new CambioAuditoriaDto(campo, valorAnterior, valorNuevo, false, etiquetaCampo);
    }

    /// <summary>
    /// La misma máscara que <see cref="MapearCambio"/>, para un campo YA
    /// normalizado a texto por el módulo del asistente (sin JSON que parsear,
    /// design.md D1/D5) — <c>cupo</c>/<c>tope_mensual_usd</c>/<c>activo</c> son
    /// seguros, <c>razon</c> queda sin clasificar y por lo tanto enmascarado.
    /// </summary>
    public static CambioAuditoriaDto MapearCampoDeAdministracion(Modules.Asistente.Contracts.CampoDeAdministracion campo)
    {
        var etiquetaCampo = EtiquetaDeCampo(campo.Campo);
        return EsValorSeguro(campo.Campo)
            ? new CambioAuditoriaDto(
                campo.Campo,
                FormatearBooleanoDeTexto(campo.Campo, campo.ValorAnterior),
                FormatearBooleanoDeTexto(campo.Campo, campo.ValorNuevo),
                false,
                etiquetaCampo)
            : new CambioAuditoriaDto(campo.Campo, null, null, true, etiquetaCampo);
    }

    /// <summary>
    /// «true»/«false» → «Sí»/«No» para un valor booleano YA normalizado a texto
    /// (design.md D5, «Humanized identity events», tarea 2.9) — ver
    /// <see cref="EtiquetasAuditoria.CamposBooleanos"/>. Cualquier otro campo o
    /// valor pasa sin tocar.
    /// </summary>
    private static string? FormatearBooleanoDeTexto(string campo, string? valor)
    {
        if (valor is null || !EtiquetasAuditoria.CamposBooleanos.Contains(campo)) return valor;
        if (valor.Equals("true", StringComparison.OrdinalIgnoreCase)) return "Sí";
        if (valor.Equals("false", StringComparison.OrdinalIgnoreCase)) return "No";
        return valor;
    }

    /// <summary>
    /// El nombre natural «Nombre Apellido» de una persona resuelta por
    /// <see cref="IRepositorioAuditoria"/> (design.md D6) — compartido por el
    /// actor, el usuario afectado del asistente
    /// (<see cref="FuenteAuditoriaAsistente"/>) y el sujeto humanizado de
    /// <c>identity.users</c>/<c>identity.personas</c>/<c>identity.user_roles</c>
    /// (design.md D5, «Humanized identity events», tarea 2.9): es LA MISMA
    /// resolución, no una copia que pueda desincronizarse.
    /// </summary>
    public static string? NombreNaturalDePersona(NombreResuelto nombre)
    {
        if (!string.IsNullOrWhiteSpace(nombre.NombrePersona) && !string.IsNullOrWhiteSpace(nombre.ApellidoPersona))
            return $"{nombre.NombrePersona.Trim()} {nombre.ApellidoPersona.Trim()}";
        if (!string.IsNullOrWhiteSpace(nombre.NombrePersona)) return nombre.NombrePersona.Trim();
        if (!string.IsNullOrWhiteSpace(nombre.ApellidoPersona)) return nombre.ApellidoPersona.Trim();
        return string.IsNullOrWhiteSpace(nombre.NombreCuenta) ? null : nombre.NombreCuenta.Trim();
    }

    /// <summary>Nombres de columna presentes en cualquiera de los dos snapshots.</summary>
    public static string[] ObtenerClaves(JsonDocument? anterior, JsonDocument? nueva) =>
        Claves(anterior).Concat(Claves(nueva)).Distinct(StringComparer.Ordinal).ToArray();

    private static IEnumerable<string> Claves(JsonDocument? documento)
    {
        if (documento is null || documento.RootElement.ValueKind != JsonValueKind.Object) return [];
        return documento.RootElement.EnumerateObject().Select(p => p.Name);
    }

    /// <summary>
    /// Un campo cuyo «antes» Y «después» están ausentes o son <c>null</c> en el
    /// JSON crudo — antes de cualquier enmascarado — no aporta nada al panel
    /// («Fecha de baja: — → —» en un ALTA es ruido). Se evalúa sobre el JSON
    /// crudo, no sobre el <see cref="CambioAuditoriaDto"/> ya mapeado: un campo
    /// enmascarado siempre lleva valores <c>null</c> en el DTO aunque el dato
    /// real no lo sea.
    /// </summary>
    public static bool EsAmbosAusentes(JsonDocument? anterior, JsonDocument? nueva, string campo) =>
        EsAusente(anterior, campo) && EsAusente(nueva, campo);

    private static bool EsAusente(JsonDocument? documento, string campo)
    {
        if (documento is null || documento.RootElement.ValueKind != JsonValueKind.Object) return true;
        return !documento.RootElement.TryGetProperty(campo, out var valor)
            || valor.ValueKind == JsonValueKind.Null;
    }

    public static JsonDocument? Parsear(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);

    private static bool IntentarObtenerValor(JsonDocument? documento, string campo, out string? resultado)
    {
        resultado = null;
        if (documento is null || !documento.RootElement.TryGetProperty(campo, out var valor)) return true;
        switch (valor.ValueKind)
        {
            case JsonValueKind.String:
                var texto = valor.GetString();
                if (texto is null) return true;
                var formateado = FormatearValorDeFecha(campo, texto);
                if (formateado is not null)
                {
                    resultado = formateado;
                    return true;
                }
                if (texto.Length > 120) return false;
                resultado = texto;
                return true;
            // «Sí»/«No» y no «true»/«false» (design.md D5, «Humanized identity
            // events», tarea 2.9): acá el tipo JSON ya dice que es booleano, así
            // que no hace falta una lista de nombres de campo como en
            // FormatearBooleanoDeTexto — cualquier campo seguro que sea booleano
            // en la base queda cubierto.
            case JsonValueKind.True:
                resultado = "Sí";
                return true;
            case JsonValueKind.False:
                resultado = "No";
                return true;
            case JsonValueKind.Number:
                resultado = valor.GetRawText();
                return true;
            // Un null de JSON tiene que quedar null en el DTO, nunca el texto
            // literal «null» (bug de UI reportado): «—» lo resuelve el panel.
            case JsonValueKind.Null:
                resultado = null;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// «26/9/2026 22:52:30» (hora de Buenos Aires) para un TIMESTAMPTZ seguro, o
    /// «1/3/2026» para un DATE seguro (design.md D5/D11) — <c>null</c> si el
    /// campo no es de fecha o el texto no parsea, para que el llamador use el
    /// valor crudo como antes.
    /// </summary>
    private static string? FormatearValorDeFecha(string campo, string texto)
    {
        if (CamposDeFechaHora.Contains(campo)
            && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante))
        {
            var local = TimeZoneInfo.ConvertTime(instante, ZonaInstitucion);
            return local.ToString("d/M/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (CamposDeFecha.Contains(campo)
            && DateOnly.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            return fecha.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
        }

        return null;
    }
}
