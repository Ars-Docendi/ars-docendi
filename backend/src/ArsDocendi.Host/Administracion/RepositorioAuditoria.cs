using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Host.Administracion;

public sealed record RegistroAuditoria(
    RegistroCambio Registro,
    string? NombreCuenta,
    string? NombrePersona,
    string? ApellidoPersona);

public sealed record ResultadoPaginaAuditoria(IReadOnlyList<RegistroAuditoria> Registros, long Total);

/// <summary>Un nombre resuelto desde <c>identity</c>, para actores del asistente (design.md D5/D6).</summary>
public sealed record NombreResuelto(string? NombreCuenta, string? NombrePersona, string? ApellidoPersona);

public interface IRepositorioAuditoria
{
    /// <summary>
    /// Lista <c>audit.change_log</c> filtrado, con el corrimiento explícito que
    /// pide la fusión de fuentes (design.md D2): <paramref name="salto"/> y
    /// <paramref name="cantidad"/> reemplazan a <c>filtros.Pagina</c>/
    /// <c>filtros.TamanoPagina</c> para la ventana — el resto de
    /// <paramref name="filtros"/> sigue gobernando qué filas califican.
    /// </summary>
    Task<ResultadoPaginaAuditoria> ListarAsync(
        ConsultaAuditoriaDto filtros, int salto, int cantidad, CancellationToken ct);

    /// <summary>
    /// Resuelve nombres de cuenta/persona para actores del asistente
    /// (sistema-seccion-unificada, design.md D5: <c>FuenteAuditoriaAsistente</c>
    /// no tiene join propio, así que reusa el mismo par cuenta/persona que
    /// <c>audit.change_log</c> ya resuelve).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Resuelve nombres para el sujeto humano de un evento de identidad
    /// (sistema-seccion-unificada, design.md D5, «Humanized identity events»,
    /// tarea 2.9): <paramref name="idsDeUsuarios"/> son <c>row_pk</c> de
    /// <c>identity.users</c> o el <c>user_id</c> del snapshot de
    /// <c>identity.user_roles</c>; <paramref name="idsDePersonas"/> son
    /// <c>row_pk</c> de <c>identity.personas</c> — esa tabla no tiene cuenta
    /// propia, así que se lee DIRECTO, sin pasar por <c>identity.users</c>.
    /// UNA sola consulta batched por página (<c>UNION ALL</c> vía
    /// <c>Concat</c>), no una por fila.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresDeSujetosAsync(
        IReadOnlyCollection<Guid> idsDeUsuarios, IReadOnlyCollection<Guid> idsDePersonas, CancellationToken ct);

    /// <summary>
    /// Resuelve el nombre de <c>identity.roles</c> por <c>role_id</c>, para la
    /// asignación de rol de design.md D5 («Rol {rol} asignado a/quitado a
    /// {nombre}», tarea 2.9).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolverNombresDeRolesAsync(
        IReadOnlyCollection<Guid> idsDeRoles, CancellationToken ct);
}

/// <summary>
/// <see cref="IRepositorioAuditoria"/> sobre <c>audit.change_log</c>
/// (sistema-seccion-unificada, design.md D4/D9, tarea 2.6): <c>modulo</c>
/// reemplaza a <c>schema</c>, <c>tabla</c> admite forma calificada o desnuda, y
/// <c>q</c> reemplaza a <c>actor</c> con alcance de etiqueta (nunca de valor).
/// </summary>
public sealed class RepositorioAuditoria(IdentityDbContext db) : IRepositorioAuditoria
{
    public async Task<ResultadoPaginaAuditoria> ListarAsync(
        ConsultaAuditoriaDto filtros, int salto, int cantidad, CancellationToken ct)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));

        var consulta =
            from registro in db.RegistrosDeCambio.AsNoTracking()
            join cuentaBase in db.Usuarios.AsNoTracking()
                on registro.CambiadoPor equals (Guid?)cuentaBase.Id into cuentas
            from cuenta in cuentas.DefaultIfEmpty()
            join personaBase in db.Personas.AsNoTracking()
                on (cuenta == null ? null : cuenta.PersonaId) equals (Guid?)personaBase.Id into personas
            from persona in personas.DefaultIfEmpty()
            select new
            {
                Registro = registro,
                NombreCuenta = cuenta == null ? null : cuenta.NombreParaMostrar,
                NombrePersona = persona == null ? null : persona.Nombre,
                ApellidoPersona = persona == null ? null : persona.Apellido,
            };

        if (filtros.Desde is { } desde)
            consulta = consulta.Where(r => r.Registro.CambiadoEn >= desde);
        if (filtros.Hasta is { } hasta)
            consulta = consulta.Where(r => r.Registro.CambiadoEn <= hasta);
        if (filtros.Accion is { Length: > 0 } accion)
            consulta = consulta.Where(r => r.Registro.Accion == accion);
        if (filtros.Modulo is { Length: > 0 } modulo)
            consulta = consulta.Where(r => r.Registro.NombreSchema == modulo);
        if (filtros.Tabla is { Length: > 0 } tabla)
        {
            var partes = tabla.Split('.', 2);
            consulta = partes.Length == 2
                ? consulta.Where(r => r.Registro.NombreSchema == partes[0] && r.Registro.NombreTabla == partes[1])
                : consulta.Where(r => r.Registro.NombreTabla == tabla);
        }
        if (filtros.CambiadoPor is { } actorId)
            consulta = consulta.Where(r => r.Registro.CambiadoPor == actorId);
        if (filtros.RowPk is { Length: > 0 } rowPk)
            consulta = consulta.Where(r => r.Registro.ClaveFila == rowPk);
        if (filtros.Q is { Length: > 0 } q)
        {
            // Inline y no un método separado: un tipo anónimo no se puede
            // nombrar como parámetro de otro método, y extraerlo a un record
            // con nombre rompe la traducción de EF Core — el compilador de
            // consultas no aplana un `Select(r => new MiRecord(...))` de la
            // misma forma que aplana un `Select(r => new { ... })`, y el
            // `Where` posterior queda intentando reconstruir el registro
            // entero dentro del predicado en vez de leer sus columnas.
            var normalizado = EtiquetasAuditoria.Normalizar(q);
            var patron = "%" + EscaparPatron(normalizado) + "%";
            var schemas = EtiquetasAuditoria.SchemasQueMatchean(normalizado);
            var objetos = EtiquetasAuditoria.ObjetosQueMatchean(normalizado);
            var campos = EtiquetasAuditoria.CamposQueMatchean(normalizado).ToArray();

            // El objeto humanizado (design.md D5, «Humanized identity events»,
            // tarea 2.9) nombra al SUJETO afectado — «Cuenta de usuario de
            // {nombre}», «Persona {nombre}», «Roles de {nombre}» — así que la
            // búsqueda por etiqueta tiene que poder encontrar ese nombre
            // también. Una sola consulta por página (no una por fila) resuelve
            // qué `identity.users`/`identity.personas` tienen ese nombre HOY,
            // por la misma expresión de nombre que ya usan las etiquetas; el
            // predicado de abajo entra por sus IDS, nunca por el nombre en sí
            // — sigue siendo "nunca por valor" (design.md D4).
            var (idsUsuariosQueMatchean, idsPersonasQueMatchean) =
                await ResolverSujetosQueMatcheanNombreAsync(patron, ct);
            var textoIdsUsuarios = idsUsuariosQueMatchean.Select(id => id.ToString()).ToArray();
            var textoIdsPersonas = idsPersonasQueMatchean.Select(id => id.ToString()).ToArray();
            // Las claves de fila (row_pk) de identity.user_roles cuyo user_id
            // —el VALOR, no sólo la clave— matchea: `->>'user_id'` es una
            // extracción de valor, así que no se puede armar con
            // JsonExistAny (que sólo pregunta por claves). Una consulta aparte
            // en vez de meter el valor en el predicado principal, porque
            // FilaNueva/FilaAnterior son `jsonb`: LIKE no tiene operador para
            // ese tipo sin castear, y castear adentro del `Where` compuesto de
            // abajo no es más simple que resolverlo antes, en una sola consulta.
            var clavesFilaDeAsignacionesQueMatchean = idsUsuariosQueMatchean.Count == 0
                ? []
                : await db.Database.SqlQuery<string>($"""
                    SELECT row_pk FROM audit.change_log
                    WHERE schema_name = 'identity' AND table_name = 'user_roles'
                      AND (
                        (new_row IS NOT NULL AND (new_row ->> 'user_id')::uuid = ANY({idsUsuariosQueMatchean.ToArray()}))
                        OR (old_row IS NOT NULL AND (old_row ->> 'user_id')::uuid = ANY({idsUsuariosQueMatchean.ToArray()}))
                      )
                    """).ToListAsync(ct);

            consulta = consulta.Where(r =>
                schemas.Contains(r.Registro.NombreSchema)
                || objetos.Contains(r.Registro.NombreSchema + "." + r.Registro.NombreTabla)
                // UPDATE: los NOMBRES de columna cambiada, nunca su valor
                // (changed_columns es un array de texto, no el snapshot).
                || (r.Registro.ColumnasCambiadas != null && r.Registro.ColumnasCambiadas.Any(c => campos.Contains(c)))
                // INSERT/DELETE (sin changed_columns): las CLAVES del snapshot
                // que sí exista, vía el operador jsonb `?|` (design.md D4) —
                // JsonExistAny pregunta "¿existe alguna de estas claves?" y
                // nunca lee el valor que esa clave tiene.
                || (r.Registro.ColumnasCambiadas == null && r.Registro.FilaNueva != null
                    && EF.Functions.JsonExistAny(r.Registro.FilaNueva, campos))
                || (r.Registro.ColumnasCambiadas == null && r.Registro.FilaAnterior != null
                    && EF.Functions.JsonExistAny(r.Registro.FilaAnterior, campos))
                || EF.Functions.ILike(
                    EF.Functions.Unaccent(r.Registro.NombreSchema + "." + r.Registro.NombreTabla), patron)
                || EF.Functions.ILike(EF.Functions.Unaccent(r.Registro.ClaveFila), patron)
                || EF.Functions.ILike(EF.Functions.Unaccent(
                    (r.ApellidoPersona != null && r.NombrePersona != null)
                        ? r.NombrePersona + " " + r.ApellidoPersona
                        : (r.NombreCuenta ?? (r.Registro.CambiadoPor == null && r.Registro.RequestId == null
                            ? "Proceso automático"
                            : "Actor no identificado"))), patron)
                // Sujeto humanizado: identity.users por su propio row_pk — pero
                // NUNCA si este evento cambió display_name, el único caso en
                // que la etiqueta se queda genérica (IdentidadDeAuditoria).
                || (r.Registro.NombreSchema == "identity" && r.Registro.NombreTabla == "users"
                    && textoIdsUsuarios.Contains(r.Registro.ClaveFila)
                    && !(r.Registro.ColumnasCambiadas != null
                        && r.Registro.ColumnasCambiadas.Contains("display_name")))
                // identity.personas por su propio row_pk — nunca si el propio
                // evento cambió nombre/apellido (mismo criterio que el objeto).
                || (r.Registro.NombreSchema == "identity" && r.Registro.NombreTabla == "personas"
                    && textoIdsPersonas.Contains(r.Registro.ClaveFila)
                    && !(r.Registro.ColumnasCambiadas != null
                        && (r.Registro.ColumnasCambiadas.Contains("nombre")
                            || r.Registro.ColumnasCambiadas.Contains("apellido"))))
                // identity.user_roles por el user_id de SU snapshot (old_row o
                // new_row) — un id interno de esta búsqueda, nunca mostrado.
                || (r.Registro.NombreSchema == "identity" && r.Registro.NombreTabla == "user_roles"
                    && clavesFilaDeAsignacionesQueMatchean.Contains(r.Registro.ClaveFila)));
        }

        var total = await consulta.LongCountAsync(ct);
        var registros = await consulta
            .OrderByDescending(r => r.Registro.CambiadoEn)
            .ThenByDescending(r => r.Registro.Id)
            .Skip(salto)
            .Take(cantidad)
            .Select(r => new RegistroAuditoria(
                r.Registro,
                r.NombreCuenta,
                r.NombrePersona,
                r.ApellidoPersona))
            .ToListAsync(ct);

        return new ResultadoPaginaAuditoria(registros, total);
    }

    /// <summary>Escapa <c>%</c>, <c>_</c> y <c>\</c> antes de envolver en comodines (design.md D4).</summary>
    private static string EscaparPatron(string valor) =>
        valor.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>
    /// Qué <c>identity.users</c>/<c>identity.personas</c> tienen HOY un nombre
    /// que matchea <paramref name="patron"/> — la MISMA expresión de nombre que
    /// ya arma la etiqueta de objeto humanizada (<see
    /// cref="MapeadorEventoAuditoria.NombreNaturalDePersona"/>: persona
    /// completa si tiene nombre Y apellido, si no el nombre de la cuenta). UNA
    /// sola consulta (<c>Concat</c> → <c>UNION ALL</c>), no una por fila.
    /// </summary>
    private async Task<(IReadOnlyCollection<Guid> IdsUsuarios, IReadOnlyCollection<Guid> IdsPersonas)>
        ResolverSujetosQueMatcheanNombreAsync(string patron, CancellationToken ct)
    {
        var porUsuario =
            from cuenta in db.Usuarios.AsNoTracking()
            join personaBase in db.Personas.AsNoTracking()
                on cuenta.PersonaId equals (Guid?)personaBase.Id into personas
            from persona in personas.DefaultIfEmpty()
            where EF.Functions.ILike(
                EF.Functions.Unaccent(
                    persona != null && persona.Nombre != null && persona.Apellido != null
                        ? persona.Nombre + " " + persona.Apellido
                        : cuenta.NombreParaMostrar),
                patron)
            select new { Id = cuenta.Id, EsPersona = false };

        var porPersona =
            from persona in db.Personas.AsNoTracking()
            where persona.Nombre != null && persona.Apellido != null
                && EF.Functions.ILike(EF.Functions.Unaccent(persona.Nombre + " " + persona.Apellido), patron)
            select new { Id = persona.Id, EsPersona = true };

        var filas = await porUsuario.Concat(porPersona).ToListAsync(ct);

        return (
            [.. filas.Where(f => !f.EsPersona).Select(f => f.Id)],
            [.. filas.Where(f => f.EsPersona).Select(f => f.Id)]);
    }

    public async Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, NombreResuelto>();
        }

        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));

        var filas = await (
            from cuenta in db.Usuarios.AsNoTracking()
            where ids.Contains(cuenta.Id)
            join personaBase in db.Personas.AsNoTracking()
                on cuenta.PersonaId equals (Guid?)personaBase.Id into personas
            from persona in personas.DefaultIfEmpty()
            select new
            {
                cuenta.Id,
                NombreCuenta = cuenta.NombreParaMostrar,
                NombrePersona = persona == null ? null : persona.Nombre,
                ApellidoPersona = persona == null ? null : persona.Apellido,
            }).ToListAsync(ct);

        return filas.ToDictionary(
            f => f.Id,
            f => new NombreResuelto(f.NombreCuenta, f.NombrePersona, f.ApellidoPersona));
    }

    public async Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresDeSujetosAsync(
        IReadOnlyCollection<Guid> idsDeUsuarios, IReadOnlyCollection<Guid> idsDePersonas, CancellationToken ct)
    {
        if (idsDeUsuarios.Count == 0 && idsDePersonas.Count == 0)
        {
            return new Dictionary<Guid, NombreResuelto>();
        }

        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));

        // Las dos ramas proyectan la MISMA forma anónima a propósito: es lo que
        // permite que Concat() traduzca a un UNION ALL — una sola consulta para
        // toda la página, no una por fila (design.md D5, tarea 2.9).
        var porUsuario =
            from cuenta in db.Usuarios.AsNoTracking()
            where idsDeUsuarios.Contains(cuenta.Id)
            join personaBase in db.Personas.AsNoTracking()
                on cuenta.PersonaId equals (Guid?)personaBase.Id into personas
            from persona in personas.DefaultIfEmpty()
            select new
            {
                Id = cuenta.Id,
                NombreCuenta = (string?)cuenta.NombreParaMostrar,
                NombrePersona = persona == null ? null : persona.Nombre,
                ApellidoPersona = persona == null ? null : persona.Apellido,
            };

        var porPersona =
            from persona in db.Personas.AsNoTracking()
            where idsDePersonas.Contains(persona.Id)
            select new
            {
                Id = persona.Id,
                NombreCuenta = (string?)null,
                NombrePersona = (string?)persona.Nombre,
                ApellidoPersona = (string?)persona.Apellido,
            };

        var filas = await porUsuario.Concat(porPersona).ToListAsync(ct);

        return filas.ToDictionary(
            f => f.Id,
            f => new NombreResuelto(f.NombreCuenta, f.NombrePersona, f.ApellidoPersona));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolverNombresDeRolesAsync(
        IReadOnlyCollection<Guid> idsDeRoles, CancellationToken ct)
    {
        if (idsDeRoles.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));

        var filas = await db.Roles.AsNoTracking()
            .Where(r => idsDeRoles.Contains(r.Id))
            .Select(r => new { r.Id, r.Nombre })
            .ToListAsync(ct);

        return filas.ToDictionary(f => f.Id, f => f.Nombre);
    }
}

