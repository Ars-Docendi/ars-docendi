using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Borra de los dos registros lo que superó la ventana de retención (RNF-19).
/// </summary>
/// <remarks>
/// <b>«Retención de 90 días» sin un mecanismo que borre es una frase en un
/// documento.</b> Esta clase es lo que convierte la política en un hecho, y por eso
/// su test es parte del entregable y no un extra.
///
/// Corre en el proceso y no en la base. Un <c>pg_cron</c> exigiría una extensión más
/// en la provisión de cada ambiente y se testearía aparte del resto; un servicio
/// hospedado corre donde ya corre todo y se apaga con el módulo (RNF-20).
///
/// El corte se calcula contra el reloj inyectado, así que un test adelanta el tiempo
/// en vez de esperar noventa días.
/// </remarks>
internal sealed class PurgaDeRegistros(
    CadenaDuena cadena,
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<PurgaDeRegistros> log)
{
    /// <summary>
    /// Borra lo vencido. Idempotente: sin nada que borrar no falla ni escribe.
    /// </summary>
    /// <returns>Cuántas filas se borraron entre los dos registros.</returns>
    public async Task<int> PurgarAsync(CancellationToken ct)
    {
        var corte = reloj.GetUtcNow() - TimeSpan.FromDays(opciones.Value.RetencionDeRegistrosDias);

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        var operativas = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.registro_operativo WHERE ocurrido_en < @corte",
            "corte",
            corte,
            ct);

        // El analítico se corta por día porque es lo único que guarda. Se compara
        // contra el día del corte y no contra el instante: la fila de ese día ya
        // perdió la hora, así que no hay forma de saber si cayó antes o después.
        var analiticas = await BorrarAsync(
            conexion,
            "DELETE FROM asistente.registro_analitico WHERE dia < @corte",
            "corte",
            DateOnly.FromDateTime(corte.UtcDateTime),
            ct);

        var total = operativas + analiticas;

        if (total > 0)
        {
            log.LogInformation(
                "Purga del asistente: {Operativas} filas operativas y {Analiticas} analíticas anteriores a {Corte:yyyy-MM-dd}.",
                operativas,
                analiticas,
                corte);
        }

        return total;
    }

    private static async Task<int> BorrarAsync(
        NpgsqlConnection conexion,
        string sql,
        string parametro,
        object valor,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue(parametro, valor);

        return await comando.ExecuteNonQueryAsync(ct);
    }
}
