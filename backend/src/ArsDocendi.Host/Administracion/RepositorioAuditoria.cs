using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Host.Administracion;

public sealed record RegistroAuditoria(
    RegistroCambio Registro,
    string? NombreCuenta,
    string? NombrePersona,
    string? ApellidoPersona,
    string FormatoClave = "legacy",
    string? ClaveCompleta = null);

// Proyección de metadata aditiva sin cambiar el modelo Identity compartido.
public sealed class MetadatosClaveAuditoria
{
    public long Id { get; set; }
    public string FormatoClave { get; set; } = "legacy";
    public string? ClaveCompleta { get; set; }
}

public sealed record ResultadoPaginaAuditoria(IReadOnlyList<RegistroAuditoria> Registros, long Total);

public interface IRepositorioAuditoria
{
    Task<ResultadoPaginaAuditoria> ListarAsync(ConsultaAuditoriaDto filtros, CancellationToken ct);
}

public sealed class RepositorioAuditoria(IdentityDbContext db) : IRepositorioAuditoria
{
    public async Task<ResultadoPaginaAuditoria> ListarAsync(
        ConsultaAuditoriaDto filtros,
        CancellationToken ct)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));

        var clavePrecisa = filtros.RowPk?.TrimStart().StartsWith('{') == true;
        var eventos = clavePrecisa
            ? db.RegistrosDeCambio.FromSqlInterpolated($"""
                SELECT r.* FROM audit.change_log r
                WHERE r.row_pk=CAST({filtros.RowPk} AS jsonb)::text
                    OR (r.row_pk_format='legacy' AND audit.resolved_key(
                        r.schema_name,r.table_name,r.old_row,r.new_row,r.row_key)=CAST({filtros.RowPk} AS jsonb))
                """).AsNoTracking()
            : db.RegistrosDeCambio.AsNoTracking();

        var consulta =
            from registro in eventos
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
        if (filtros.Schema is { Length: > 0 } schema)
            consulta = consulta.Where(r => r.Registro.NombreSchema == schema);
        if (filtros.Tabla is { Length: > 0 } tabla)
            consulta = consulta.Where(r => r.Registro.NombreTabla == tabla);
        if (filtros.CambiadoPor is { } actorId)
            consulta = consulta.Where(r => r.Registro.CambiadoPor == actorId);
        if (!clavePrecisa && filtros.RowPk is { Length: > 0 } rowPk)
            consulta = consulta.Where(r => r.Registro.ClaveFila == rowPk);
        if (filtros.Actor is { Length: > 0 } actor)
        {
            var actorNormalizado = actor.Trim().ToLower();
            consulta = consulta.Where(r =>
                ((r.ApellidoPersona != null && r.NombrePersona != null)
                    ? r.ApellidoPersona + ", " + r.NombrePersona
                    : (r.NombreCuenta ?? "Actor no identificado"))
                .ToLower()
                .Contains(actorNormalizado));
        }

        var total = await consulta.LongCountAsync(ct);
        var registros = await consulta
            .OrderByDescending(r => r.Registro.CambiadoEn)
            .ThenByDescending(r => r.Registro.Id)
            .Skip((filtros.Pagina - 1) * filtros.TamanoPagina)
            .Take(filtros.TamanoPagina)
            .Select(r => new RegistroAuditoria(
                r.Registro,
                r.NombreCuenta,
                r.NombrePersona,
                r.ApellidoPersona))
            .ToListAsync(ct);

        if (registros.Count > 0)
        {
            var ids = registros.Select(r => r.Registro.Id).ToArray();
            var metadatos = await db.Database.SqlQuery<MetadatosClaveAuditoria>($"""
                SELECT id AS "Id",
                    CASE WHEN row_pk_format<>'legacy' THEN row_pk_format
                        WHEN audit.resolved_key(schema_name,table_name,old_row,new_row,row_key) IS NOT NULL THEN 'legacy-recovered'
                        WHEN (schema_name,table_name) IN (('identity','rol_permisos'),('portal','docente_habilidades'),('tareas','tarea_relaciones')) THEN 'legacy-group'
                        ELSE 'legacy' END AS "FormatoClave",
                    audit.resolved_key(schema_name,table_name,old_row,new_row,row_key)::text AS "ClaveCompleta"
                FROM audit.change_log WHERE id=ANY({ids})
                """).ToDictionaryAsync(x => x.Id, ct);
            registros = registros.Select(r => r with
            {
                FormatoClave = metadatos[r.Registro.Id].FormatoClave,
                ClaveCompleta = metadatos[r.Registro.Id].ClaveCompleta,
            }).ToList();
        }
        return new ResultadoPaginaAuditoria(registros, total);
    }
}
