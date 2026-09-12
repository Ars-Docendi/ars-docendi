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
    /// <summary>Las columnas que este escritor pone, tabla por tabla.</summary>
    /// <remarks>
    /// ES LA ÚNICA LISTA, Y TIENE QUE SEGUIR SIÉNDOLO. Los dos INSERT de abajo se
    /// arman desde acá, y <see cref="MigradorAsistente"/> verifica contra acá que la
    /// base las tenga antes de dejar arrancar. Escribirla dos veces —una acá y otra
    /// en el chequeo— haría que el próximo desajuste fuera entre las dos listas, que
    /// es el mismo defecto una capa más arriba y sin nadie que lo mire.
    ///
    /// Los nombres son a la vez los de las columnas y los de los parámetros: el
    /// INSERT se arma con ambos desde esta lista, así que agregar una columna es
    /// agregar un renglón acá y su <c>AddWithValue</c> con el mismo nombre.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnasQueEscribe =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["registro_operativo"] =
            [
                "actor_id", "ocurrido_en", "carril", "estado", "llamadas_al_modelo",
                "tokens_de_entrada", "tokens_de_salida", "latencia_ms", "hubo_reintento",
                "truncado", "proveedor", "tokens_de_cache", "intencion_sombra",
            ],
            ["registro_analitico"] = ["pregunta", "categoria", "estado", "dia"],
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

        // También va solo al operativo, y el motivo es el mismo de arriba más uno
        // propio: las capturas son la minoría, así que cada intención concreta es un
        // valor raro, y un valor raro en el analítico es el selector que le daría
        // utilidad al canal residual de TD-012.
        //
        // Nulo se manda como nulo y no como cadena vacía: «no capturó» es el caso
        // normal, y una cadena vacía sería una intención sin nombre.
        comando.Parameters.AddWithValue(
            "intencion_sombra", NpgsqlDbType.Text, (object?)turno.IntencionSombra ?? DBNull.Value);

        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task AnaliticoAsync(
        NpgsqlConnection conexion, TurnoParaRegistrar turno, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(InsertarEnAnalitico, conexion);

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

    /// <summary>
    /// Arma el INSERT de una tabla desde <see cref="ColumnasQueEscribe"/>.
    /// </summary>
    /// <remarks>
    /// Las columnas no se interpolan de ningún valor de afuera: salen de la
    /// constante de arriba y nada más. Los valores siguen viajando por parámetro.
    /// </remarks>
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
            // Se traga el fallo a propósito: el turno ya se resolvió y el usuario ya
            // tiene su respuesta. Lo que se pierde es una observación; lo que se
            // evitaría perdiendo menos es el servicio entero.
            log.LogError(
                excepcion, "No se pudo escribir el registro {Cual} del asistente.", cual);
        }
    }
}
