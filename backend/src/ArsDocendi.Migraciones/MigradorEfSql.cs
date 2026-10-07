using System.Data;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Migraciones;

/// <summary>Adaptador de infraestructura reutilizado por cada contexto; Shared conserva contratos puros.</summary>
public abstract class MigradorEfSql<TContexto>(TContexto db, string contexto, params string[] schemas)
    : IMigradorModulo where TContexto : DbContext
{
    public string Contexto => contexto;

    public async Task<EstadoMigracionesModulo> ConsultarAsync(CancellationToken ct)
    {
        var disponibles = db.Database.GetMigrations().ToArray();
        var aplicadas = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        ValidadorHistorialMigraciones.Validar(disponibles, aplicadas);
        if (disponibles.Length > 0 && aplicadas.Length == 0)
        {
            var conexion = db.Database.GetDbConnection();
            var cerrar = conexion.State != ConnectionState.Open;
            if (cerrar) await conexion.OpenAsync(ct);
            try
            {
                await using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = ANY(@schemas))";
                var parametro = comando.CreateParameter();
                parametro.ParameterName = "schemas";
                parametro.Value = schemas;
                comando.Parameters.Add(parametro);
                if (await comando.ExecuteScalarAsync(ct) is true)
                    throw new InvalidOperationException($"Schema gestionado de {Contexto} existente sin historial reconocido; no se modificó la base.");
            }
            finally { if (cerrar) await conexion.CloseAsync(); }
        }
        ValidarRecursos();
        return new(Contexto, disponibles, aplicadas, disponibles.Skip(aplicadas.Length).ToArray());
    }

    public void ValidarRecursos()
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        foreach (var migracion in assembly.Migrations.Values)
            _ = MigracionSqlExtensions.LeerRecursos(migracion.AsType());
    }

    public async Task<string> GenerarScriptAsync(CancellationToken ct)
    {
        var estado = await ConsultarAsync(ct);
        if (estado.Pendientes.Count == 0) return string.Empty;
        return db.GetService<IMigrator>().GenerateScript(
            estado.Aplicadas.LastOrDefault() ?? Migration.InitialDatabase,
            estado.Disponibles.Last(), MigrationsSqlGenerationOptions.Default);
    }

    public async Task MigrarAsync(CancellationToken ct)
    {
        var estado = await ConsultarAsync(ct);
        if (estado.Pendientes.Count > 0) await db.Database.MigrateAsync(ct);
    }
}
