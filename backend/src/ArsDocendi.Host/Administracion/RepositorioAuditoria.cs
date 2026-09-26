using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Host.Administracion;

public sealed record RegistroAuditoria(
    RegistroCambio Registro,
    string? NombreCuenta,
    string? NombrePersona,
    string? ApellidoPersona);

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
        if (filtros.Schema is { Length: > 0 } schema)
            consulta = consulta.Where(r => r.Registro.NombreSchema == schema);
        if (filtros.Tabla is { Length: > 0 } tabla)
            consulta = consulta.Where(r => r.Registro.NombreTabla == tabla);
        if (filtros.CambiadoPor is { } actorId)
            consulta = consulta.Where(r => r.Registro.CambiadoPor == actorId);
        if (filtros.RowPk is { Length: > 0 } rowPk)
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

        return new ResultadoPaginaAuditoria(registros, total);
    }
}
