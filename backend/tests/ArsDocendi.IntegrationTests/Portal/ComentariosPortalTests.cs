using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

/// <summary>
/// Los comentarios de esquema de portal, que son lo que el modelo lee.
/// </summary>
/// <remarks>
/// <b>Se verifican acá y no en <c>PrefijoDeEsquemaTests</c></b> porque aquéllos
/// recorren las tablas que el manifiesto concede, y portal todavía no se concede: el
/// <c>GRANT</c> es una decisión aparte con su propio gate. Sin estos tests, los
/// comentarios quedarían sin verificar hasta que alguien abra el schema, que es
/// justo cuando ya no hay tiempo de escribirlos.
///
/// El día que portal entre al manifiesto, los de allá empiezan a cubrirlo también y
/// esta clase pasa a ser la que verifica lo que aquéllos no miran: el contenido.
/// </remarks>
public sealed class ComentariosPortalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "portal_comentarios")
{
    /// <summary>Las seis que el catálogo de preguntas necesita.</summary>
    private static readonly string[] Expuestas =
    [
        "perfiles", "educaciones", "certificaciones",
        "experiencias", "docente_habilidades", "habilidades",
    ];

    /// <summary>Las que quedan fuera del alcance del asistente.</summary>
    private static readonly string[] FueraDeAlcance =
        ["contactos", "cvs", "proyectos", "proyecto_documentos"];

    [Fact]
    public async Task Las_seis_tablas_expuestas_tienen_comentario()
    {
        var comentadas = await ComentariosDeTablaAsync();

        var sinComentario = Expuestas
            .Where(tabla => !comentadas.TryGetValue(tabla, out var texto)
                            || string.IsNullOrWhiteSpace(texto))
            .ToArray();

        Assert.Empty(sinComentario);
    }

    [Fact]
    public async Task Ninguna_tabla_fuera_de_alcance_esta_comentada()
    {
        // Describirle al modelo algo que no puede leer sólo sirve para que lo pida y
        // choque con permission denied en vez de abstenerse.
        var comentadas = await ComentariosDeTablaAsync();

        Assert.All(FueraDeAlcance, tabla => Assert.DoesNotContain(tabla, comentadas.Keys));
    }

    [Fact]
    public async Task Toda_columna_de_las_tablas_expuestas_tiene_comentario()
    {
        // LA LISTA VA CALIFICADA Y CON MOTIVO. Excluir por nombre de columna suelto
        // —«created_at»— habría dejado sin verificar los seis `created_at` de las
        // otras tablas, que sí se comentan.
        string[] noSeConceden =
        [
            // Contador AGREGADO sobre todo el padrón: una policy por fila no puede
            // acotarlo, así que es el único canal de habilidades que la RLS no cierra.
            "habilidades.usos",

            // Maquinaria de curaduría del vocabulario, no dominio.
            "habilidades.sugerido",
            "habilidades.canonica_id",

            // Texto libre autodeclarado. El enmascarador identifica la columna por
            // (OID, attnum) y una expresión reporta OID 0, que se trata como pública:
            // clasificarla como sensible no alcanza, así que no se concede.
            "experiencias.descripcion",
        ];

        var comentadas = await ComentariosDeColumnaAsync();

        var sinComentario = (await ColumnasRealesAsync())
            .Where(columna => !noSeConceden.Contains(columna, StringComparer.Ordinal))
            .Where(columna => !comentadas.TryGetValue(columna, out var texto)
                              || string.IsNullOrWhiteSpace(texto))
            .OrderBy(columna => columna, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(sinComentario);

        // Y la simétrica: lo que NO se concede tampoco se comenta. Describirle al
        // modelo una columna que no puede leer sólo sirve para que la pida.
        Assert.All(
            noSeConceden, columna => Assert.DoesNotContain(columna, comentadas.Keys));
    }

    [Fact]
    public async Task El_comentario_del_perfil_declara_el_camino_hacia_el_padron()
    {
        // `portal.perfiles.persona_id` es el ÚNICO camino de portal hacia un nombre.
        // La FK ya se lo dice al lector de catálogo, pero el comentario lo dice en
        // palabras, que es lo que el modelo lee para decidir el join.
        var columnas = await ComentariosDeColumnaAsync();

        Assert.Contains(
            "identity.personas", columnas["perfiles.persona_id"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_comentarios_advierten_que_el_dato_es_autodeclarado()
    {
        // ES LA ADVERTENCIA QUE LOS DE IDENTITY NO NECESITAN. Portal lo carga el
        // docente sobre sí mismo, nadie lo valida y está casi vacío: un conteo sobre
        // estas tablas cuenta a quienes cargaron el dato, no al Departamento.
        // Sin esto, el modelo contesta «ningún docente sabe Python» cuando la verdad
        // es «nadie cargó sus habilidades».
        var tablas = await ComentariosDeTablaAsync();

        Assert.Contains("AUTODECLARADO", tablas["perfiles"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cargaron", tablas["perfiles"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Los_comentarios_explican_qué_significa_un_nulo_en_las_fechas_de_cierre()
    {
        // Tres columnas donde el nulo NO es un dato faltante sino un estado, y
        // leerlo al revés produce una afirmación falsa sobre una persona: un título
        // en curso contado como obtenido, o una certificación que no vence contada
        // como vencida.
        var columnas = await ComentariosDeColumnaAsync();

        Assert.Contains("EN CURSO", columnas["educaciones.hasta"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NO VENCE", columnas["certificaciones.vencimiento"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIGUE", columnas["experiencias.hasta"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_comentario_del_tipo_distingue_habilidad_de_interes()
    {
        // «Sabe Python» e «interesado en dictar Python» son la misma tabla con un
        // filtro distinto. Confundirlos le atribuye a alguien una competencia que no
        // declaró, que es exactamente el tipo de afirmación falsa que la política de
        // abstención existe para impedir.
        var columnas = await ComentariosDeColumnaAsync();
        var tipo = columnas["docente_habilidades.tipo"];

        Assert.Contains("habilidad", tipo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interes", tipo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_comentario_de_termino_norm_dice_en_qué_caja_esta_normalizado()
    {
        // LO ENCONTRÓ LA CORRIDA FINANCIADA. El modelo tradujo «qué docentes saben
        // Kubernetes» a `termino_norm = lower('Kubernetes')` y no devolvió nada: la
        // normalización real es a MAYÚSCULAS y el comentario decía que había que
        // comparar por esa columna sin decir cómo. No es un error del modelo —
        // adivinó, porque el esquema no se lo decía— y hacía fallar TODA pregunta por
        // habilidad, en silencio y con cero filas.
        var columnas = await ComentariosDeColumnaAsync();

        Assert.Contains(
            "MAYÚSCULAS", columnas["habilidades.termino_norm"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_comentarios_nombran_sinonimos_del_dominio()
    {
        // Sin los sinónimos, «quiénes tienen posgrado» no encuentra
        // portal.educaciones: ni «posgrado» ni «título» aparecen en el esquema.
        var tablas = await ComentariosDeTablaAsync();

        Assert.Contains("posgrado", tablas["educaciones"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skill", tablas["habilidades"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("currículum", tablas["perfiles"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Los_comentarios_advierten_las_colisiones_con_designaciones()
    {
        // Portal y designaciones tienen conceptos que se llaman parecido y NO son lo
        // mismo: la carrera que alguien estudió no es una carrera que el Departamento
        // dicte, y un puesto en la industria no es un cargo docente. Sin la
        // advertencia, el modelo une las tablas equivocadas.
        var columnas = await ComentariosDeColumnaAsync();

        Assert.Contains("identity.carreras", columnas["educaciones.carrera"], StringComparison.Ordinal);
        Assert.Contains("designaciones.cargos", columnas["experiencias.puesto"], StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    private Task<IReadOnlyDictionary<string, string>> ComentariosDeTablaAsync() =>
        LeerDiccionarioAsync(
            """
            SELECT c.relname, pg_catalog.obj_description(c.oid, 'pg_class')
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
             WHERE c.relkind = 'r'
               AND n.nspname = 'portal'
               AND pg_catalog.obj_description(c.oid, 'pg_class') IS NOT NULL
            """);

    private Task<IReadOnlyDictionary<string, string>> ComentariosDeColumnaAsync() =>
        LeerDiccionarioAsync(
            """
            SELECT c.relname || '.' || a.attname, pg_catalog.col_description(c.oid, a.attnum)
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
              JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
             WHERE c.relkind = 'r'
               AND n.nspname = 'portal'
               AND a.attnum > 0
               AND NOT a.attisdropped
               AND pg_catalog.col_description(c.oid, a.attnum) IS NOT NULL
            """);

    private async Task<IReadOnlyList<string>> ColumnasRealesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT c.relname || '.' || a.attname
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
              JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
             WHERE c.relkind = 'r'
               AND n.nspname = 'portal'
               AND c.relname = ANY(@tablas)
               AND a.attnum > 0
               AND NOT a.attisdropped
             ORDER BY 1
            """, conexion);

        comando.Parameters.AddWithValue("tablas", Expuestas);

        await using var lector = await comando.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);

        var columnas = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            columnas.Add(lector.GetString(0));
        }

        return columnas;
    }

    private async Task<IReadOnlyDictionary<string, string>> LeerDiccionarioAsync(string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        var filas = new Dictionary<string, string>(StringComparer.Ordinal);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            filas[lector.GetString(0)] = lector.GetString(1);
        }

        return filas;
    }
}
