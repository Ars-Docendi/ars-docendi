using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Modules.Aulas.Domain;
using Modules.Aulas.Infrastructure;
using Npgsql;

namespace Modules.Aulas.Repositories;

internal sealed class RepositorioSolicitudesAula(AulasDbContext db)
{
    public Task<SolicitudReservaAula?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        db.SolicitudesReserva.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SolicitudReservaAula>> ListarPorDocenteAsync(
        Guid docenteId, CancellationToken ct) =>
        await db.SolicitudesReserva
                .AsNoTracking()
                .Where(s => s.DocenteId == docenteId)
                .OrderByDescending(s => s.CreadoEn)
                .ToListAsync(ct);

    public async Task<IReadOnlyList<SolicitudReservaAula>> ListarTodasAsync(CancellationToken ct) =>
        await db.SolicitudesReserva
                .AsNoTracking()
                .OrderByDescending(s => s.CreadoEn)
                .ToListAsync(ct);

    public void Agregar(SolicitudReservaAula solicitud) => db.SolicitudesReserva.Add(solicitud);

    /// <summary>
    /// Otra reserva aprobada de la misma aula que se superpone con <paramref name="s"/>. Es el
    /// chequeo amable: la garantía es el EXCLUDE <c>solicitudes_reserva_aula_sin_superposicion</c>.
    /// </summary>
    public Task<bool> AulaOcupadaAsync(Guid aulaId, SolicitudReservaAula s, CancellationToken ct) =>
        db.SolicitudesReserva.AsNoTracking().AnyAsync(o =>
            o.Id != s.Id && o.AulaId == aulaId && o.Estado == EstadosSolicitudAula.Aprobada &&
            o.Dia == s.Dia && o.HorarioDesde < s.HorarioHasta && s.HorarioDesde < o.HorarioHasta, ct);

    public async Task<IReadOnlyList<Aula>> ListarAulasAsync(CancellationToken ct) =>
        await db.Aulas.AsNoTracking().OrderBy(a => a.Nombre).ToListAsync(ct);

    public Task<Aula?> ObtenerAulaAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking ? db.Aulas : db.Aulas.AsNoTracking()).SingleOrDefaultAsync(a => a.Id == id, ct);

    public void AgregarAula(Aula aula) => db.Aulas.Add(aula);
    public void EliminarAula(Aula aula) => db.Aulas.Remove(aula);
    public void EsperarVersion(Aula aula, uint version) =>
        db.Entry(aula).Property(a => a.Version).OriginalValue = version;

    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg)
        {
            var traducido = pg.ConstraintName switch
            {
                "solicitudes_reserva_aula_sin_superposicion" => ErroresAulas.AulaOcupada(),
                "aulas_nombre_unico" => ErroresAulas.NombreRepetido(),
                "solicitudes_reserva_aula_id_fkey" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "aula-en-uso",
                    "El aula tiene reservas y no se puede eliminar. Desactivala."),
                _ => null,
            };
            if (traducido is not null) throw traducido;
            throw;
        }
    }
}

internal static class ErroresAulas
{
    public static ExcepcionAplicacion AulaOcupada() => new(
        TipoErrorAplicacion.Conflicto,
        "aula-ocupada",
        "El aula ya tiene una reserva aprobada que se superpone con ese horario.");

    public static ExcepcionAplicacion NombreRepetido() => new(
        TipoErrorAplicacion.Conflicto,
        "aula-nombre-repetido",
        "Ya existe un aula con ese nombre.");
}
