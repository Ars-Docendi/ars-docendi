using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;
using NpgsqlTypes;

namespace Modules.Asistente.Infrastructure;

/// <summary>Parte cada turno en dos filas que no se pueden volver a juntar.</summary>
internal sealed class RegistroDelTurno(CadenaDuena cadena, ILogger<RegistroDelTurno> log)
    : IRegistroDelTurno
{
    /// <summary>Las columnas que este escritor pone, tabla por tabla.</summary>
    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnasQueEscribe =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["registro_operativo"] =
            [
                "actor_id", "ocurrido_en", "carril", "estado", "llamadas_al_modelo",
                "tokens_de_entrada", "tokens_de_salida", "latencia_ms", "hubo_reintento",
                "truncado", "proveedor", "tokens_de_cache", "intencion_sombra", "motivo_rechazo",
            ],
            ["registro_analitico"] = ["id", "pregunta", "categoria", "estado", "dia", "causa", "modelo"],
        };

    private static readonly string InsertarEnOperativo = Insertar("registro_operativo");
    private static readonly string InsertarEnAnalitico = Insertar("registro_analitico");

    public async Task RegistrarAsync(TurnoParaRegistrar turno, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turno);

        await EscribirAsync("operativo", conexion => OperativoAsync(conexion, turno, ct), ct);
        await EscribirAsync("analítico", conexion => AnaliticoAsync(conexion, turno, ct), ct);
    }

    private static async Task OperativoAsync(
        NpgsqlConnection conexion, TurnoParaRegistrar turno, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(InsertarEnOperativo, conexion);

        comando.Parameters.AddWithValue("actor_id", turno.Actor);
        comando.Parameters.AddWithValue("ocurrido_en", turno.Cuando);
        comando.Parameters.AddWithValue("carril", turno.Carril.ToString());
        comando.Parameters.AddWithValue("estado", turno.Estado.ToString());
        comando.Parameters.AddWithValue("llamadas_al_modelo", turno.LlamadasAlModelo);
        comando.Parameters.AddWithValue("tokens_de_entrada", turno.TokensDeEntrada);
        comando.Parameters.AddWithValue("tokens_de_salida", turno.TokensDeSalida);
        comando.Parameters.AddWithValue("latencia_ms", turno.LatenciaMs);
        comando.Parameters.AddWithValue("hubo_reintento", turno.HuboReintento);
        comando.Parameters.AddWithValue("truncado", turno.Truncado);

        // Va al operativo y NO al analítico. En el analítico sería una columna más
        // por la cual agrupar preguntas, y con esta escala eso achica el conjunto
        // anónimo; acá es lo que permite atribuir el costo a quien lo generó.
        comando.Parameters.AddWithValue("proveedor", turno.Proveedor);
        comando.Parameters.AddWithValue("tokens_de_cache", turno.TokensDeCache);

        // También va solo al operativo, y el motivo es el mismo de arriba más uno propio: las
        // capturas son la minoría, así que cada intención concreta es un valor raro, y un valor
        // raro en el analítico es el selector que le daría utilidad al canal residual de TD-012.
        comando.Parameters.AddWithValue(
            "intencion_sombra", NpgsqlDbType.Text, (object?)turno.IntencionSombra ?? DBNull.Value);

        // También nulo por omisión, y también es el caso normal (asistente- rechazos-dinamicos):
        // sólo un rechazo declarado por el modelo lo trae.
        comando.Parameters.AddWithValue(
            "motivo_rechazo",
            NpgsqlDbType.Text,
            turno.MotivoDeRechazo is { } motivo ? MotivosDeRechazo.ValorDeCable(motivo) : DBNull.Value);

        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task AnaliticoAsync(
        NpgsqlConnection conexion, TurnoParaRegistrar turno, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(InsertarEnAnalitico, conexion);

        // Supplied by the caller and not left to the column's own DEFAULT gen_random_uuid(): on a
        // Respondida turn this is the exact id already handed to the client as
        // ResultadoDelTurno.ClaveDeRetroalimentacion, so the feedback token and the row it rates
        // have to be the same value from the moment either one exists.
        comando.Parameters.AddWithValue("id", turno.AnaliticoId);
        comando.Parameters.AddWithValue("pregunta", turno.Pregunta);
        comando.Parameters.AddWithValue("categoria", turno.Categoria);
        comando.Parameters.AddWithValue("estado", turno.Estado.ToString());

        // EL REDONDEO. Con la hora puesta, un join por tiempo contra el registro operativo
        // devolvería el autor de cada pregunta.
        comando.Parameters.AddWithValue("dia", DateOnly.FromDateTime(turno.Cuando.UtcDateTime));

        // Two coarse attributes of the TURN, never of the person (design D2/D3 of
        // asistente-metricas-de-calidad): a closed failure code, and the same empresa/modelo string
        // the operational record carries.
        comando.Parameters.AddWithValue(
            "causa",
            NpgsqlDbType.Text,
            turno.Causa is { } causa ? CausasDelTurno.ValorDeCable(causa) : DBNull.Value);
        comando.Parameters.AddWithValue(
            "modelo", NpgsqlDbType.Text, (object?)turno.Modelo ?? DBNull.Value);

        await comando.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A plain UPDATE by id, and nothing else: `reemplazado` is the only column it
    /// touches, so it cannot add an actor or a time to the analytic record (TD-012).
    /// Not part of <see cref="ColumnasQueEscribe"/> because no INSERT writes it; the
    /// migrator checks it explicitly.
    /// </remarks>
    public Task MarcarReemplazadoAsync(Guid analiticoId, CancellationToken ct) =>
        EscribirAsync(
            "analítico (reemplazado)",
            async conexion =>
            {
                await using var comando = new NpgsqlCommand(
                    "UPDATE asistente.registro_analitico SET reemplazado = true WHERE id = @id",
                    conexion);
                comando.Parameters.AddWithValue("id", analiticoId);
                await comando.ExecuteNonQueryAsync(ct);
            },
            ct);

    /// <summary>Arma el INSERT de una tabla desde <see cref="ColumnasQueEscribe"/>.</summary>
    private static string Insertar(string tabla)
    {
        var columnas = ColumnasQueEscribe[tabla];

        return $"INSERT INTO asistente.{tabla} ({string.Join(", ", columnas)}) "
            + $"VALUES ({string.Join(", ", columnas.Select(columna => "@" + columna))})";
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
            // Se traga el fallo a propósito: el turno ya se resolvió y el usuario ya tiene su
            // respuesta.
            log.LogError(
                excepcion, "No se pudo escribir el registro {Cual} del asistente.", cual);
        }
    }
}
