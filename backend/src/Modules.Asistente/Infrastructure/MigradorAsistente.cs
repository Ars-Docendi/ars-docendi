using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
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
/// Conecta con la cadena del dueño de la base: conceder requiere ser dueño de la
/// tabla. El asistente nunca usa esta conexión para consultar.
/// </remarks>
internal sealed class MigradorAsistente(
    IOptions<OpcionesAsistente> opciones,
    IConfiguration configuracion,
    ILogger<MigradorAsistente> log) : IMigradorModulo
{
    public async Task MigrarAsync(CancellationToken ct)
    {
        var valores = opciones.Value;
        var cadena = configuracion.GetConnectionString("ArsDocendi")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'ArsDocendi'; sin ella no se pueden aplicar los privilegios del asistente.");

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);

        await PrivilegiosAsistente.AplicarAsync(
            conexion, valores.RolSoloLectura, valores.RolSoloLecturaPii, ct);

        log.LogInformation(
            "Privilegios del asistente aplicados a {RolSoloLectura} y {RolSoloLecturaPii}",
            valores.RolSoloLectura,
            valores.RolSoloLecturaPii);
    }
}
