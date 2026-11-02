using System.Data.Common;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Modules.Portal.Infrastructure;

/// <summary>Portal conserva la autoridad de sus asociaciones; Storage solo consume contratos.</summary>
public sealed class ReferenciasArchivosPortal(PortalDbContext db)
    : IConsultasReferenciasArchivos, IParticipanteTransaccionArchivos
{
    private DbConnection? conexionRestauracion;
    public string Consumidor => "Portal";

    public async Task<bool> TieneReferenciasAsync(Guid archivoId, CancellationToken ct) =>
        await db.Cvs.AsNoTracking().AnyAsync(x => x.ArchivoId == archivoId, ct)
        || await db.ProyectoDocumentos.AsNoTracking().AnyAsync(x => x.ArchivoId == archivoId, ct);

    public async Task EnlistarAsync(DbConnection conexion, DbTransaction transaccion, CancellationToken ct)
    {
        if (conexionRestauracion is not null || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Portal ya participa en una transacción.");
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
