using System.Data.Common;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Modules.Designaciones.Infrastructure;

/// <summary>Consulta asociaciones locales y participa de la misma transacción que Storage.</summary>
public sealed class ReferenciasArchivosDesignaciones(DesignacionesDbContext db)
    : IConsultasReferenciasArchivos, IParticipanteTransaccionArchivos
{
    private DbConnection? conexionRestauracion;
    public string Consumidor => "Designaciones";

    public Task<bool> TieneReferenciasAsync(Guid archivoId, CancellationToken ct) =>
        db.PedidoAdjuntos.AsNoTracking().AnyAsync(a => a.ArchivoId == archivoId, ct);

    public async Task EnlistarAsync(DbConnection conexion, DbTransaction transaccion, CancellationToken ct)
    {
        if (conexionRestauracion is not null || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Designaciones ya participa en una transacción.");
        // Clone conserva autenticación/DataSource sin extraer ni persistir credenciales.
        conexionRestauracion = (DbConnection)((ICloneable)db.Database.GetDbConnection()).Clone();
        db.Database.SetDbConnection(conexion, contextOwnsConnection: false);
        await db.Database.UseTransactionAsync(transaccion, ct);
    }

    public async Task DesenlistarAsync(CancellationToken ct)
    {
        await db.Database.UseTransactionAsync(null, ct);
        if (conexionRestauracion is not null)
        {
            db.Database.SetDbConnection(conexionRestauracion, contextOwnsConnection: true);
            conexionRestauracion = null;
        }
    }
}
