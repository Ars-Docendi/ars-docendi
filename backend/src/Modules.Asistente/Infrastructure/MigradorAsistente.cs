using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Aplica los privilegios de lectura del asistente. Interno al módulo: el Host
/// lo resuelve solo a través de <see cref="IMigradorModulo"/>.
/// </summary>
/// <remarks>
/// No usa EF Core: el módulo no tiene entidades ni schema propio, así que no hay
/// nada que versionar con un historial de migraciones. El script es idempotente
/// por construcción —<c>CREATE EXTENSION IF NOT EXISTS</c> y GRANT repetidos son
/// no-op—, que es justo lo que <see cref="IMigradorModulo"/> pide.
///
/// Se registra último en la composición del Host, así que corre después de las
/// migraciones que crean las tablas de <c>identity</c> y <c>designaciones</c>.
/// Sin esas tablas, cada GRANT fallaría con «relation does not exist».
///
/// Pide <see cref="CadenaDuena"/> y no una cadena cualquiera: conceder requiere
/// ser dueño de la tabla, así que con una de solo lectura este código no podría
/// hacer su trabajo. Que el tipo lo diga evita descubrirlo en runtime.
/// </remarks>
internal sealed class MigradorAsistente(
    IOptions<OpcionesAsistente> opciones,
    CadenaDuena cadena,
    ILogger<MigradorAsistente> log) : IMigradorModulo
{
    public async Task MigrarAsync(CancellationToken ct)
    {
        var valores = opciones.Value;

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await PrivilegiosAsistente.AplicarAsync(
            conexion, valores.RolSoloLectura, valores.RolSoloLecturaPii, ct);

        // Después de los privilegios y no antes: el DDL de los registros revoca su
        // propio schema a los dos roles, y para eso los roles ya tienen que existir.
        await RegistrosAsistente.AplicarAsync(
            conexion, valores.RolSoloLectura, valores.RolSoloLecturaPii, ct);

        log.LogInformation(
            "Privilegios del asistente aplicados a {RolSoloLectura} y {RolSoloLecturaPii}",
            valores.RolSoloLectura,
            valores.RolSoloLecturaPii);
    }
}
