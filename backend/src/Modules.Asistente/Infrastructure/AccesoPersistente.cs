using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// <see cref="IAccesoAlAsistente"/> sobre Postgres (asistente-acceso-granular).
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> por el mismo motivo que
/// <see cref="CuotaPersistente"/>: lee <c>presupuesto_rol</c> y
/// <c>acceso_usuario_revocado</c>, dentro del schema <c>asistente</c> que los
/// roles de solo lectura tienen revocado entero.
/// </remarks>
internal sealed class AccesoPersistente(CadenaDuena cadena, IConsultasIdentity identidad)
    : IAccesoAlAsistente
{
    public async Task<bool> TieneAccesoAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using (var revocacion = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM asistente.acceso_usuario_revocado WHERE actor_id = @actor)",
            conexion))
        {
            revocacion.Parameters.AddWithValue("actor", actor);
            if ((bool)(await revocacion.ExecuteScalarAsync(ct))!)
            {
                return ReglaDeAccesoEfectivo.Resolver(true, [], new Dictionary<string, bool>()).Acceso;
            }
        }

        var codigosDeRol = await identidad.ObtenerCodigosDeRolesDeSistemaAsync(actor, ct);
        if (codigosDeRol.Count == 0)
        {
            return ReglaDeAccesoEfectivo.Resolver(false, codigosDeRol, new Dictionary<string, bool>()).Acceso;
        }

        await using var roles = new NpgsqlCommand(
            "SELECT rol_code, acceso_habilitado FROM asistente.presupuesto_rol WHERE rol_code = ANY(@roles)",
            conexion);
        roles.Parameters.AddWithValue("roles", codigosDeRol.ToArray());

        var accesoPorRol = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using (var lector = await roles.ExecuteReaderAsync(ct))
        {
            while (await lector.ReadAsync(ct))
            {
                accesoPorRol[lector.GetString(0)] = lector.GetBoolean(1);
            }
        }

        return ReglaDeAccesoEfectivo.Resolver(false, codigosDeRol, accesoPorRol).Acceso;
    }
}
