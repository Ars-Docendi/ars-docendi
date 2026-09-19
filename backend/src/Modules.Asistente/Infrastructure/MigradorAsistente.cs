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
/// No usa EF Core: el módulo no tiene entidades propias, así que no hay nada que
/// versionar con un historial de migraciones. Los scripts convergen por
/// construcción —<c>CREATE ... IF NOT EXISTS</c>, GRANT repetido y
/// <c>ADD COLUMN IF NOT EXISTS</c> son no-op la segunda vez—, que es justo lo que
/// <see cref="IMigradorModulo"/> pide.
///
/// Sin historial, nada garantiza que esa convergencia se haya escrito. Por eso
/// después de aplicar los scripts verifica que la base tenga las columnas que el
/// módulo escribe, y no arranca si le falta alguna: ver
/// <see cref="VerificarColumnasDelRegistroAsync"/>.
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

        await VerificarColumnasDelRegistroAsync(conexion, ct);

        log.LogInformation(
            "Privilegios del asistente aplicados a {RolSoloLectura} y {RolSoloLecturaPii}",
            valores.RolSoloLectura,
            valores.RolSoloLecturaPii);
    }

    /// <summary>
    /// Falla si a la base le falta alguna columna que el registro del turno escribe.
    /// </summary>
    /// <remarks>
    /// CIERRA EL MODO DE FALLA SILENCIOSO, que es el único que importa acá. Con una
    /// columna faltante, el INSERT del registro revienta en cada turno; el escritor
    /// se traga el fallo a propósito —para que la observabilidad no tumbe el
    /// servicio— y el endpoint sigue devolviendo 200. El resultado es un registro que
    /// deja de guardar sin que nada avise, y solo se descubre mirando la tabla. Pasó,
    /// y pasó desapercibido un día entero.
    ///
    /// Negarse a arrancar es la respuesta proporcionada: el arranque es el último
    /// momento en que alguien está mirando, y una migración a medias es exactamente
    /// lo que <c>--migrate</c> existe para no dejar pasar.
    ///
    /// La lista de columnas sale de <see cref="RegistroDelTurno.ColumnasQueEscribe"/>
    /// y no de una copia acá: una segunda lista solo puede coincidir con el INSERT o
    /// mentirle, y este chequeo existe justamente porque la que mentía no la miraba
    /// nadie.
    /// </remarks>
    private static async Task VerificarColumnasDelRegistroAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        var faltantes = new List<string>();

        foreach (var (tabla, columnas) in RegistroDelTurno.ColumnasQueEscribe)
        {
            var presentes = await ColumnasDeAsync(conexion, tabla, ct);

            faltantes.AddRange(columnas
                .Where(columna => !presentes.Contains(columna))
                .Select(columna => $"asistente.{tabla}.{columna}"));
        }

        if (faltantes.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "La base no tiene columnas que el registro del asistente escribe: "
            + string.Join(", ", faltantes.Order(StringComparer.Ordinal))
            + ". El proceso no arranca a propósito: sin ellas el INSERT del registro "
            + "falla en cada turno, el fallo se traga para no tumbar el servicio y el "
            + "registro deja de guardar en silencio. Si la columna es nueva, le falta "
            + "su ALTER TABLE ... ADD COLUMN IF NOT EXISTS en "
            + "database/asistente/002_asistente_registros.sql.");
    }

    private static async Task<HashSet<string>> ColumnasDeAsync(
        NpgsqlConnection conexion, string tabla, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT column_name FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = @tabla
            """,
            conexion);

        comando.Parameters.AddWithValue("tabla", tabla);

        var columnas = new HashSet<string>(StringComparer.Ordinal);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            columnas.Add(lector.GetString(0));
        }

        return columnas;
    }
}
