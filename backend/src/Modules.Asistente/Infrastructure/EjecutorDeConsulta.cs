using System.Data;
using System.Globalization;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Npgsql;
using Npgsql.Schema;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Ejecuta la consulta generada acotada al actor.
/// </summary>
/// <remarks>
/// Tres capas, en este orden y cada una independiente de las otras:
///
/// 1. El <b>rol</b> con que conecta no tiene ningún privilegio de mutación.
/// 2. La <b>transacción</b> se declara <c>READ ONLY</c>.
/// 3. Las <b>policies RLS</b> filtran las filas según el actor fijado.
///
/// El validador es una cuarta capa que corre antes, fuera de esta clase. Si
/// fallara entera, las tres de acá siguen en pie.
/// </remarks>
internal sealed class EjecutorDeConsulta(
    CadenaSoloLectura cadenaBasica,
    CadenaSoloLecturaPii cadenaConDatosPersonales,
    IClasificadorDeSensibilidad clasificador,
    IOptions<OpcionesAsistente> opciones) : IEjecutorDeConsulta
{
    /// <summary>
    /// Ajuste transaction-local donde viaja el actor. Lo leen las funciones
    /// <c>SECURITY DEFINER</c> del schema <c>identity</c>.
    /// </summary>
    private const string AjusteDelActor = "app.asistente_user_id";

    public async Task<ResultadoDeConsulta> EjecutarAsync(
        string sql, Guid actor, bool conDatosPersonales, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var valores = opciones.Value;
        var cadena = new NpgsqlConnectionStringBuilder(
            conDatosPersonales ? cadenaConDatosPersonales.Valor : cadenaBasica.Valor)
        {
            CommandTimeout = valores.TimeoutDeComandoSegundos,
        }.ConnectionString;

        // Conexión y transacción NUEVAS por ejecución, también en el reintento.
        // Reusar la transacción dejaría que una segunda ejecución heredara el
        // ajuste de la primera.
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);

        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        await PrepararTransaccionAsync(conexion, transaccion, actor, valores, ct);

        // La resolución del manifiesto va antes de leer: si fallara después, ya
        // tendríamos las filas en memoria sin saber cuáles se pueden mandar afuera.
        await clasificador.PrepararAsync(ct);

        return await LeerAsync(conexion, transaccion, sql, valores.TopeDeFilas, clasificador, ct);
    }

    /// <summary>
    /// Deja la transacción en solo lectura, con el timeout de sentencia y con el
    /// actor fijado.
    /// </summary>
    /// <remarks>
    /// El tercer parámetro de <c>set_config</c> en verdadero es lo que hace el
    /// ajuste <b>transaction-local</b>. Con una variante de sesión, el ajuste
    /// sobreviviría al <c>COMMIT</c> y a la devolución de la conexión al pool, y
    /// el turno siguiente que tomara esa conexión física heredaría el actor del
    /// anterior. Ese fallo no tira error: responde con el alcance equivocado.
    ///
    /// <c>SET TRANSACTION READ ONLY</c> va antes que cualquier otra cosa de la
    /// transacción porque PostgreSQL no lo admite una vez que la transacción tocó
    /// datos.
    /// </remarks>
    private static async Task PrepararTransaccionAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        Guid actor,
        OpcionesAsistente valores,
        CancellationToken ct)
    {
        await using (var soloLectura = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", conexion, transaccion))
        {
            await soloLectura.ExecuteNonQueryAsync(ct);
        }

        await using var preparacion = new NpgsqlCommand(
            $"""
            SELECT set_config('statement_timeout', @timeout, true),
                   set_config('{AjusteDelActor}', @actor, true)
            """, conexion, transaccion);

        preparacion.Parameters.AddWithValue(
            "timeout",
            valores.TimeoutDeSentenciaMs.ToString(CultureInfo.InvariantCulture));
        preparacion.Parameters.AddWithValue("actor", actor.ToString());

        await preparacion.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Ejecuta la consulta envuelta y devuelve las filas ya recortadas.
    /// </summary>
    /// <remarks>
    /// <b>La envoltura pide una fila de más a propósito.</b> Con un límite exacto,
    /// «devolvió N» y «devolvió más de N y se recortó» son indistinguibles desde
    /// el resultado, y la redacción termina afirmando un total sobre un recorte —
    /// una afirmación falsa producida por el sistema, no por el modelo. Con la
    /// fila sonda, la distinción es aritmética.
    ///
    /// La consulta generada ya pasó por el validador, así que interpolarla acá no
    /// abre nada que el validador no haya mirado. Parametrizarla es imposible: es
    /// código, no un valor.
    /// </remarks>
    private static async Task<ResultadoDeConsulta> LeerAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        string sql,
        int tope,
        IClasificadorDeSensibilidad clasificador,
        CancellationToken ct)
    {
        var envuelta = $"SELECT * FROM (\n{sql}\n) AS resultado_asistente LIMIT {tope + 1}";

        await using var comando = new NpgsqlCommand(envuelta, conexion, transaccion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        var columnas = new List<string>(lector.FieldCount);
        var sensibilidad = new List<SensibilidadDeColumna>(lector.FieldCount);

        // El esquema de columnas trae el identificador de tabla y el número de
        // atributo que el motor reportó para cada una. Se clasifica ACÁ y no en la
        // capa de aplicación porque es el único punto donde esos identificadores
        // existen: más arriba solo quedan los alias, que no dicen de dónde vino la
        // columna.
        var esquema = lector.GetColumnSchema();

        for (var indice = 0; indice < lector.FieldCount; indice++)
        {
            columnas.Add(lector.GetName(indice));
            sensibilidad.Add(clasificador.Clasificar(
                esquema[indice].TableOID,
                esquema[indice].ColumnAttributeNumber ?? 0));
        }

        var filas = new List<IReadOnlyList<object?>>();
        var truncado = false;

        while (await lector.ReadAsync(ct))
        {
            if (filas.Count == tope)
            {
                // Ésta es la fila sonda. Se descarta acá: nunca sale del ejecutor,
                // así que no llega ni al modelo ni al cliente.
                truncado = true;
                break;
            }

            var fila = new object?[lector.FieldCount];
            for (var indice = 0; indice < lector.FieldCount; indice++)
            {
                fila[indice] = lector.IsDBNull(indice) ? null : lector.GetValue(indice);
            }

            filas.Add(fila);
        }

        return new ResultadoDeConsulta(columnas, filas, truncado, sensibilidad);
    }
}
