using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;
using NpgsqlTypes;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Parte cada turno en dos filas que no se pueden volver a juntar.
/// </summary>
/// <remarks>
/// Pide <see cref="CadenaDuena"/> y no una de solo lectura: los registros los
/// escribe la aplicación, y los roles del asistente tienen el schema
/// <c>asistente</c> revocado entero. Que el tipo lo diga evita descubrirlo en
/// runtime.
///
/// Las dos inserciones NO van en una transacción común. Una transacción las ataría
/// en el WAL y en el orden de commit, y lo que este código existe para lograr es
/// exactamente lo contrario. Si una falla y la otra no, se pierde media
/// observación: es el precio, y es menor que el de poder reconstruir quién preguntó
/// qué.
/// </remarks>
internal sealed class RegistroDelTurno(CadenaDuena cadena, ILogger<RegistroDelTurno> log)
    : IRegistroDelTurno
{
    public async Task RegistrarAsync(TurnoParaRegistrar turno, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turno);

        await EscribirAsync("operativo", conexion => OperativoAsync(conexion, turno, ct), ct);
        await EscribirAsync("analítico", conexion => AnaliticoAsync(conexion, turno, ct), ct);
    }

    private static async Task OperativoAsync(
        NpgsqlConnection conexion, TurnoParaRegistrar turno, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.registro_operativo
                (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo,
                 tokens_de_entrada, tokens_de_salida, latencia_ms, hubo_reintento,
                 truncado, proveedor, tokens_de_cache, intencion_sombra)
            VALUES (@actor, @cuando, @carril, @estado, @llamadas,
                    @entrada, @salida, @latencia, @reintento, @truncado, @proveedor,
                    @cache, @intencion)
            """,
            conexion);

        comando.Parameters.AddWithValue("actor", turno.Actor);
        comando.Parameters.AddWithValue("cuando", turno.Cuando);
        comando.Parameters.AddWithValue("carril", turno.Carril.ToString());
        comando.Parameters.AddWithValue("estado", turno.Estado.ToString());
        comando.Parameters.AddWithValue("llamadas", turno.LlamadasAlModelo);
        comando.Parameters.AddWithValue("entrada", turno.TokensDeEntrada);
        comando.Parameters.AddWithValue("salida", turno.TokensDeSalida);
        comando.Parameters.AddWithValue("latencia", turno.LatenciaMs);
        comando.Parameters.AddWithValue("reintento", turno.HuboReintento);
        comando.Parameters.AddWithValue("truncado", turno.Truncado);

        // Va al operativo y NO al analítico. En el analítico sería una columna más
        // por la cual agrupar preguntas, y con esta escala eso achica el conjunto
        // anónimo; acá es lo que permite atribuir el costo a quien lo generó.
        comando.Parameters.AddWithValue("proveedor", turno.Proveedor);
        comando.Parameters.AddWithValue("cache", turno.TokensDeCache);

        // También va solo al operativo, y el motivo es el mismo de arriba más uno
        // propio: las capturas son la minoría, así que cada intención concreta es un
        // valor raro, y un valor raro en el analítico es el selector que le daría
        // utilidad al canal residual de TD-012.
        //
        // Nulo se manda como nulo y no como cadena vacía: «no capturó» es el caso
        // normal, y una cadena vacía sería una intención sin nombre.
        comando.Parameters.AddWithValue(
            "intencion", NpgsqlDbType.Text, (object?)turno.IntencionSombra ?? DBNull.Value);

        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task AnaliticoAsync(
        NpgsqlConnection conexion, TurnoParaRegistrar turno, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.registro_analitico (pregunta, categoria, estado, dia)
            VALUES (@pregunta, @categoria, @estado, @dia)
            """,
            conexion);

        comando.Parameters.AddWithValue("pregunta", turno.Pregunta);
        comando.Parameters.AddWithValue("categoria", turno.Categoria);
        comando.Parameters.AddWithValue("estado", turno.Estado.ToString());

        // EL REDONDEO. Con la hora puesta, un join por tiempo contra el registro
        // operativo devolvería el autor de cada pregunta.
        //
        // Quien garantiza que la hora se pierda es el TIPO de la columna, que es
        // `date`: aunque acá se mandara un timestamp completo, el motor lo trunca.
        // Esta conversión es explícita igual, para que el código diga lo mismo que
        // el esquema; el test que sostiene la propiedad es el que verifica el tipo.
        comando.Parameters.AddWithValue("dia", DateOnly.FromDateTime(turno.Cuando.UtcDateTime));

        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task EscribirAsync(
        string cual, Func<NpgsqlConnection, Task> escribir, CancellationToken ct)
    {
        try
        {
            await using var conexion = new NpgsqlConnection(cadena.Valor);
            await conexion.OpenAsync(ct);
            await escribir(conexion);
        }
        catch (Exception excepcion) when (excepcion is NpgsqlException or InvalidOperationException)
        {
            // Se traga el fallo a propósito: el turno ya se resolvió y el usuario ya
            // tiene su respuesta. Lo que se pierde es una observación; lo que se
            // evitaría perdiendo menos es el servicio entero.
            log.LogError(
                excepcion, "No se pudo escribir el registro {Cual} del asistente.", cual);
        }
    }
}
