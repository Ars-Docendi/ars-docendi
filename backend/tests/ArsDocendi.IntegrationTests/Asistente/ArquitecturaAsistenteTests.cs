using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Guards de arquitectura propios del módulo del asistente.
/// </summary>
/// <remarks>
/// Los guards generales de <c>ArquitecturaIdentityTests</c> ya barren
/// <c>Modules.Asistente</c> junto con el resto. Acá van los que solo tienen sentido
/// para este módulo, porque es el único con una excepción declarada: consulta
/// schemas ajenos sin pasar por Contracts. Esa excepción es sostenible únicamente
/// si el resto de sus fronteras está verificado en vez de supuesto.
///
/// Cada guard viene en par: uno corre sobre el código real y el otro alimenta al
/// mismo detector con una violación sintética. Sin el segundo, un detector roto
/// —una regex que no matchea nada— pasaría en verde para siempre.
/// </remarks>
public sealed partial class ArquitecturaAsistenteTests
{
    /// <summary>
    /// Únicos archivos que pueden nombrar <c>CadenaDuena</c>: el migrador, que
    /// necesita ser dueño para conceder, y la composición, que la usa para derivar
    /// las dos de solo lectura.
    /// </summary>
    private static readonly string[] PuedenUsarLaCadenaDelDueno =
    [
        "MigradorAsistente.cs",
        "ModuleExtensions.cs",
    ];

    // ------------------------------------------------- la cadena del dueño no se filtra

    [Fact]
    public void Solo_el_migrador_y_la_composicion_usan_la_cadena_del_dueno()
    {
        var archivos = CodigoDelModulo();

        // Es el guard más importante del módulo. Todo el trabajo de privilegios por
        // columna se evapora si el motor de consulta recibe la conexión del dueño:
        // seguiría funcionando, leyendo de más y sin fallar.
        Assert.NotEmpty(archivos);
        var infracciones = Detectar(
            archivos.Where(a => !PuedenUsarLaCadenaDelDueno.Contains(Path.GetFileName(a.Ruta))),
            UsoDeCadenaDuena());

        Assert.True(infracciones.Count == 0,
            "Solo el migrador y la composición pueden nombrar CadenaDuena. Detectado en: "
            + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_detector_reconoce_un_uso_de_la_cadena_del_dueno()
    {
        var sintetico = new Archivo(
            "Application/MotorDeConsulta.cs",
            "public sealed class MotorDeConsulta(CadenaDuena cadena) { }");

        var infracciones = Detectar([sintetico], UsoDeCadenaDuena());

        Assert.Single(infracciones);
    }

    // --------------------------------------------------------- el módulo no escribe

    [Fact]
    public void El_codigo_del_modulo_no_contiene_sentencias_de_mutacion()
    {
        var archivos = CodigoDelModulo();

        Assert.NotEmpty(archivos);
        var infracciones = Detectar(archivos, MutacionEnCodigo());

        Assert.True(infracciones.Count == 0,
            "El asistente es de solo lectura: no puede escribir. Detectado en: "
            + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_detector_reconoce_una_sentencia_de_mutacion_en_codigo()
    {
        Archivo[] sinteticos =
        [
            new("Application/Guardar.cs", "var sql = \"INSERT INTO designaciones.pedidos VALUES (1)\";"),
            new("Application/Borrar.cs", "var sql = \"DELETE FROM identity.personas\";"),
            new("Application/Vaciar.cs", "var sql = \"TRUNCATE designaciones.pedidos\";"),
            new("Application/VaciarTabla.cs", "var sql = \"TRUNCATE TABLE identity.personas\";"),
            new("Application/Persistir.cs", "await db.SaveChangesAsync(ct);"),
        ];

        var infracciones = Detectar(sinteticos, MutacionEnCodigo());

        Assert.Equal(5, infracciones.Count);
    }

    [Fact]
    public void Una_lista_de_palabras_prohibidas_no_cuenta_como_mutacion()
    {
        // El validador de la SQL generada tiene que enumerar lo que rechaza. Una
        // enumeración de prohibiciones es lo contrario de una mutación, y el
        // detector no puede confundirlas: si lo hiciera, la salida sería quitar el
        // guard o quitar la enumeración, y las dos son peores.
        Archivo[] sinteticos =
        [
            new("Application/ValidadorFicticio.cs",
                """
                private static readonly HashSet<string> Prohibidas =
                    ["insert", "update", "delete", "truncate", "merge"];
                """),
        ];

        Assert.Empty(Detectar(sinteticos, MutacionEnCodigo()));
    }

    // ------------------------------------------------------------- el DDL no muta

    [Fact]
    public void El_DDL_del_asistente_solo_concede_privilegios()
    {
        var archivos = DdlDelAsistente();

        // El único .sql del módulo concede lectura. Si alguna vez incluyera un DDL
        // que crea o borra, dejaría de ser una migración de privilegios sin que el
        // nombre del archivo lo diga.
        Assert.NotEmpty(archivos);
        var infracciones = Detectar(archivos, MutacionEnSql());

        Assert.True(infracciones.Count == 0,
            "El DDL del asistente solo concede privilegios. Detectado en: "
            + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_detector_reconoce_un_DDL_que_muta()
    {
        Archivo[] sinteticos =
        [
            new("002_tabla.sql", "CREATE TABLE asistente.consultas (id uuid);"),
            new("003_baja.sql", "DROP TABLE designaciones.pedidos;"),
            new("004_semilla.sql", "INSERT INTO identity.roles (code) VALUES ('x');"),
        ];

        var infracciones = Detectar(sinteticos, MutacionEnSql());

        Assert.Equal(3, infracciones.Count);
    }

    // ------------------------------------------------------------- las referencias

    [Fact]
    public void El_modulo_solo_referencia_ArsDocendi_Shared()
    {
        var proyecto = Path.Combine(
            RaizRepositorio.BackendSrc(), "Modules.Asistente", "Modules.Asistente.csproj");
        Assert.True(File.Exists(proyecto), $"No se encontró {proyecto}.");

        // Los .csproj escriben las rutas con separador de Windows. Path.* no lo
        // parte en Linux, así que hay que normalizar antes de quedarse con el nombre.
        var referencias = XDocument.Load(proyecto)
            .Descendants("ProjectReference")
            .Select(nodo => (nodo.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/'))
            .Select(ruta => Path.GetFileNameWithoutExtension(ruta))
            .ToArray();

        // Los edges hacia Contracts ajenos llegan con el carril determinista de API.
        // Hasta entonces, cualquier referencia nueva es un error, no una decisión.
        Assert.Equal(["ArsDocendi.Shared"], referencias);
    }

    // ------------------------------------------------------------------------ apoyo

    private sealed record Archivo(string Ruta, string Contenido);

    private static IReadOnlyList<string> Detectar(IEnumerable<Archivo> archivos, Regex patron) =>
        archivos.Where(a => patron.IsMatch(a.Contenido)).Select(a => a.Ruta).ToList();

    private static Archivo[] CodigoDelModulo()
    {
        var raiz = RaizRepositorio.Ruta();
        var modulo = Path.Combine(RaizRepositorio.BackendSrc(), "Modules.Asistente");

        return [.. Directory.EnumerateFiles(modulo, "*.cs", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(ruta => new Archivo(
                Path.GetRelativePath(raiz, ruta),
                SinComentariosDeCodigo(File.ReadAllText(ruta))))];
    }

    private static Archivo[] DdlDelAsistente()
    {
        var raiz = RaizRepositorio.Ruta();
        var directorio = Path.Combine(raiz, "database", "asistente");

        return [.. Directory.EnumerateFiles(directorio, "*.sql", SearchOption.AllDirectories)
            .Select(ruta => new Archivo(
                Path.GetRelativePath(raiz, ruta),
                SinComentariosDeSql(File.ReadAllText(ruta))))];
    }

    private static string SinComentariosDeCodigo(string codigo) =>
        ComentariosDeCodigo().Replace(codigo, string.Empty);

    private static string SinComentariosDeSql(string sql) =>
        ComentariosDeSql().Replace(sql, string.Empty);

    [GeneratedRegex(@"//.*?$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex ComentariosDeCodigo();

    [GeneratedRegex(@"--.*?$", RegexOptions.Multiline)]
    private static partial Regex ComentariosDeSql();

    [GeneratedRegex(@"\bCadenaDuena\b")]
    private static partial Regex UsoDeCadenaDuena();

    // TRUNCATE exige un objetivo —igual que las otras tres formas de este patrón—
    // y no aparece suelto. El validador de la SQL generada tiene que ENUMERAR las
    // palabras prohibidas para poder rechazarlas, y una lista de prohibiciones es
    // lo contrario de una mutación. Exigir el objetivo conserva todos los
    // verdaderos positivos: una sentencia real siempre nombra qué trunca.
    [GeneratedRegex(
        @"\bINSERT\s+INTO\b|\bUPDATE\s+[\w"".]+\s+SET\b|\bDELETE\s+FROM\b|" +
        @"\bTRUNCATE\s+(?:TABLE\s+)?[\w"".]+|" +
        @"\bSaveChanges(?:Async)?\s*\(|\bExecuteUpdate\w*\s*\(|\bExecuteDelete\w*\s*\(",
        RegexOptions.IgnoreCase)]
    private static partial Regex MutacionEnCodigo();

    [GeneratedRegex(
        @"\bINSERT\s+INTO\b|\bUPDATE\s+[\w"".]+\s+SET\b|\bDELETE\s+FROM\b|" +
        @"\bTRUNCATE\s+(?:TABLE\s+)?[\w"".]+|" +
        @"\bDROP\s+\w+\b|\bALTER\s+TABLE\b|\bCREATE\s+TABLE\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex MutacionEnSql();
}
