using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>Verifica los comentarios de esquema y el prefijo del prompt (RNF-14).</summary>
public sealed class PrefijoDeEsquemaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_prefijo")
{
    // ------------------------------------------------------- comentarios

    // ----------------------------------------------------------- prefijo

    [Fact]
    public async Task El_prefijo_describe_las_tablas_que_el_rol_puede_leer()
    {
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        Assert.Contains("identity.personas", esquema.Prefijo, StringComparison.Ordinal);
        Assert.Contains("designaciones.pedidos", esquema.Prefijo, StringComparison.Ordinal);
        Assert.Contains("designaciones.designaciones", esquema.Prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_prefijo_no_menciona_ninguna_tabla_denegada()
    {
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        foreach (var denegada in Manifiesto.Cargar().Tablas.Where(t => !t.EsConcedida))
        {
            Assert.DoesNotContain(denegada.Cualificado, esquema.Prefijo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task El_prefijo_no_menciona_el_schema_de_auditoria()
    {
        // audit tiene REVOKE USAGE, así que has_schema_privilege lo deja afuera
        // solo. Si apareciera, sería la señal de que ese REVOKE se perdió.
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        Assert.DoesNotContain("audit.", esquema.Prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_dos_roles_difieren_exactamente_en_las_columnas_personales()
    {
        var basico = await PrefijoAsync(conDatosPersonales: false);
        var conDatos = await PrefijoAsync(conDatosPersonales: true);

        Assert.NotEqual(basico.Prefijo, conDatos.Prefijo);

        foreach (var personal in new[] { "documento", "cuil", "fecha_nacimiento", "telefono" })
        {
            Assert.Contains($"- {personal} (", conDatos.Prefijo, StringComparison.Ordinal);
            Assert.DoesNotContain($"- {personal} (", basico.Prefijo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task El_prefijo_describe_como_se_relacionan_las_tablas()
    {
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        Assert.Contains("Cómo se relacionan", esquema.Prefijo, StringComparison.Ordinal);
        Assert.Contains(
            "designaciones.pedidos.materia_id referencia a identity.materias.id",
            esquema.Prefijo,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_prefijo_trae_los_hechos_de_comisiones_que_el_modelo_no_puede_inferir(
        bool conDatosPersonales)
    {
        var prefijo = (await PrefijoAsync(conDatosPersonales)).Prefijo;

        // Several períodos accumulate, aula/responsabilidad are free text, dia is 1-7, and a null
        // link means "not matched": without these the model reads a wrong answer as a right one.
        Assert.Contains("ACUMULA varios períodos", prefijo, StringComparison.Ordinal);
        Assert.Contains("TEXTO LIBRE", prefijo, StringComparison.Ordinal);
        Assert.Contains("sábado = 6", prefijo, StringComparison.Ordinal);
        Assert.Contains("Nulo significa", prefijo, StringComparison.Ordinal);

        // The import log is denied: it must not even be named.
        Assert.DoesNotContain("sga_importaciones", prefijo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_prefijo_trae_los_proyectos_del_portal_sin_su_descripcion(bool conDatosPersonales)
    {
        var prefijo = (await PrefijoAsync(conDatosPersonales)).Prefijo;

        Assert.Contains("portal.proyectos", prefijo, StringComparison.Ordinal);
        Assert.Contains("- doi (", prefijo, StringComparison.Ordinal);

        // The free-text description is denied: it must not even be named as a column of projects.
        Assert.DoesNotContain("portal.proyectos.descripcion", prefijo, StringComparison.Ordinal);
        Assert.DoesNotContain("proyecto_documentos", prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_prefijo_lista_las_claves_foraneas_reales_de_comisiones()
    {
        var prefijo = (await PrefijoAsync(conDatosPersonales: false)).Prefijo;

        foreach (var relacion in new[]
        {
            "comisiones.sga_comisiones.materia_id referencia a identity.materias.id",
            "comisiones.sga_comision_carreras.carrera_id referencia a identity.carreras.id",
            "comisiones.sga_comision_docentes.persona_id referencia a identity.personas.id",
            "comisiones.sga_comision_horarios.comision_guarani_id referencia a comisiones.sga_comisiones.comision_guarani_id",
        })
        {
            Assert.Contains(relacion, prefijo, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_prefijo_trae_los_hechos_de_aulas_que_el_modelo_no_puede_inferir(
        bool conDatosPersonales)
    {
        var prefijo = (await PrefijoAsync(conDatosPersonales)).Prefijo;

        // These are SOLICITUDES, not the aula where a comisión is taught (that is Guaraní's horarios);
        // there is no aula catalog; aula_asignada exists only when approved.
        Assert.Contains("aulas.solicitudes_reserva", prefijo, StringComparison.Ordinal);
        Assert.Contains("NO es el aula en que se dicta una comisión", prefijo, StringComparison.Ordinal);
        Assert.Contains("No hay catálogo de aulas", prefijo, StringComparison.Ordinal);
        Assert.Contains("Sólo existe cuando estado es aprobada", prefijo, StringComparison.Ordinal);
        Assert.Contains(
            "aulas.solicitudes_reserva.docente_id referencia a identity.personas.id",
            prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public void Ninguna_lista_de_valores_cerrados_lee_filas_de_aulas()
    {
        // Same reason as comisiones: the prompt connection has no actor, so the policy would hide
        // the rows, and a prompt shared by every actor must not carry values of a protected table.
        Assert.DoesNotContain(
            LectorDeValoresDeCatalogo.Declaradas(conGlosario: true),
            columna => columna.Esquema == "aulas");
    }

    [Fact]
    public void Ninguna_lista_de_valores_cerrados_lee_filas_de_comisiones()
    {
        // The prompt connection has no actor, so a list read from a row-protected table would come
        // back empty; and putting row values of such a table in a prompt shared by every actor is the
        // thing the policy exists to prevent.
        Assert.DoesNotContain(
            LectorDeValoresDeCatalogo.Declaradas(conGlosario: true),
            columna => columna.Esquema == "comisiones");
    }

    // --------------------------------------------------- estabilidad y caché

    [Fact]
    public async Task El_prefijo_es_identico_entre_llamadas()
    {
        var proveedor = ProveedorNuevo();
        var ct = TestContext.Current.CancellationToken;

        var primero = await proveedor.ObtenerAsync(false, ct);
        var segundo = await proveedor.ObtenerAsync(false, ct);

        Assert.Equal(primero.Prefijo, segundo.Prefijo);
    }

    [Fact]
    public async Task El_prefijo_no_contiene_nada_variable_por_turno()
    {
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        // Ni la fecha de hoy ni la de ayer: el prefijo no puede tener fechas.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.DoesNotContain(hoy.ToString("yyyy-MM-dd"), esquema.Prefijo, StringComparison.Ordinal);
        Assert.DoesNotContain(
            hoy.AddDays(-1).ToString("yyyy-MM-dd"), esquema.Prefijo, StringComparison.Ordinal);
        Assert.DoesNotContain("app.asistente_user_id", esquema.Prefijo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- huella

    [Fact]
    public async Task La_huella_es_la_misma_en_dos_proveedores_distintos()
    {
        var ct = TestContext.Current.CancellationToken;

        var primera = await ProveedorNuevo().ObtenerAsync(false, ct);
        var segunda = await ProveedorNuevo().ObtenerAsync(false, ct);

        Assert.Equal(primera.Huella, segunda.Huella);
    }

    [Fact]
    public async Task Los_dos_roles_tienen_huellas_distintas()
    {
        var basico = await PrefijoAsync(conDatosPersonales: false);
        var conDatos = await PrefijoAsync(conDatosPersonales: true);

        Assert.NotEqual(basico.Huella, conDatos.Huella);
    }

    [Fact]
    public async Task Conceder_una_columna_cambia_la_huella()
    {
        var antes = await PrefijoAsync(conDatosPersonales: false);

        await EjecutarAsync(
            $"GRANT SELECT (azure_oid) ON identity.users TO \"{RolSoloLectura}\"");

        // Proveedor nuevo: el prefijo NO se invalida solo, y ése es el diseño.
        // Una migración de esquema exige reiniciar el proceso.
        var despues = await PrefijoAsync(conDatosPersonales: false);

        Assert.NotEqual(antes.Huella, despues.Huella);
        Assert.Contains("- azure_oid (", despues.Prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revocar_una_columna_la_saca_del_prefijo()
    {
        var antes = await PrefijoAsync(conDatosPersonales: false);
        Assert.Contains("- legajo (", antes.Prefijo, StringComparison.Ordinal);

        await EjecutarAsync(
            $"REVOKE SELECT (legajo) ON identity.personas FROM \"{RolSoloLectura}\"");

        var despues = await PrefijoAsync(conDatosPersonales: false);

        // La dirección peligrosa: con una lista embebida en el código, el prompt
        // seguiría ofreciéndole al modelo una columna que ya no puede leer, y el
        // turno fallaría con permission denied en vez de abstenerse.
        Assert.DoesNotContain("- legajo (", despues.Prefijo, StringComparison.Ordinal);
    }

    // ------------------------------- valores de los catálogos cerrados

    // EL CASO QUE MOTIVÓ TODO ESTO. Alguien preguntó por «ingeniería informática»,
    // el modelo copió esas palabras al WHERE y la carrera se llama «Ingeniería EN
    // Informática». Cero filas, SQL válido, y el turno respondió que no había nada.
    // Ni el motor ni el validador pueden notarlo: un literal que no matchea es
    // indistinguible de un dato que no existe.

    [Fact]
    public async Task El_prefijo_enumera_los_valores_de_los_catalogos_cerrados()
    {
        await SembrarAsync();
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        // El nombre exacto, con el «en» que el modelo no tenía forma de adivinar.
        Assert.Contains("Ingeniería en Informática", esquema.Prefijo, StringComparison.Ordinal);
        Assert.Contains("Profesor Titular", esquema.Prefijo, StringComparison.Ordinal);
        Assert.Contains("VALORES POSIBLES", esquema.Prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_glosario_el_prefijo_enumera_las_dedicaciones_y_sin_el_no()
    {
        var sin = await PrefijoAsync(conDatosPersonales: false);
        var con = await PrefijoConGlosarioAsync(conDatosPersonales: false);

        Assert.DoesNotContain("Categoría 1", sin.Prefijo, StringComparison.Ordinal);
        Assert.DoesNotContain("designaciones.dedicaciones.codigo", sin.Prefijo, StringComparison.Ordinal);

        Assert.Contains(
            "- designaciones.dedicaciones.nombre: "
            + "'Categoría 1', 'Categoría 2', 'Categoría 3', 'Categoría 4', 'Categoría 5', 'Categoría 6'",
            con.Prefijo, StringComparison.Ordinal);
        Assert.Contains(
            "- designaciones.dedicaciones.codigo: '1', '2', '3', '4', '5', '6'",
            con.Prefijo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_bloque_del_glosario_es_el_mismo_en_el_prefijo_basico_y_en_el_de_datos_personales()
    {
        // El glosario sólo puede nombrar columnas del rol básico, así que no hay nada que distinga
        // un prefijo del otro en su bloque.
        var basico = await PrefijoConGlosarioAsync(conDatosPersonales: false);
        var personal = await PrefijoConGlosarioAsync(conDatosPersonales: true);

        Assert.NotEqual(basico.Prefijo, personal.Prefijo);
        Assert.Equal(BloqueDelGlosario(basico.Prefijo), BloqueDelGlosario(personal.Prefijo));
        Assert.Equal(
            RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos),
            BloqueDelGlosario(basico.Prefijo));
    }

    [Fact]
    public async Task Con_el_glosario_apagado_el_prefijo_es_el_de_siempre_para_los_dos_roles()
    {
        // EL DEFAULT. Sin opciones y con
        // las opciones en su default tienen que dar el mismo prefijo, y ninguno
        // puede mencionar el glosario ni las dedicaciones.
        var conOpciones = new ProveedorDeEsquema(Apertura, Options.Create(new OpcionesAsistente()));

        foreach (var conDatosPersonales in new[] { false, true })
        {
            var sinOpciones = await ProveedorNuevo().ObtenerAsync(conDatosPersonales, TestContext.Current.CancellationToken);
            var porDefault = await conOpciones.ObtenerAsync(conDatosPersonales, TestContext.Current.CancellationToken);

            Assert.Equal(sinOpciones.Prefijo, porDefault.Prefijo);
            Assert.DoesNotContain("GLOSARIO INSTITUCIONAL", porDefault.Prefijo, StringComparison.Ordinal);
            Assert.DoesNotContain("- designaciones.dedicaciones.codigo:", porDefault.Prefijo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task El_sello_del_evaluador_cambia_con_el_glosario()
    {
        // `EsquemaParaPrompt.Huella` es lo que sella los reportes de evaluación: si
        // no cambiara con la opción, el gate de regresión compararía una corrida con
        // glosario contra una línea de base sin él como si fueran el mismo prompt.
        var sin = await PrefijoAsync(conDatosPersonales: false);
        var con = await PrefijoConGlosarioAsync(conDatosPersonales: false);

        Assert.NotEqual(sin.Huella, con.Huella);
    }

    [Fact]
    public async Task El_codigo_que_el_prefijo_ensena_a_escribir_se_ejecuta()
    {
        // `codigo` es SMALLINT y el prefijo lo lista como texto: '3'. PostgreSQL resuelve el
        // literal sin tipo contra la columna, y esto lo prueba contra la base real con el rol del
        // asistente, para que un modelo que copie la forma no aprenda un filtro que falla.
        var esquema = await PrefijoConGlosarioAsync(conDatosPersonales: false);
        Assert.Contains("'3'", esquema.Prefijo, StringComparison.Ordinal);

        await using var conexion = await AbrirConexionComoAsistenteAsync(conDatosPersonales: false);
        await using var comando = new NpgsqlCommand(
            "SELECT nombre FROM designaciones.dedicaciones WHERE codigo = '3'", conexion);

        Assert.Equal(
            "Categoría 3",
            await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ apoyo

    private async Task<IReadOnlyList<VocabularioDeUnaColumna>> ValoresAsync(bool conGlosario = false)
    {
        var (basica, _) = CadenasDeLectura();
        await using var conexion = new NpgsqlConnection(basica.Valor);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);

        return await LectorDeValoresDeCatalogo.LeerAsync(
            conexion, TestContext.Current.CancellationToken, conGlosario);
    }

    /// <summary>Desde el encabezado del glosario hasta el final del prefijo.</summary>
    private static string BloqueDelGlosario(string prefijo) =>
        prefijo[prefijo.IndexOf("\nGLOSARIO INSTITUCIONAL", StringComparison.Ordinal)..];

    private Task<Modules.Asistente.Application.EsquemaParaPrompt> PrefijoConGlosarioAsync(bool conDatosPersonales) =>
        new ProveedorDeEsquema(
                Apertura,
                Options.Create(new OpcionesAsistente { GlosarioEnElPrefijo = true }))
            .ObtenerAsync(conDatosPersonales, TestContext.Current.CancellationToken);

    private ProveedorDeEsquema ProveedorNuevo()
    {
        var (basica, conDatosPersonales) = CadenasDeLectura();
        return new ProveedorDeEsquema(Apertura);
    }

    private Task<Modules.Asistente.Application.EsquemaParaPrompt> PrefijoAsync(bool conDatosPersonales) =>
        ProveedorNuevo().ObtenerAsync(conDatosPersonales, TestContext.Current.CancellationToken);

    /// <summary>Los schemas que el manifiesto declara expuestos.</summary>
    private static string[] SchemasExpuestos() => [.. Manifiesto.Cargar().SchemasExpuestos];

    private async Task<IReadOnlyDictionary<string, string>> ComentariosDeTablaAsync() =>
        await LeerDiccionarioAsync(
            """
            SELECT n.nspname || '.' || c.relname,
                   pg_catalog.obj_description(c.oid, 'pg_class')
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
             WHERE c.relkind = 'r'
               AND n.nspname = ANY(@schemas)
               AND pg_catalog.obj_description(c.oid, 'pg_class') IS NOT NULL
            """);

    private async Task<IReadOnlyDictionary<string, string>> ComentariosDeColumnaAsync() =>
        await LeerDiccionarioAsync(
            """
            SELECT n.nspname || '.' || c.relname || '.' || a.attname,
                   pg_catalog.col_description(c.oid, a.attnum)
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
              JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
             WHERE c.relkind = 'r'
               AND a.attnum > 0
               AND NOT a.attisdropped
               AND n.nspname = ANY(@schemas)
               AND pg_catalog.col_description(c.oid, a.attnum) IS NOT NULL
            """);

    /// <summary>Cuántas tablas con comentario tiene un schema en la base.</summary>
    private async Task<long> ContarTablasComentadasAsync(string schema)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT count(*)
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
             WHERE c.relkind = 'r'
               AND n.nspname = @schema
               AND pg_catalog.obj_description(c.oid, 'pg_class') IS NOT NULL
            """, conexion);

        comando.Parameters.AddWithValue("schema", schema);
        return (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<IReadOnlyDictionary<string, string>> LeerDiccionarioAsync(string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        var filas = new Dictionary<string, string>(StringComparer.Ordinal);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("schemas", SchemasExpuestos());
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            filas[lector.GetString(0)] = lector.GetString(1);
        }

        return filas;
    }

}
