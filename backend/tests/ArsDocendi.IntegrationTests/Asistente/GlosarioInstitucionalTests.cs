using System.Text.Json;
using System.Text.RegularExpressions;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Todo lo que el glosario nombra existe, es legible y no es sensible
/// (asistente-glosario-institucional, D4).
/// </summary>
/// <remarks>
/// <b>LO QUE ESTE TEST EXISTE PARA EVITAR.</b> Una línea del glosario viaja al
/// prompt como un hecho: «aprobado → estado = 'en_lote'». Si la columna se renombra
/// o el valor deja de existir, el modelo sigue copiando el literal y la consulta
/// vuelve vacía con SQL válido, que se lee como «no hay datos». Es el mismo defecto
/// que el catálogo de ejemplos verificados.
///
/// <b>Cada regla se prueba contra una entrada rota que vive ACÁ</b> y no en el
/// archivo real: un verificador que dejara de verificar —una consulta que devuelve
/// siempre «existe»— haría pasar al archivo real sin que nada lo notara. Es el
/// mismo par que <c>PrefijoDeLosCassettesTests.El_guard_reconoce_una_huella_ajena</c>.
///
/// Corre contra la base migrada y sin seed: los valores que se comprueban salen de
/// <c>CHECK</c> o de catálogos que siembra una migración.
/// </remarks>
public sealed partial class GlosarioInstitucionalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_glosario")
{
    private const string Estado = "designaciones.pedidos.estado";
    private const string Propietario = "designaciones.pedidos.propietario_actual";

    /// <summary>
    /// Las tablas cuyos valores se comprueban contra sus filas.
    /// </summary>
    /// <remarks>
    /// DECLARADAS, no detectadas, por el mismo motivo que la lista de catálogos
    /// cerrados: que una tabla sirva de fuente de verdad es una decisión que se
    /// revisa en el diff. Las tres son catálogos sembrados por migración.
    /// </remarks>
    private static readonly string[] TablasDeCatalogo =
        ["designaciones.cargos", "designaciones.dedicaciones", "identity.roles"];

    // ------------------------------------------------------- el archivo real

    [Fact]
    public async Task El_archivo_real_cumple_todas_las_reglas()
    {
        var resultado = await VerificarAsync(CatalogoDeGlosario.Vigente.Terminos);

        Assert.True(
            resultado.Violaciones.Count == 0,
            "El glosario nombra algo que no existe, que el rol básico no puede leer o que es "
            + "sensible:" + Environment.NewLine + string.Join(Environment.NewLine, resultado.Violaciones));
    }

    [Fact]
    public async Task La_verificacion_no_es_vacia()
    {
        // Anti-vacuidad: si el recorrido no visitara nada, el test de arriba pasaría
        // sin comparar una sola columna.
        var resultado = await VerificarAsync(CatalogoDeGlosario.Vigente.Terminos);

        Assert.True(resultado.Columnas >= 20, $"Sólo se verificaron {resultado.Columnas} columnas.");
        Assert.True(resultado.Valores >= 30, $"Sólo se verificaron {resultado.Valores} valores.");
    }

    [Fact]
    public async Task Los_valores_de_propietario_actual_y_de_cargo_se_encuentran_en_sus_catalogos()
    {
        // `propietario_actual` no tiene CHECK: sus valores son códigos de
        // `identity.roles`. Los cargos son filas de `designaciones.cargos`.
        var terminos = CatalogoDeGlosario.Vigente.Terminos;

        var bandeja = terminos.Single(t => t.Termino == "en qué bandeja está").Referencias.Single();
        Assert.Equal(Propietario, bandeja.Columna);
        Assert.Equal("identity.roles.code", bandeja.VerificaContra);

        var titular = terminos.Single(t => t.Termino == "titular").Referencias.Single();
        Assert.Equal("designaciones.cargos.codigo", titular.Columna);

        var resultado = await VerificarAsync([.. terminos.Where(t => t.Termino is "en qué bandeja está" or "titular")]);
        Assert.Empty(resultado.Violaciones);
    }

    // ------------------------------- columnas: existen, se leen, no son sensibles

    [Fact]
    public async Task Una_columna_que_no_existe_falla_nombrando_el_termino_y_la_columna()
    {
        var roto = Termino("inventado", columna: "designaciones.pedidos.estado_inexistente");

        await AssertFallaAsync(roto, "inventado", "designaciones.pedidos.estado_inexistente", "no existe");
    }

    [Fact]
    public async Task Un_schema_ausente_del_manifiesto_de_privilegios_falla()
    {
        // `tareas` no está en el manifiesto del asistente: aunque la tabla existiera,
        // el rol básico no puede leerla.
        var roto = Termino("de tareas", columna: "tareas.tareas.estado");

        await AssertFallaAsync(roto, "de tareas", "tareas.tareas.estado", "manifiesto de privilegios");
    }

    [Fact]
    public async Task Una_columna_concedida_solo_al_rol_con_datos_personales_falla()
    {
        // Se arma un manifiesto sintético en vez de apuntar a una columna real:
        // hoy ninguna columna de `designaciones` es exclusiva del rol PII, y un test
        // que dependiera de que alguna lo sea se rompería con una migración ajena.
        var manifiesto = Manifiesto.Cargar() with
        {
            Tablas =
            [
                .. Manifiesto.Cargar().Tablas.Where(t => t.Cualificado != "designaciones.pedidos"),
                new TablaManifiesto
                {
                    Schema = "designaciones",
                    Tabla = "pedidos",
                    Estado = "concedida",
                    ColumnasConcedidas = new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["asistente_ro"] = ["id"],
                        ["asistente_ro_pii"] = ["id", "estado"],
                    },
                },
            ],
        };

        var roto = Termino("solo pii", columna: Estado, valores: ["devuelto"]);

        await AssertFallaAsync(roto, "solo pii", Estado, "rol básico", manifiesto);
    }

    [Fact]
    public async Task Una_columna_sensible_texto_falla()
    {
        var roto = Termino("justificado", columna: "designaciones.pedidos.justificacion");

        await AssertFallaAsync(roto, "justificado", "designaciones.pedidos.justificacion", "sensible-texto");
    }

    [Fact]
    public async Task La_columna_de_verificaContra_tambien_se_verifica()
    {
        // Una fuente de verdad ilegible para el rol básico no puede serlo del
        // glosario: el valor se verificaría contra algo que el asistente no ve.
        var roto = Termino("fuente ilegible", columna: Propietario, valores: ["decanato"], verificaContra: "tareas.estados_tarea.codigo");

        await AssertFallaAsync(roto, "fuente ilegible", "tareas.estados_tarea.codigo", "no existe");
    }

    // ------------------------------------------------------------- valores

    [Fact]
    public async Task Aprobado_sobre_el_estado_falla_porque_no_esta_en_el_CHECK()
    {
        var roto = Termino("aprobado de mentira", columna: Estado, valores: ["aprobado"]);

        await AssertFallaAsync(roto, "aprobado de mentira", Estado, "CHECK", valor: "aprobado");
    }

    [Fact]
    public async Task Un_valor_en_una_columna_sin_CHECK_ni_catalogo_ni_fuente_falla()
    {
        // `pedidos.numero` es texto libre: ningún medio mecánico lo verifica.
        var roto = Termino("numerado", columna: "designaciones.pedidos.numero", valores: ["P-1"]);

        await AssertFallaAsync(roto, "numerado", "designaciones.pedidos.numero", "no hay forma de verificarlo");
    }

    [Fact]
    public async Task Una_fuente_que_no_es_una_columna_de_catalogo_falla()
    {
        // `pedidos.numero` existe y se lee, pero no es un catálogo: que sus valores
        // «verifiquen» un valor del glosario no verifica nada.
        var roto = Termino(
            "fuente de pedidos", columna: Propietario, valores: ["decanato"],
            verificaContra: "designaciones.pedidos.numero");

        await AssertFallaAsync(roto, "fuente de pedidos", "designaciones.pedidos.numero", "catálogo");
    }

    [Fact]
    public async Task Un_valor_que_no_esta_en_la_fuente_declarada_falla()
    {
        var roto = Termino(
            "bandeja de nadie", columna: Propietario, valores: ["jefe_inexistente"],
            verificaContra: "identity.roles.code");

        await AssertFallaAsync(roto, "bandeja de nadie", Propietario, "identity.roles.code", valor: "jefe_inexistente");
    }

    [Fact]
    public async Task Un_valor_que_no_es_una_fila_del_catalogo_falla()
    {
        var roto = Termino("cargo fantasma", columna: "designaciones.cargos.codigo", valores: ["decano"]);

        await AssertFallaAsync(roto, "cargo fantasma", "designaciones.cargos.codigo", "designaciones.cargos", valor: "decano");
    }

    [Fact]
    public async Task Declarar_la_fuente_en_una_columna_con_CHECK_falla()
    {
        // Con CHECK el valor se verifica contra el CHECK. Una fuente de más sería una
        // segunda verdad que puede discrepar de la primera.
        var roto = Termino(
            "doble verdad", columna: Estado, valores: ["devuelto"], verificaContra: "identity.roles.code");

        await AssertFallaAsync(roto, "doble verdad", Estado, "verificaContra");
    }

    // ------------------------------------------------------- la explicación

    [Fact]
    public async Task Un_identificador_entre_acentos_graves_que_no_existe_falla()
    {
        var roto = Termino("explicado", columna: Estado, valores: ["devuelto"], explicacion: "Usa `columna_inventada` del pedido.");

        await AssertFallaAsync(roto, "explicado", "columna_inventada", "no existe");
    }

    [Fact]
    public async Task Un_identificador_entre_acentos_graves_que_existe_pasa()
    {
        var bueno = Termino("explicado", columna: Estado, valores: ["devuelto"], explicacion: "Mira `estado` y `pedidos`.");

        var resultado = await VerificarAsync([bueno]);

        Assert.Empty(resultado.Violaciones);
    }

    [Fact]
    public async Task Una_columna_cualificada_fuera_de_las_referencias_falla()
    {
        var roto = Termino(
            "cualificado", columna: Estado, valores: ["devuelto"],
            explicacion: "Mira designaciones.pedidos.numero para identificarlo.");

        await AssertFallaAsync(roto, "cualificado", "designaciones.pedidos.numero", "fuera de las referencias");
    }

    // ----------------------------------------------------------------- apoyo

    private async Task AssertFallaAsync(
        TerminoDeGlosario roto, string termino, string columna, string motivo,
        Manifiesto? manifiesto = null, string? valor = null)
    {
        var resultado = await VerificarAsync([roto], manifiesto);

        Assert.True(resultado.Violaciones.Count > 0, $"La entrada rota «{termino}» pasó sin ninguna violación.");

        // Una violación que nombra al término Y a la columna Y el motivo: si sólo
        // dijera «algo falló» no diría qué entrada arreglar.
        Assert.Contains(
            resultado.Violaciones,
            v => v.Contains(termino, StringComparison.Ordinal)
                 && v.Contains(columna, StringComparison.Ordinal)
                 && v.Contains(motivo, StringComparison.Ordinal)
                 && (valor is null || v.Contains(valor, StringComparison.Ordinal)));
    }

    private static TerminoDeGlosario Termino(
        string termino,
        string columna,
        string[]? valores = null,
        string? verificaContra = null,
        string explicacion = "Explicación de prueba.")
    {
        var json = JsonSerializer.Serialize(new
        {
            terminos = new[]
            {
                new
                {
                    termino,
                    explicacion,
                    referencias = new[] { new { columna, valores = valores ?? [], verificaContra } },
                },
            },
        });

        return CatalogoDeGlosario.Interpretar(json).Terminos[0];
    }

    private sealed record Resultado(IReadOnlyList<string> Violaciones, int Columnas, int Valores);

    /// <summary>Aplica las reglas 2 a 5 de D4 y las de la explicación a cada término.</summary>
    private async Task<Resultado> VerificarAsync(
        IReadOnlyList<TerminoDeGlosario> terminos, Manifiesto? manifiesto = null)
    {
        manifiesto ??= Manifiesto.Cargar();
        var sensibilidad = ManifiestoDeSensibilidad.Cargar();
        var violaciones = new List<string>();
        var columnasVerificadas = 0;
        var valoresVerificados = 0;

        await using var conexion = await AbrirConexionAsync();

        foreach (var termino in terminos)
        {
            foreach (var referencia in termino.Referencias)
            {
                var existe = await VerificarColumnaAsync(
                    conexion, manifiesto, sensibilidad, termino.Termino, referencia.Columna, violaciones);
                columnasVerificadas++;

                if (referencia.VerificaContra is not null)
                {
                    await VerificarColumnaAsync(
                        conexion, manifiesto, sensibilidad, termino.Termino, referencia.VerificaContra, violaciones);
                    columnasVerificadas++;
                }

                if (existe)
                {
                    valoresVerificados += await VerificarValoresAsync(conexion, termino, referencia, violaciones);
                }
            }

            await VerificarExplicacionAsync(conexion, manifiesto, termino, violaciones);
        }

        return new Resultado(violaciones, columnasVerificadas, valoresVerificados);
    }

    /// <summary>Reglas 2, 3 y 4: existe, el rol básico la lee y es pública.</summary>
    /// <returns>Si la columna existe y es legible, o sea, si tiene sentido mirar sus valores.</returns>
    private static async Task<bool> VerificarColumnaAsync(
        NpgsqlConnection conexion,
        Manifiesto manifiesto,
        ManifiestoDeSensibilidad sensibilidad,
        string termino,
        string columna,
        List<string> violaciones)
    {
        var partes = columna.Split('.');
        var (esquema, tabla, nombre) = (partes[0], partes[1], partes[2]);

        var existe = await ExisteLaColumnaAsync(conexion, esquema, tabla, nombre);
        if (!existe)
        {
            violaciones.Add($"«{termino}»: la columna {columna} no existe en la base.");
        }

        var concedida = manifiesto.Tablas.Any(t =>
            t.Schema == esquema && t.Tabla == tabla && t.EsConcedida
            && t.ColumnasConcedidas.TryGetValue("asistente_ro", out var columnas)
            && columnas.Contains(nombre));

        if (!concedida)
        {
            violaciones.Add(
                $"«{termino}»: la columna {columna} no está concedida al rol básico en el manifiesto de privilegios.");
        }

        var clasificacion = sensibilidad.Clasificacion(esquema, tabla, nombre);
        if (clasificacion != ClasificacionDeSensibilidad.Publica)
        {
            var etiqueta = clasificacion switch
            {
                ClasificacionDeSensibilidad.SensibleTexto => "sensible-texto",
                ClasificacionDeSensibilidad.SensibleValor => "sensible-valor",
                _ => "sin clasificar",
            };

            violaciones.Add($"«{termino}»: la columna {columna} es {etiqueta} y no es pública.");
        }

        return existe && concedida;
    }

    /// <summary>Regla 5: cada valor se verifica por el primer medio que aplica.</summary>
    private static async Task<int> VerificarValoresAsync(
        NpgsqlConnection conexion, TerminoDeGlosario termino, ReferenciaDeGlosario referencia, List<string> violaciones)
    {
        if (referencia.Valores.Count == 0)
        {
            return 0;
        }

        var literales = await LiteralesDelCheckAsync(conexion, referencia);

        if (literales.Count > 0)
        {
            if (referencia.VerificaContra is not null)
            {
                violaciones.Add(
                    $"«{termino.Termino}»: {referencia.Columna} tiene CHECK y declara verificaContra; "
                    + "con CHECK el valor se verifica contra el CHECK.");
            }

            foreach (var valor in referencia.Valores.Where(v => !literales.Contains(v)))
            {
                violaciones.Add(
                    $"«{termino.Termino}»: el valor '{valor}' no está en el CHECK de {referencia.Columna}.");
            }

            return referencia.Valores.Count;
        }

        var fuente = referencia.VerificaContra ?? referencia.Columna;
        var partesDeLaFuente = fuente.Split('.');
        var tablaDeLaFuente = $"{partesDeLaFuente[0]}.{partesDeLaFuente[1]}";

        if (!TablasDeCatalogo.Contains(tablaDeLaFuente))
        {
            violaciones.Add(
                referencia.VerificaContra is not null
                    ? $"«{termino.Termino}»: {referencia.Columna} se verifica contra {fuente}, que no es una columna de catálogo declarada."
                    : $"«{termino.Termino}»: {referencia.Columna} no tiene CHECK, ni es un catálogo, ni declara "
                      + "verificaContra: no hay forma de verificarlo.");
            return 0;
        }

        // Los identificadores salen de `TablasDeCatalogo` y de una referencia ya
        // validada con la forma esquema.tabla.columna, nunca de una pregunta.
        foreach (var valor in referencia.Valores)
        {
            await using var comando = new NpgsqlCommand(
                $"SELECT EXISTS (SELECT 1 FROM {partesDeLaFuente[0]}.{partesDeLaFuente[1]} "
                + $"WHERE {partesDeLaFuente[2]}::text = @valor)", conexion);
            comando.Parameters.AddWithValue("valor", valor);

            if (await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken) is not true)
            {
                violaciones.Add(
                    $"«{termino.Termino}»: el valor '{valor}' de {referencia.Columna} no es un valor de {fuente}.");
            }
        }

        return referencia.Valores.Count;
    }

    /// <summary>Reglas de la explicación: lo que dice del dato también existe.</summary>
    private static async Task VerificarExplicacionAsync(
        NpgsqlConnection conexion, Manifiesto manifiesto, TerminoDeGlosario termino, List<string> violaciones)
    {
        var referenciadas = termino.Referencias.Select(r => r.Columna).ToHashSet(StringComparer.Ordinal);

        foreach (Match cualificada in ColumnaCualificada().Matches(termino.Explicacion))
        {
            if (!referenciadas.Contains(cualificada.Value))
            {
                violaciones.Add(
                    $"«{termino.Termino}»: la explicación nombra {cualificada.Value}, que está fuera de las referencias.");
            }
        }

        foreach (Match identificador in EntreAcentosGraves().Matches(termino.Explicacion))
        {
            var nombre = identificador.Groups[1].Value;

            var legible = manifiesto.Tablas.Any(t =>
                t.EsConcedida
                && (t.Tabla == nombre
                    || (t.ColumnasConcedidas.TryGetValue("asistente_ro", out var columnas) && columnas.Contains(nombre))));

            await using var comando = new NpgsqlCommand(
                """
                SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE column_name = @nombre)
                    OR EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @nombre)
                """, conexion);
            comando.Parameters.AddWithValue("nombre", nombre);
            var existe = await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken) is true;

            if (!existe || !legible)
            {
                violaciones.Add(
                    $"«{termino.Termino}»: la explicación nombra `{nombre}`, que no existe como columna o tabla "
                    + "que el rol básico pueda leer.");
            }
        }
    }

    private static async Task<bool> ExisteLaColumnaAsync(
        NpgsqlConnection conexion, string esquema, string tabla, string columna)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                 WHERE table_schema = @esquema AND table_name = @tabla AND column_name = @columna)
            """, conexion);
        comando.Parameters.AddWithValue("esquema", esquema);
        comando.Parameters.AddWithValue("tabla", tabla);
        comando.Parameters.AddWithValue("columna", columna);

        return await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken) is true;
    }

    /// <summary>
    /// Los literales de texto de los <c>CHECK</c> que mencionan la columna.
    /// </summary>
    /// <remarks>
    /// PostgreSQL devuelve <c>IN (…)</c> como <c>ARRAY['a'::text, …]</c>, así que se
    /// extraen los literales entre comillas y se des-escapa <c>''</c>; no se intenta
    /// evaluar la restricción.
    /// </remarks>
    private static async Task<HashSet<string>> LiteralesDelCheckAsync(
        NpgsqlConnection conexion, ReferenciaDeGlosario referencia)
    {
        await using var comando = new NpgsqlCommand(
            """
            SELECT pg_catalog.pg_get_constraintdef(c.oid)
              FROM pg_catalog.pg_constraint c
              JOIN pg_catalog.pg_class t ON t.oid = c.conrelid
              JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
              JOIN pg_catalog.pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (c.conkey)
             WHERE c.contype = 'c'
               AND n.nspname = @esquema AND t.relname = @tabla AND a.attname = @columna
            """, conexion);
        comando.Parameters.AddWithValue("esquema", referencia.Esquema);
        comando.Parameters.AddWithValue("tabla", referencia.Tabla);
        comando.Parameters.AddWithValue("columna", referencia.NombreDeColumna);

        var literales = new HashSet<string>(StringComparer.Ordinal);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            foreach (Match literal in LiteralDeSql().Matches(lector.GetString(0)))
            {
                literales.Add(literal.Groups[1].Value.Replace("''", "'", StringComparison.Ordinal));
            }
        }

        return literales;
    }

    [GeneratedRegex(@"\b[a-z_]+\.[a-z_]+\.[a-z_]+\b")]
    private static partial Regex ColumnaCualificada();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex EntreAcentosGraves();

    [GeneratedRegex(@"'((?:[^']|'')*)'")]
    private static partial Regex LiteralDeSql();
}
