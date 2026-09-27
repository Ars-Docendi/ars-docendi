using System.Globalization;
using System.Text;

namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Diccionarios estáticos de etiquetas y normalización de texto para el feed
/// unificado de auditoría (sistema-seccion-unificada, design.md D4/D5).
/// </summary>
/// <remarks>
/// Separado de <see cref="ServicioAuditoria"/> (tarea 2.1) porque estas tablas
/// las necesitan DOS lectores: el mapeo de <c>audit.change_log</c>
/// (<see cref="MapeadorEventoAuditoria"/>) y la búsqueda por etiqueta
/// (<see cref="RepositorioAuditoria"/>, design.md D4) parten del mismo
/// diccionario — una copia en cada lado se desincroniza en silencio.
/// </remarks>
public static class EtiquetasAuditoria
{
    public static readonly HashSet<string> CamposConValorSeguro = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "codigo", "scope", "estado", "status", "action", "novedad", "tipo_baja",
        "activo", "is_active", "es_sistema", "orden", "horas", "horas_investigacion",
        "horas_externas", "vigente_desde", "vigente_hasta", "created_at", "deleted_at",
        // Del rastro de administración del asistente (design.md D5): activo ya
        // está arriba; cupo y tope_mensual_usd son propios de esa fuente.
        "cupo", "tope_mensual_usd",
    };

    /// <summary>
    /// Campos booleanos seguros que además vienen como TEXTO ya normalizado
    /// (nunca como <c>JsonDocument</c>) desde alguna fuente — hoy, el rastro de
    /// administración del asistente (<c>MapearCampoDeAdministracion</c>). Ahí
    /// no hay tipo JSON del que leer «es booleano»: hace falta esta lista para
    /// saber cuándo «true»/«false» son «Sí»/«No» y no texto libre
    /// (sistema-seccion-unificada, design.md D5, «Humanized identity events»).
    /// Los campos que SÍ llegan como <c>JsonDocument</c>
    /// (<see cref="MapeadorEventoAuditoria.MapearCambio"/>) no la necesitan: el
    /// propio <c>JsonValueKind</c> ya dice si el valor es booleano.
    /// </summary>
    public static readonly HashSet<string> CamposBooleanos = new(StringComparer.OrdinalIgnoreCase)
    {
        "activo", "is_active", "es_sistema",
    };

    public static readonly HashSet<string> CamposPersonalesOSecretos = new(StringComparer.OrdinalIgnoreCase)
    {
        "documento", "cuil", "legajo", "nombre", "apellido", "fecha_nacimiento", "telefono",
        "upn", "display_name", "azure_oid", "email", "mail", "correo", "client_ip",
        "password", "token", "access_token", "refresh_token", "secret", "uri",
    };

    public static readonly Dictionary<string, string> EtiquetasModulos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity"] = "Identidad",
        ["designaciones"] = "Designaciones",
        ["portal"] = "Portal",
        ["asistente"] = "Asistente",
    };

    public static readonly Dictionary<string, string> EtiquetasObjetos = new(StringComparer.OrdinalIgnoreCase)
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

    public static readonly Dictionary<string, string> EtiquetasCampos = new(StringComparer.OrdinalIgnoreCase)
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
        // Campos del rastro de administración del asistente (design.md D5).
        ["cupo"] = "Cupo diario",
        ["tope_mensual_usd"] = "Tope mensual (USD)",
        ["razon"] = "Razón",
        // Resto de las tablas auditadas (audit.attach) — sin esto, Humanizar()
        // deja etiquetas crudas como «Carrera id» o el nombre en inglés sin
        // traducir (bug de UI reportado sobre identity.user_roles).
        ["id"] = "Identificador",
        ["carrera_id"] = "Carrera",
        ["rol_id"] = "Rol",
        ["permiso_id"] = "Permiso",
        ["pedido_id"] = "Solicitud",
        ["accion"] = "Acción",
        ["actor_id"] = "Actor",
        ["etapa"] = "Etapa",
        ["comentario"] = "Comentario",
        ["tipo"] = "Tipo",
        ["uri"] = "Enlace",
        ["abreviatura"] = "Abreviatura",
        ["numero"] = "Número",
        ["periodo_id"] = "Período",
        ["prioritario"] = "Prioritario",
        ["cargo_solicitado_id"] = "Cargo solicitado",
        ["dedicacion_solicitada"] = "Dedicación solicitada",
        ["justificacion"] = "Justificación",
        ["tipo_baja_detalle"] = "Detalle de baja",
        ["etapa_retorno"] = "Etapa de retorno",
        ["propietario_actual"] = "Propietario actual",
        ["snapshot"] = "Datos congelados",
        ["cargo_id"] = "Cargo",
        ["dedicacion"] = "Dedicación",
        ["origen_pedido_id"] = "Solicitud de origen",
        ["materia_id"] = "Materia",
        ["granted_at"] = "Otorgado el",
        ["granted_by"] = "Otorgado por",
        ["azure_oid"] = "Identificador de Azure AD",
        ["display_name"] = "Nombre para mostrar",
        ["last_login_at"] = "Último inicio de sesión",
        ["puesto"] = "Puesto",
        ["organizacion"] = "Organización",
        ["desde"] = "Desde",
        ["hasta"] = "Hasta",
        ["nivel"] = "Nivel",
        ["carrera"] = "Carrera",
        ["institucion"] = "Institución",
        ["emisor"] = "Emisor",
        ["fecha"] = "Fecha",
        ["vencimiento"] = "Vencimiento",
        ["rol"] = "Rol",
        ["doi"] = "DOI",
        ["fecha_carga"] = "Fecha de carga",
        ["termino"] = "Término",
        ["termino_norm"] = "Término normalizado",
        ["sugerido"] = "Sugerido",
        ["canonica_id"] = "Término canónico",
        ["usos"] = "Usos",
        ["habilidad_id"] = "Habilidad",
        ["proyecto_id"] = "Proyecto",
        ["perfil_id"] = "Perfil",
        ["mail"] = "Correo electrónico",
        ["carga_desde"] = "Carga desde",
        ["carga_hasta"] = "Carga hasta",
        ["impacto_desde"] = "Impacto desde",
        ["impacto_hasta"] = "Impacto hasta",
    };

    public static string Humanizar(string valor)
    {
        var palabras = valor
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (palabras.Length == 0) return valor;
        palabras[0] = char.ToUpperInvariant(palabras[0][0]) + palabras[0][1..];
        return string.Join(' ', palabras);
    }

    /// <summary>
    /// Normaliza texto para <c>q</c> (design.md D4): recorta, pasa a minúsculas
    /// y quita acentos. La misma normalización corre en C# (acá, para resolver
    /// qué etiquetas matchean) y en SQL (<c>unaccent(lower(...))</c>, design.md
    /// D4) — el resultado tiene que ser el mismo de los dos lados para que
    /// «lucia fernandez» encuentre «Lucía Fernández».
    /// </summary>
    public static string Normalizar(string valor)
    {
        var recortado = valor.Trim().ToLowerInvariant();
        var descompuesto = recortado.Normalize(NormalizationForm.FormD);
        var sinAcentos = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinAcentos.Append(caracter);
            }
        }

        return sinAcentos.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Las claves <c>schema.tabla</c> cuya etiqueta de objeto contiene
    /// <paramref name="qNormalizado"/> (design.md D4).
    /// </summary>
    /// <remarks>
    /// Devuelve la clave COMBINADA (no un par) a propósito: EF Core traduce
    /// <c>lista.Contains(columnaA + "." + columnaB)</c> a un <c>IN</c> normal,
    /// pero no sabe traducir <c>listaDeTuplas.Any(t =&gt; t.A == columnaA &amp;&amp;
    /// t.B == columnaB)</c> — lo intenta como subconsulta y falla en runtime.
    /// </remarks>
    public static IReadOnlyList<string> ObjetosQueMatchean(string qNormalizado) =>
        [.. EtiquetasObjetos
            .Where(par => Normalizar(par.Value).Contains(qNormalizado, StringComparison.Ordinal))
            .Select(par => par.Key)];

    /// <summary>Los schemas cuya etiqueta de módulo contiene <paramref name="qNormalizado"/>.</summary>
    public static IReadOnlyList<string> SchemasQueMatchean(string qNormalizado) =>
        [.. EtiquetasModulos
            .Where(par => Normalizar(par.Value).Contains(qNormalizado, StringComparison.Ordinal))
            .Select(par => par.Key)];

    /// <summary>Las claves de campo cuya etiqueta contiene <paramref name="qNormalizado"/>.</summary>
    public static IReadOnlyList<string> CamposQueMatchean(string qNormalizado) =>
        [.. EtiquetasCampos
            .Where(par => Normalizar(par.Value).Contains(qNormalizado, StringComparison.Ordinal))
            .Select(par => par.Key)];
}
