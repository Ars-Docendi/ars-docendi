using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Modules.Portal.Api;
using Modules.Portal.Domain;
using Modules.Portal.Infrastructure;
using Npgsql;

namespace Modules.Portal.Repositories;

public sealed class RepositorioVocabulario(PortalDbContext db)
{
    /// <summary>Términos que se ofrecen al elegir: incorporados y no fusionados.</summary>
    public async Task<IReadOnlyList<string>> ListarOfrecidosAsync(CancellationToken ct) =>
        await db.Habilidades.AsNoTracking()
            .Where(h => !h.Sugerido && h.CanonicaId == null)
            .OrderBy(h => h.Termino)
            .Select(h => h.Termino)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TerminoVocabularioDto>> ListarAsync(CancellationToken ct)
    {
        var terminos = await db.Habilidades.AsNoTracking().ToListAsync(ct);
        var docentes = await db.DocenteHabilidades.AsNoTracking()
            .GroupBy(x => x.HabilidadId)
            .Select(g => new { g.Key, Cantidad = g.Select(x => x.PerfilId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Cantidad, ct);
        var nombres = terminos.ToDictionary(h => h.Id, h => h.Termino);
        return terminos
            .OrderBy(h => h.Termino, StringComparer.CurrentCultureIgnoreCase)
            .Select(h => new TerminoVocabularioDto(
                h.Id,
                h.Termino,
                h.Sugerido,
                h.CanonicaId is { } c ? nombres.GetValueOrDefault(c) : null,
                docentes.GetValueOrDefault(h.Id)))
            .ToList();
    }

    public Task<Habilidad?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Habilidades.SingleOrDefaultAsync(h => h.Id == id, ct);

    public Task<bool> EsCanonicoDeOtrosAsync(Guid id, CancellationToken ct) =>
        db.Habilidades.AnyAsync(h => h.CanonicaId == id, ct);

    public async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "termino-repetido",
                "Ya existe ese término. Si son el mismo, fusionalos.");
        }
    }

    /// <summary>
    /// Fusiona <paramref name="origen"/> en <paramref name="destino"/> en una transacción: marca el
    /// canónico (bloquea la fila del origen, ver <see cref="RepositorioPortal.ReemplazarTagsAsync"/>),
    /// mueve los usos sin duplicar y sube las versiones de los perfiles afectados para que un editor
    /// abierto reciba 409.
    /// </summary>
    public async Task FusionarAsync(Guid origen, Guid destino, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE portal.habilidades SET canonica_id={destino} WHERE id={origen}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE portal.perfiles p
               SET version_habilidades = version_habilidades + 1,
                   version_intereses = version_intereses + 1
             WHERE p.id IN (SELECT perfil_id FROM portal.docente_habilidades WHERE habilidad_id={origen})
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO portal.docente_habilidades (perfil_id, habilidad_id, tipo)
            SELECT perfil_id, {destino}, tipo FROM portal.docente_habilidades WHERE habilidad_id={origen}
            ON CONFLICT DO NOTHING
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM portal.docente_habilidades WHERE habilidad_id={origen}", ct);
        await tx.CommitAsync(ct);
    }
}
