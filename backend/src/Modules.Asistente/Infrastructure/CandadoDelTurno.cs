using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Un turno en curso por actor, con un advisory lock de sesión de Postgres
/// (asistente-turno-exclusivo-del-actor, design.md D5 de asistente-administracion-de-uso).
/// </summary>
public sealed class CandadoDelTurno : IAsyncDisposable
{
    private readonly NpgsqlConnection _conexion;
    private readonly Guid _actor;
    private bool _liberado;

    private CandadoDelTurno(NpgsqlConnection conexion, Guid actor)
    {
        _conexion = conexion;
        _actor = actor;
    }

    /// <summary>
    /// Intenta tomar el candado del actor. Devuelve <c>null</c> si ya está
    /// tomado — nunca lanza por eso, que es el caso esperado y no una falla.
    /// </summary>
    public static async Task<CandadoDelTurno?> IntentarAsync(
        CadenaDuena cadena, Guid actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cadena);

        // Sin pool: esta conexión es del candado y de nadie más, así que devolverla al pool no
        // tendría sentido — el candado de sesión seguiría atado a ella hasta que Postgres cierre el
        // backend, y un pool que la reutilizara le pasaría el candado colgado a quien la tome
        // después.
        var builder = new NpgsqlConnectionStringBuilder(cadena.Valor) { Pooling = false };
        var conexion = new NpgsqlConnection(builder.ConnectionString);

        try
        {
            await conexion.OpenAsync(ct);

            await using var comando = new NpgsqlCommand(SqlTomar, conexion);
            comando.Parameters.AddWithValue("actor", actor.ToString());

            var obtenido = (bool)(await comando.ExecuteScalarAsync(ct))!;
            if (!obtenido)
            {
                await conexion.DisposeAsync();
                return null;
            }

            return new CandadoDelTurno(conexion, actor);
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Libera el candado y cierra la conexión dedicada. Idempotente: llamarlo
    /// dos veces no falla ni intenta liberar dos veces.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_liberado)
        {
            return;
        }

        _liberado = true;

        try
        {
            // CancellationToken.None a propósito: liberar el candado tiene que pasar incluso si el
            // token que trajo el turno ya se canceló — es exactamente la situación (timeout del
            // presupuesto, cancelación del cliente) que este candado tiene que sobrevivir.
            if (_conexion.State == System.Data.ConnectionState.Open)
            {
                await using var comando = new NpgsqlCommand(SqlLiberar, _conexion);
                comando.Parameters.AddWithValue("actor", _actor.ToString());
                await comando.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        finally
        {
            await _conexion.DisposeAsync();
        }
    }

    // La clave es hashtext('asistente:turno:' || actor), calculada en SQL y no en C#: así el mismo
    // cálculo determinista lo hace un solo motor —el de Postgres— y tomar y liberar nunca pueden
    // desincronizarse por una reimplementación del hash en dos lenguajes.
    private const string SqlTomar =
        "SELECT pg_try_advisory_lock(hashtext('asistente:turno:' || @actor))";

    private const string SqlLiberar =
        "SELECT pg_advisory_unlock(hashtext('asistente:turno:' || @actor))";
}
