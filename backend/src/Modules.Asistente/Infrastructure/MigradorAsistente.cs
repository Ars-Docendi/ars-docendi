using System.Security.Cryptography;
using System.Text;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Aplica los privilegios de lectura del asistente. Interno al módulo: el Host lo resuelve solo a
/// través de <see cref="IMigradorModulo"/>. Sin esas tablas, cada GRANT fallaría con «relation does
/// not exist».
/// </summary>
internal sealed class MigradorAsistente(
    IOptions<OpcionesAsistente> opciones,
    CadenaDuena cadena,
    ILogger<MigradorAsistente> log) : IMigradorModulo
{
    /// <summary>Recurso con la tabla de la versión aplicada.</summary>
    public const string RecursoVersion = "asistente/007_asistente_version.sql";

    /// <summary>Todos los recursos que aplica, en orden.</summary>
    private static readonly string[] Recursos =
    [
        PrivilegiosAsistente.RecursoSql,
        RegistrosAsistente.RecursoSql,
        RetroalimentacionAsistente.RecursoSql,
        HistorialAsistente.RecursoSql,
        AuditoriaDeSoporteAsistente.RecursoSql,
        AdministracionAsistente.RecursoSql,
        AdministracionAsistente.RecursoSqlVigenteUnico,
        ModeloAsistente.RecursoSql,
        CalidadAsistente.RecursoSql,
        RecursoVersion,
    ];

    public string Contexto => "asistente";

    public void ValidarRecursos()
    {
        foreach (var recurso in Recursos)
        {
            _ = RecursosSql.Leer(typeof(MigradorAsistente).Assembly, recurso);
        }
    }

    public async Task<EstadoMigracionesModulo> ConsultarAsync(CancellationToken ct)
    {
        var disponible = Huella();

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        // Dos consultas y no un CASE: PostgreSQL resuelve las tablas al parsear,
        // así que nombrar una que todavía no existe falla aunque la rama no corra.
        string? aplicada = null;
        await using (var existe = new NpgsqlCommand(
            "SELECT to_regclass('asistente.version_aplicada') IS NOT NULL", conexion))
        {
            if (await existe.ExecuteScalarAsync(ct) is true)
            {
                await using var version = new NpgsqlCommand(
                    "SELECT huella FROM asistente.version_aplicada", conexion);
                aplicada = await version.ExecuteScalarAsync(ct) as string;
            }
        }

        return aplicada == disponible
            ? new(Contexto, [disponible], [disponible], [])
            : new(Contexto, [disponible], [], [disponible]);
    }

    /// <summary>
    /// El SQL que <see cref="MigrarAsync"/> ejecutaría, para el preview del deploy.
    /// </summary>
    public async Task<string> GenerarScriptAsync(CancellationToken ct)
    {
        var estado = await ConsultarAsync(ct);
        if (estado.Pendientes.Count == 0)
        {
            return string.Empty;
        }

        var valores = opciones.Value;
        var script = new StringBuilder()
            .AppendLine("BEGIN;")
            .AppendLine($"SELECT set_config('app.asistente_rol_basico', {Literal(valores.RolSoloLectura)}, true),")
            .AppendLine($"       set_config('app.asistente_rol_pii', {Literal(valores.RolSoloLecturaPii)}, true);");

        foreach (var recurso in Recursos)
        {
            script.AppendLine($"-- {recurso}")
                .AppendLine(RecursosSql.Leer(typeof(MigradorAsistente).Assembly, recurso));
        }

        return script
            .AppendLine(SqlRegistrarVersion.Replace("@huella", $"'{estado.Disponibles[0]}'", StringComparison.Ordinal))
            .AppendLine("COMMIT;")
            .ToString();
    }

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

        // The feedback table needs no role GUCs (it lives inside a schema already
        // denied wholesale), but it does need registro_analitico to already exist,
        // since its primary key is a foreign key into that table.
        await RetroalimentacionAsistente.AplicarAsync(conexion, ct);

        // Same reason as the feedback table: no role GUCs needed, both live inside the already
        // wholesale-denied `asistente` schema.
        await HistorialAsistente.AplicarAsync(conexion, ct);
        await AuditoriaDeSoporteAsistente.AplicarAsync(conexion, ct);

        // Same reason as the two calls above: no role GUCs needed, lives inside the already
        // wholesale-denied `asistente` schema.
        await AdministracionAsistente.AplicarAsync(conexion, ct);

        // Same reason: runtime-model tables (credential, selection, catalog),
        // inside the wholesale-denied schema; inert until an administrator uses them.
        await ModeloAsistente.AplicarAsync(conexion, ct);

        // Quality columns on registro_analitico plus the review audit table
        // (asistente-metricas-de-calidad). After RegistrosAsistente: it alters its table.
        await CalidadAsistente.AplicarAsync(conexion, ct);

        await VerificarColumnasDelRegistroAsync(conexion, ct);

        // Último: la versión sólo queda registrada si todo lo anterior se aplicó.
        await using (var version = new NpgsqlCommand(
            RecursosSql.Leer(typeof(MigradorAsistente).Assembly, RecursoVersion), conexion))
        {
            await version.ExecuteNonQueryAsync(ct);
        }

        await using (var registro = new NpgsqlCommand(SqlRegistrarVersion, conexion))
        {
            registro.Parameters.AddWithValue("huella", Huella());
            await registro.ExecuteNonQueryAsync(ct);
        }

        log.LogInformation(
            "Privilegios del asistente aplicados a {RolSoloLectura} y {RolSoloLecturaPii}",
            valores.RolSoloLectura,
            valores.RolSoloLecturaPii);
    }

    private static string Literal(string valor) => $"'{valor.Replace("'", "''", StringComparison.Ordinal)}'";

    private const string SqlRegistrarVersion =
        """
        INSERT INTO asistente.version_aplicada (huella) VALUES (@huella)
        ON CONFLICT (unica) DO UPDATE SET huella = EXCLUDED.huella, aplicada_en = now();
        """;

    /// <summary>
    /// <c>asistente_</c> + SHA-256 de los scripts, en orden, y de los dos nombres de
    /// rol: cambiar cualquiera de ellos es una versión nueva que hay que aplicar.
    /// </summary>
    private string Huella()
    {
        var valores = opciones.Value;
        var contenido = new StringBuilder();

        foreach (var recurso in Recursos)
        {
            contenido.Append(recurso).Append('\n')
                .Append(RecursosSql.Leer(typeof(MigradorAsistente).Assembly, recurso)).Append('\n');
        }

        contenido.Append(valores.RolSoloLectura).Append('\n').Append(valores.RolSoloLecturaPii);

        return "asistente_"
            + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido.ToString())))[..16];
    }

    /// <summary>
    /// Columns written NOT by the two INSERTs of <see cref="RegistroDelTurno"/> but by something
    /// else, checked explicitly because nothing derives them from that list
    /// (asistente-metricas-de-calidad): the replaced flag, written by its own UPDATE, and the audit
    /// table of reads of the quality problem list, written by its own INSERT.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnasEscritasAparte =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["registro_analitico"] = ["reemplazado"],
            ["auditoria_revision_de_calidad"] = ["id", "lector_id", "ocurrido_en", "filtros", "filas"],
        };

    /// <summary>
    /// Falla si a la base le falta alguna columna que el registro del turno escribe.
    /// </summary>
    private static async Task VerificarColumnasDelRegistroAsync(
        NpgsqlConnection conexion, CancellationToken ct)
    {
        var faltantes = new List<string>();

        foreach (var (tabla, columnas) in RegistroDelTurno.ColumnasQueEscribe.Concat(ColumnasEscritasAparte))
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
            + "su ALTER TABLE ... ADD COLUMN IF NOT EXISTS en un "
            + "database/asistente/NNN_*.sql registrado en MigradorAsistente.Recursos "
            + "(002 para el operativo, 010 para causa y modelo del analítico).");
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
