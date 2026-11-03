using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Creates the seven administration-of-use tables (budgets, org cap, price table, maintenance flag,
/// admin audit trail) on an already-migrated base.
/// </summary>
public static class AdministracionAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/006_asistente_administracion.sql";

    /// <summary>
    /// A lo sumo un override vigente por actor, impuesto por el motor. Va aparte
    /// porque 006 ya se aplicó en ambientes compartidos y no se edita.
    /// </summary>
    public const string RecursoSqlVigenteUnico = "asistente/008_asistente_presupuesto_usuario_vigente_unico.sql";

    /// <summary>
    /// Applies the administration-of-use tables' DDL. Converges on a base that already has them.
    /// </summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        foreach (var recurso in (string[])[RecursoSql, RecursoSqlVigenteUnico])
        {
            var sql = RecursosSql.Leer(typeof(AdministracionAsistente).Assembly, recurso);
            await using var ddl = new NpgsqlCommand(sql, conexion, transaccion);
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
