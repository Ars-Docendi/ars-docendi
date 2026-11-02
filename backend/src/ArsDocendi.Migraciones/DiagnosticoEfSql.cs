using System.Data;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Migraciones;

/// <summary>Ejecuta exclusivamente el SELECT local declarado por cada módulo en una transacción de lectura.</summary>
public abstract class DiagnosticoEfSql<TContexto>(TContexto db, string contexto, string sql)
    : IDiagnosticoIntegridadModulo where TContexto : DbContext
{
    public string Contexto => contexto;

    public async Task<IReadOnlyList<RegistroIntegridad>> LeerAsync(CancellationToken ct)
    {
        var conexion = db.Database.GetDbConnection();
        var cerrar = conexion.State != ConnectionState.Open;
        if (cerrar) await conexion.OpenAsync(ct);
        try
        {
            await using var transaccion = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandTimeout = 30;
            comando.CommandText = "SET TRANSACTION READ ONLY";
            await comando.ExecuteNonQueryAsync(ct);
            comando.CommandText = sql;
            var registros = new List<RegistroIntegridad>();
            await using (var lector = await comando.ExecuteReaderAsync(ct))
            {
                while (await lector.ReadAsync(ct))
                    registros.Add(new(lector.GetString(0), lector.GetString(1), lector.GetGuid(2),
                        lector.IsDBNull(3) ? null : lector.GetString(3),
                        lector.IsDBNull(4) ? null : lector.GetGuid(4)));
            }
            await transaccion.CommitAsync(ct);
            return registros;
        }
        finally { if (cerrar) await conexion.CloseAsync(); }
    }
}
