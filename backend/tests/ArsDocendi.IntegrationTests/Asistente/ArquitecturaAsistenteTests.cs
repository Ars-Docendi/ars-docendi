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
    /// Únicos archivos que pueden nombrar <c>CadenaDuena</c>, y por qué cada uno.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>El migrador: conceder privilegios exige ser dueño de la tabla.</item>
    /// <item>La composición: deriva de ella las dos cadenas de solo lectura.</item>
    /// <item>El escritor de los registros y su purga: los dos registros los escribe
    /// la aplicación, y los roles del asistente tienen su schema revocado entero
    /// (definición §3.4).</item>
    /// </list>
    ///
    /// La lista es corta a propósito y crece solo con un motivo escrito. Lo que
    /// sigue afuera es lo que importa: <c>EjecutorDeConsulta</c>,
    /// <c>ProveedorDeEsquema</c>, <c>ConsultorDeAlcance</c> y todo lo que toque la
    /// consulta generada.
    /// </remarks>
    private static readonly string[] PuedenUsarLaCadenaDelDueno =
    [
        "MigradorAsistente.cs",
        "ModuleExtensions.cs",
        "RegistroDelTurno.cs",
        "PurgaDeRegistros.cs",
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
    public void El_codigo_del_modulo_no_muta_datos_de_otro_schema()
    {
        var archivos = CodigoDelModulo();

        Assert.NotEmpty(archivos);
        var infracciones = DetectarMutacionAjena(archivos, MutacionEnCodigo());

        Assert.True(infracciones.Count == 0,
            "El asistente es de solo lectura sobre los datos del sistema. Lo único que "
            + "escribe es su propio schema `asistente`. Detectado en: "
            + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_detector_reconoce_una_sentencia_de_mutacion_ajena_en_codigo()
    {
        Archivo[] sinteticos =
        [
            new("Application/Guardar.cs", "var sql = \"INSERT INTO designaciones.pedidos VALUES (1)\";"),
            new("Application/Borrar.cs", "var sql = \"DELETE FROM identity.personas\";"),
            new("Application/Vaciar.cs", "var sql = \"TRUNCATE designaciones.pedidos\";"),
            new("Application/VaciarTabla.cs", "var sql = \"TRUNCATE TABLE identity.personas\";"),
            new("Application/Persistir.cs", "await db.SaveChangesAsync(ct);"),
        ];

        var infracciones = DetectarMutacionAjena(sinteticos, MutacionEnCodigo());

        Assert.Equal(5, infracciones.Count);
    }

    [Fact]
    public void Escribir_el_schema_propio_del_asistente_no_es_una_infraccion()
    {
        // LA LÍNEA EXACTA DEL GUARD, y conviene que esté escrita como test y no como
        // comentario. Los dos registros del propio asistente son telemetría suya, no
        // datos del sistema: la definición pide explícitamente que los escriba la
        // conexión dueña (§3.4). Lo que el invariante prohíbe es tocar los datos de
        // los módulos, y eso sigue prohibido en la línea de abajo.
        Archivo[] sinteticos =
        [
            new("Infrastructure/Propio.cs",
                "var sql = \"INSERT INTO asistente.registro_operativo (actor_id) VALUES (@a)\";"),
            new("Infrastructure/Purga.cs",
                "var sql = \"DELETE FROM asistente.registro_analitico WHERE dia < @corte\";"),
        ];

        Assert.Empty(DetectarMutacionAjena(sinteticos, MutacionEnCodigo()));

        Assert.Single(DetectarMutacionAjena(
            [new("Infrastructure/Ajeno.cs",
                "var sql = \"INSERT INTO designaciones.pedidos (id) VALUES (@a)\";")],
            MutacionEnCodigo()));
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
    public void El_DDL_del_asistente_no_toca_ningun_schema_ajeno()
    {
        var archivos = DdlDelAsistente();

        // El DDL del módulo concede lectura sobre los schemas de los otros y crea
        // el suyo propio. Lo que no puede hacer, en ninguna forma, es crear, alterar,
        // borrar o sembrar algo de identity, designaciones o audit: ahí su papel es
        // pedir permiso, no modificar.
        Assert.NotEmpty(archivos);
        var infracciones = DetectarMutacionAjena(archivos, MutacionEnSql());

        Assert.True(infracciones.Count == 0,
            "El DDL del asistente solo concede privilegios y crea su propio schema. "
            + "Detectado en: " + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_DDL_del_asistente_no_borra_ni_altera_nada_ni_siquiera_lo_propio()
    {
        // DROP y ALTER no tienen excepción de schema. Una migración que altera lo
        // que otra creó deja el esquema dependiendo del orden de aplicación, y este
        // módulo no lleva historial de migraciones: sus scripts son idempotentes por
        // construcción.
        var archivos = DdlDelAsistente();

        Assert.NotEmpty(archivos);
        Assert.Empty(Detectar(archivos, DestruccionEnSql()));
    }

    [Fact]
    public void El_detector_reconoce_un_DDL_que_toca_lo_ajeno()
    {
        Archivo[] sinteticos =
        [
            new("003_tabla_ajena.sql", "CREATE TABLE designaciones.consultas (id uuid);"),
            new("004_semilla.sql", "INSERT INTO identity.roles (code) VALUES ('x');"),
            new("005_indice.sql", "CREATE INDEX ix_x ON identity.personas (legajo);"),
        ];

        Assert.Equal(3, DetectarMutacionAjena(sinteticos, MutacionEnSql()).Count);

        // Y el propio, no.
        Assert.Empty(DetectarMutacionAjena(
            [new("006_propio.sql", "CREATE TABLE asistente.consultas (id uuid);")],
            MutacionEnSql()));
    }

    [Fact]
    public void El_detector_reconoce_un_DDL_destructivo()
    {
        Archivo[] sinteticos =
        [
            new("007_baja.sql", "DROP TABLE designaciones.pedidos;"),
            new("008_baja_propia.sql", "DROP TABLE asistente.registro_operativo;"),
            new("009_alter.sql", "ALTER TABLE asistente.registro_analitico ADD COLUMN x text;"),
        ];

        Assert.Equal(3, Detectar(sinteticos, DestruccionEnSql()).Count);
    }

    // --------------------------------------------- el ping no arrastra dependencias

    [Fact]
    public void El_controller_del_ping_no_tiene_ninguna_dependencia()
    {
        // ESTO YA SE ROMPIÓ UNA VEZ. El ping vivía junto al endpoint del turno; el
        // día que ese controller ganó dependencias, construirlo pasó a exigir las
        // cadenas de solo lectura —cuya fábrica falla si el ambiente no las
        // configuró— y el ping devolvió 500 sin base.
        //
        // Un ping que necesita configuración de base deja de poder distinguir «el
        // módulo está cargado» de «la base responde», que es lo único que el
        // invariante #3 le pide. La separación tiene que ser estructural: sin
        // constructor con parámetros, no hay nada que resolver.
        var tipo = typeof(Modules.Asistente.Api.PingAsistenteController);

        Assert.All(
            tipo.GetConstructors(),
            constructor => Assert.Empty(constructor.GetParameters()));
    }

    [Fact]
    public void El_ping_no_esta_en_el_controller_del_turno()
    {
        var tipo = typeof(Modules.Asistente.Api.AsistenteController);

        Assert.DoesNotContain(
            tipo.GetMethods().Select(m => m.Name),
            nombre => nombre.Equals("Ping", StringComparison.Ordinal));
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

    [Fact]
    public void El_SDK_del_proveedor_se_nombra_en_un_solo_archivo()
    {
        var culpables = Detectar(CodigoDelModulo(), SdkDelProveedor())
            .Where(ruta => !ruta.EndsWith("ProveedorAnthropic.cs", StringComparison.Ordinal))
            .ToList();

        // Es lo que hace cierta la promesa de que el puerto es agnóstico. Mientras
        // el SDK viva en un archivo, cambiar de proveedor —o sumar un segundo, o
        // pasarse a un modelo propio— es escribir otra clase al lado y otro brazo
        // del switch. En cuanto sus tipos se filtran a la composición o al
        // pipeline, esa promesa deja de ser verificable y pasa a ser una intención.
        Assert.Empty(culpables);
    }

    // ------------------------------------------------------------------------ apoyo

    private sealed record Archivo(string Ruta, string Contenido);

    private static IReadOnlyList<string> Detectar(IEnumerable<Archivo> archivos, Regex patron) =>
        archivos.Where(a => patron.IsMatch(a.Contenido)).Select(a => a.Ruta).ToList();

    /// <summary>
    /// Igual que <see cref="Detectar"/>, pero perdona lo que apunta al schema propio.
    /// </summary>
    /// <remarks>
    /// La excepción es angosta y explícita: solo <c>asistente</c> y solo cuando la
    /// sentencia nombra su objetivo. Una mutación sin objetivo reconocible
    /// —<c>SaveChangesAsync</c>, un <c>DROP</c>— no puede acogerse a ella, porque el
    /// detector no tiene con qué comprobar a quién le pega.
    /// </remarks>
    private static IReadOnlyList<string> DetectarMutacionAjena(
        IEnumerable<Archivo> archivos, Regex patron) =>
        archivos
            .Where(a => patron.Matches(a.Contenido).Any(m => !EsDelSchemaPropio(m)))
            .Select(a => a.Ruta)
            .ToList();

    private static bool EsDelSchemaPropio(Match coincidencia)
    {
        var objetivo = coincidencia.Groups["objetivo"];

        if (!objetivo.Success)
        {
            return false;
        }

        var nombre = objetivo.Value.Replace("\"", string.Empty, StringComparison.Ordinal);

        return nombre.Equals("asistente", StringComparison.OrdinalIgnoreCase)
            || nombre.StartsWith("asistente.", StringComparison.OrdinalIgnoreCase);
    }

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
        @"\bINSERT\s+INTO\s+(?<objetivo>[\w"".]+)|\bUPDATE\s+(?<objetivo>[\w"".]+)\s+SET\b|" +
        @"\bDELETE\s+FROM\s+(?<objetivo>[\w"".]+)|\bTRUNCATE\s+(?:TABLE\s+)?(?<objetivo>[\w"".]+)|" +
        @"\bSaveChanges(?:Async)?\s*\(|\bExecuteUpdate\w*\s*\(|\bExecuteDelete\w*\s*\(",
        RegexOptions.IgnoreCase)]
    private static partial Regex MutacionEnCodigo();

    [GeneratedRegex(
        @"\bINSERT\s+INTO\s+(?<objetivo>[\w"".]+)|\bUPDATE\s+(?<objetivo>[\w"".]+)\s+SET\b|" +
        @"\bDELETE\s+FROM\s+(?<objetivo>[\w"".]+)|\bTRUNCATE\s+(?:TABLE\s+)?(?<objetivo>[\w"".]+)|" +
        @"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<objetivo>[\w"".]+)|" +
        @"\bCREATE\s+SCHEMA\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<objetivo>[\w""]+)|" +
        @"\bCREATE\s+(?:UNIQUE\s+)?INDEX\s+(?:CONCURRENTLY\s+)?(?:IF\s+NOT\s+EXISTS\s+)?" +
        @"[\w"".]+\s+ON\s+(?<objetivo>[\w"".]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex MutacionEnSql();

    // DROP y ALTER van aparte y sin excepción de schema: no hay caso en que este
    // módulo tenga que borrar o alterar algo, ni siquiera lo propio.
    [GeneratedRegex(@"\bDROP\s+\w+\b|\bALTER\s+TABLE\b", RegexOptions.IgnoreCase)]
    private static partial Regex DestruccionEnSql();

    // El namespace raíz del SDK y sus tipos propios. Alcanza con el namespace: no
    // hay forma de usar el SDK sin nombrarlo, porque el módulo no tiene ningún
    // using global que lo traiga.
    [GeneratedRegex(@"\bAnthropic\.[A-Z]|\bAnthropicClient\b")]
    private static partial Regex SdkDelProveedor();
}
