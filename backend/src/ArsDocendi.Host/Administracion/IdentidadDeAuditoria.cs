using System.Text.Json;
using ArsDocendi.Shared.Identity;

namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Etiqueta de objeto y resumen humanizados para eventos de <c>change_log</c>
/// cuyo sujeto es una persona — <c>identity.users</c>, <c>identity.personas</c>
/// e <c>identity.user_roles</c> (sistema-seccion-unificada, design.md D5,
/// «Humanized identity events», tarea 2.9).
/// </summary>
/// <remarks>
/// Separado de <see cref="MapeadorEventoAuditoria"/> porque esta clase necesita
/// DOS resoluciones que el resto del mapeo no usa —nombres de sujeto y nombres
/// de rol— y una recolección de identificadores que corre sobre la PÁGINA
/// entera antes de mapear evento por evento (<see cref="ServicioAuditoria"/>),
/// no sobre un evento aislado.
/// </remarks>
public static class IdentidadDeAuditoria
{
    private const string Schema = "identity";
    private const string TablaUsuarios = "users";
    private const string TablaPersonas = "personas";
    private const string TablaAsignacionesDeRol = "user_roles";

    /// <summary>
    /// Los identificadores a resolver de una página completa de
    /// <c>change_log</c>, para UNA sola consulta batched por página (design.md
    /// D5) en vez de una por fila: <c>row_pk</c> de <c>identity.users</c>/
    /// <c>identity.personas</c>, y el <c>user_id</c>/<c>role_id</c> del
    /// snapshot de <c>identity.user_roles</c> — ese <c>user_id</c> es un id
    /// interno para esta búsqueda, nunca un valor a mostrar.
    /// </summary>
    public static (
        IReadOnlyCollection<Guid> IdsDeUsuarios,
        IReadOnlyCollection<Guid> IdsDePersonas,
        IReadOnlyCollection<Guid> IdsDeRoles)
        RecolectarIdentificadores(IReadOnlyList<RegistroAuditoria> pagina)
    {
        var idsDeUsuarios = new HashSet<Guid>();
        var idsDePersonas = new HashSet<Guid>();
        var idsDeRoles = new HashSet<Guid>();

        foreach (var evento in pagina)
        {
            var registro = evento.Registro;
            if (!string.Equals(registro.NombreSchema, Schema, StringComparison.Ordinal)) continue;

            switch (registro.NombreTabla)
            {
                case TablaUsuarios:
                    if (Guid.TryParse(registro.ClaveFila, out var idUsuario)) idsDeUsuarios.Add(idUsuario);
                    break;
                case TablaPersonas:
                    if (Guid.TryParse(registro.ClaveFila, out var idPersona)) idsDePersonas.Add(idPersona);
                    break;
                case TablaAsignacionesDeRol:
                    if (ExtraerGuidDelSnapshot(registro.FilaNueva, registro.FilaAnterior, "user_id") is { } idAsignado)
                        idsDeUsuarios.Add(idAsignado);
                    if (ExtraerGuidDelSnapshot(registro.FilaNueva, registro.FilaAnterior, "role_id") is { } idRol)
                        idsDeRoles.Add(idRol);
                    break;
            }
        }

        return (idsDeUsuarios, idsDePersonas, idsDeRoles);
    }

    /// <summary>
    /// La etiqueta de objeto humanizada y, sólo para un alta o baja de
    /// asignación de rol, el resumen COMPLETO que reemplaza al genérico
    /// (design.md D5). <c>ResumenEspecial</c> en <c>null</c> significa «seguí
    /// con el camino genérico existente» (<see cref="MapeadorEventoAuditoria.ResumenDeCambio"/>).
    /// </summary>
    public static (string Objeto, string? ResumenEspecial) Resolver(
        RegistroCambio registro,
        IReadOnlyList<string> columnas,
        string objetoGenerico,
        IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto,
        IReadOnlyDictionary<Guid, string> nombresRoles)
    {
        if (!string.Equals(registro.NombreSchema, Schema, StringComparison.Ordinal))
            return (objetoGenerico, null);

        return registro.NombreTabla switch
        {
            TablaUsuarios => ResolverCuentaDeUsuario(registro, columnas, objetoGenerico, nombresSujeto),
            TablaPersonas => ResolverPersona(registro, columnas, objetoGenerico, nombresSujeto),
            TablaAsignacionesDeRol => ResolverAsignacionDeRol(
                registro, columnas, objetoGenerico, nombresSujeto, nombresRoles),
            _ => (objetoGenerico, null),
        };
    }

    private static (string Objeto, string? ResumenEspecial) ResolverCuentaDeUsuario(
        RegistroCambio registro,
        IReadOnlyList<string> columnas,
        string objetoGenerico,
        IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto)
    {
        // Un cambio del propio display_name es el ÚNICO caso, para esta tabla,
        // en que el nombre actual podría no coincidir con el que tenía la fila
        // al momento del evento — se queda genérica para no exponer el cambio
        // como diff (design.md D5).
        if (columnas.Contains("display_name", StringComparer.Ordinal)) return (objetoGenerico, null);

        var nombre = NombreDeSujeto(registro.ClaveFila, nombresSujeto);
        return nombre is null ? (objetoGenerico, null) : ($"Cuenta de usuario de {nombre}", null);
    }

    private static (string Objeto, string? ResumenEspecial) ResolverPersona(
        RegistroCambio registro,
        IReadOnlyList<string> columnas,
        string objetoGenerico,
        IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto)
    {
        if (columnas.Contains("nombre", StringComparer.Ordinal) || columnas.Contains("apellido", StringComparer.Ordinal))
            return (objetoGenerico, null);

        var nombre = NombreDeSujeto(registro.ClaveFila, nombresSujeto);
        return nombre is null ? (objetoGenerico, null) : ($"Persona {nombre}", null);
    }

    /// <summary>
    /// La revocación real de un rol (<see cref="ArsDocendi.Shared.Identity.Administracion"/>
    /// del módulo, <c>ReemplazarAsignaciones</c>) es un soft-delete — un
    /// <c>UPDATE</c> que pone <c>deleted_at</c>, nunca un <c>DELETE</c> físico
    /// — así que «asignado»/«quitado» tiene que leer TAMBIÉN esa transición, no
    /// sólo <c>INSERT</c>/<c>DELETE</c> (design.md D5, «Humanized identity
    /// events», tarea 2.9, extendida tras encontrar que el `DELETE` del texto
    /// original nunca ocurre en el flujo real de revocación).
    /// </summary>
    private static (string Objeto, string? ResumenEspecial) ResolverAsignacionDeRol(
        RegistroCambio registro,
        IReadOnlyList<string> columnas,
        string objetoGenerico,
        IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto,
        IReadOnlyDictionary<Guid, string> nombresRoles)
    {
        var idUsuario = ExtraerGuidDelSnapshot(registro.FilaNueva, registro.FilaAnterior, "user_id");
        var idRol = ExtraerGuidDelSnapshot(registro.FilaNueva, registro.FilaAnterior, "role_id");
        var nombre = idUsuario is { } iu ? NombreDeSujeto(iu, nombresSujeto) : null;
        var rol = idRol is { } ir && nombresRoles.TryGetValue(ir, out var rolResuelto) ? rolResuelto : null;
        var objeto = nombre is null ? objetoGenerico : $"Roles de {nombre}";

        if (nombre is null || rol is null) return (objeto, null);

        if (registro.Accion == "INSERT") return (objeto, $"Rol {rol} asignado a {nombre}");
        if (registro.Accion == "DELETE") return (objeto, $"Rol {rol} quitado a {nombre}");

        if (registro.Accion == "UPDATE" && columnas.Contains("deleted_at", StringComparer.Ordinal))
        {
            var antesActivo = EsPropiedadNula(registro.FilaAnterior, "deleted_at");
            var despuesActivo = EsPropiedadNula(registro.FilaNueva, "deleted_at");
            if (antesActivo == true && despuesActivo == false) return (objeto, $"Rol {rol} quitado a {nombre}");
            if (antesActivo == false && despuesActivo == true) return (objeto, $"Rol {rol} asignado a {nombre}");
        }

        return (objeto, null);
    }

    private static string? NombreDeSujeto(string claveFila, IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto) =>
        Guid.TryParse(claveFila, out var id) ? NombreDeSujeto(id, nombresSujeto) : null;

    private static string? NombreDeSujeto(Guid id, IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto) =>
        nombresSujeto.TryGetValue(id, out var nombre) ? MapeadorEventoAuditoria.NombreNaturalDePersona(nombre) : null;

    /// <summary>
    /// Lee <paramref name="propiedad"/> como <c>Guid</c> de cualquiera de los
    /// dos snapshots — <paramref name="preferido"/> primero (<c>new_row</c> en
    /// un ALTA o CAMBIO), <paramref name="alternativo"/> si falta (<c>old_row</c>
    /// en una BAJA física).
    /// </summary>
    private static Guid? ExtraerGuidDelSnapshot(string? preferido, string? alternativo, string propiedad) =>
        LeerGuid(preferido, propiedad) ?? LeerGuid(alternativo, propiedad);

    /// <summary>
    /// <c>true</c> si <paramref name="propiedad"/> es JSON <c>null</c> en el
    /// snapshot, <c>false</c> si tiene un valor, <c>null</c> si no se puede
    /// determinar (snapshot ausente o propiedad faltante) — usado para leer la
    /// transición de <c>deleted_at</c> sin necesitar su valor real, que nunca
    /// se muestra.
    /// </summary>
    private static bool? EsPropiedadNula(string? json, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.TryGetProperty(propiedad, out var valor)
            ? valor.ValueKind == JsonValueKind.Null
            : null;
    }

    private static Guid? LeerGuid(string? json, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.TryGetProperty(propiedad, out var valor)
            && valor.ValueKind == JsonValueKind.String
            && Guid.TryParse(valor.GetString(), out var guid)
            ? guid
            : null;
    }
}
