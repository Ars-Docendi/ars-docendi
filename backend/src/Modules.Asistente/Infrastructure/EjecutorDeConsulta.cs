using System.Data;
using System.Globalization;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Npgsql;
using Npgsql.Schema;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Ejecuta la consulta generada acotada al actor. Las <b>policies RLS</b> filtran las filas según
/// el actor fijado.
/// </summary>
internal sealed class EjecutorDeConsulta(
    AperturaDeLectura apertura,
    IClasificadorDeSensibilidad clasificador,
    IOptions<OpcionesAsistente> opciones) : IEjecutorDeConsulta
{
    public async Task<ResultadoDeConsulta> EjecutarAsync(
        string sql,
        Guid actor,
        bool conDatosPersonales,
        CancellationToken ct,
        IReadOnlyDictionary<string, Guid>? marcadores = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var valores = opciones.Value;

        // Reescribe $refN -> @refN ANTES de abrir la conexión: es puro (no toca la base), y si algo
        // estuviera mal —un marcador sin binding, señal de que el validador y este ejecutor
        // divergieron— revienta sin haber gastado una transacción.
        var (sqlParaEjecutar, bindings) = marcadores is { Count: > 0 }
            ? ReescritorDeMarcadores.Reescribir(sql, marcadores)
            : (sql, (IReadOnlyDictionary<string, Guid>)new Dictionary<string, Guid>());

        // Conexión y transacción NUEVAS por ejecución, también en el reintento. Reusar la
        // transacción dejaría que una segunda ejecución heredara el ajuste de la primera.
        await using var conexion = await apertura.AbrirAsync(conDatosPersonales, ct);

        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        // El rechazo del motor sale traducido al tipo del módulo. Quien decide qué
        // contestarle a una persona no tiene por qué conocer el driver de la base,
        // y el mensaje crudo de PostgreSQL nombra tablas y columnas.
        try
        {
            await PreambuloDelActor.AplicarAsync(
                conexion, transaccion, actor, ct, valores.TimeoutDeSentenciaMs);

            // La resolución del manifiesto va antes de leer: si fallara después, ya
            // tendríamos las filas en memoria sin saber cuáles se pueden mandar afuera.
            await clasificador.PrepararAsync(ct);

            return await LeerAsync(
                conexion, transaccion, sqlParaEjecutar, valores.TopeDeFilas, clasificador, bindings, ct);
        }
        catch (PostgresException excepcion)
        {
            throw FallaDelMotor.Traducir(excepcion);
        }
    }

    /// <summary>Ejecuta la consulta envuelta y devuelve las filas ya recortadas.</summary>
    private static async Task<ResultadoDeConsulta> LeerAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        string sql,
        int tope,
        IClasificadorDeSensibilidad clasificador,
        IReadOnlyDictionary<string, Guid> bindings,
        CancellationToken ct)
    {
        var envuelta = $"SELECT * FROM (\n{sql}\n) AS resultado_asistente LIMIT {tope + 1}";

        await using var comando = new NpgsqlCommand(envuelta, conexion, transaccion);

        // Los `@refN` que reescribió el validador se ligan como `uuid`, nunca interpolados: es la
        // frontera que hace que un identificador interno no pueda llegar al modelo por ningún
        // camino, ni siquiera el de una consulta que lo carga en el WHERE (design.md D11).
        foreach (var (nombre, id) in bindings)
        {
            comando.Parameters.Add(new NpgsqlParameter(nombre, NpgsqlTypes.NpgsqlDbType.Uuid) { Value = id });
        }

        await using var lector = await comando.ExecuteReaderAsync(ct);

        var columnas = new List<string>(lector.FieldCount);
        var sensibilidad = new List<SensibilidadDeColumna>(lector.FieldCount);

        // El esquema de columnas trae el identificador de tabla y el número de atributo que el
        // motor reportó para cada una.
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
