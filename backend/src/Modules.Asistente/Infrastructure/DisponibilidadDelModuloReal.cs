using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IDisponibilidadDelModulo"/> sobre la fila única
/// <c>asistente.modo_mantenimiento</c> (design.md D7 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Sin ninguna caché de proceso, a propósito (tarea 6.7): cada consulta lee
/// la fila de nuevo. Cachear el valor en memoria reintroduciría exactamente
/// el defecto que este mecanismo reemplaza —dos instancias del Host verían
/// valores distintos hasta que la caché de la segunda expirara—, y una fila
/// de una sola columna booleana es demasiado barata para justificar el
/// riesgo.
/// </remarks>
internal sealed class DisponibilidadDelModuloReal(CadenaDuena cadena) : IDisponibilidadDelModulo
{
    public async Task<EstadoDeMantenimiento> ConsultarAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            "SELECT activo, razon FROM asistente.modo_mantenimiento WHERE id = 1", conexion);

        await using var lector = await comando.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct))
        {
            // No debería pasar: 006 siembra la fila única. Si algún día no
            // está, "no hay mantenimiento" es la lectura conservadora y
            // segura — nunca bloquear al sistema entero por una fila
            // faltante.
            return new EstadoDeMantenimiento(false, null);
        }

        return new EstadoDeMantenimiento(
            lector.GetBoolean(0), lector.IsDBNull(1) ? null : lector.GetString(1));
    }

    public async Task ActivarAsync(Guid actor, string razon, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razon);
        await EscribirAsync(actor, activo: true, razon, ct);
    }

    public Task DesactivarAsync(Guid actor, CancellationToken ct) =>
        EscribirAsync(actor, activo: false, razon: null, ct);

    private async Task EscribirAsync(Guid actor, bool activo, string? razon, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.modo_mantenimiento
               SET activo = @activo, razon = @razon, actor_id = @actor, actualizado_en = now()
             WHERE id = 1
            """, conexion);
        comando.Parameters.AddWithValue("activo", activo);
        comando.Parameters.AddWithValue("razon", (object?)razon ?? DBNull.Value);
        comando.Parameters.AddWithValue("actor", actor);

        await comando.ExecuteNonQueryAsync(ct);
    }
}
