using System.Text.Json;
using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace ArsDocendi.Migraciones;

/// <summary>Coordina contratos, nunca DbContexts; las escrituras quedan en cada adaptador.</summary>
public static class RunnerMigraciones
{
    public static async Task<int> EjecutarAsync(SolicitudMigracion solicitud,
        IReadOnlyList<IMigradorModulo> migradores, string cadena, string sha, CancellationToken ct)
    {
        try
        {
            if (solicitud.Modo == ModoMigracion.ValidarRecursos)
            {
                foreach (var migrador in migradores) migrador.ValidarRecursos();
                Console.WriteLine(JsonSerializer.Serialize(new { compatible = true, recursos = "verificados" }));
                return 0;
            }
            var estados = new List<EstadoMigracionesModulo>();
            // Preflight completo antes de la primera escritura de cualquier contexto.
            foreach (var migrador in migradores) estados.Add(await migrador.ConsultarAsync(ct));
            if (solicitud.Modo == ModoMigracion.Script)
            {
                var archivos = await PreviewMigraciones.GenerarAsync(migradores, estados, sha, ct);
                await PreviewMigraciones.ExportarAsync(archivos, solicitud.Directorio!, ct);
                return 0;
            }
            if (solicitud.Modo == ModoMigracion.Migrar)
            {
                foreach (var migrador in migradores) await migrador.MigrarAsync(ct);
                estados.Clear();
                foreach (var migrador in migradores) estados.Add(await migrador.ConsultarAsync(ct));
            }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                compatible = true,
                baseDatos = new NpgsqlConnectionStringBuilder(cadena).Database,
                contextos = estados,
                pendientes = estados.Sum(estado => estado.Pendientes.Count),
                sha,
            }, PreviewMigraciones.Json));
            return 0;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // No emitir mensajes del proveedor: pueden contener SQL, valores de filas o credenciales.
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                compatible = false,
                error = "Operación de migración rechazada o fallida; no se registra éxito. Revisar el destino y su historial.",
                tipo = error.GetType().Name,
                sqlState = (error as PostgresException)?.SqlState,
            }));
            return 1;
        }
    }
}
