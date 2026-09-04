using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica los comentarios de esquema y el prefijo del prompt (RNF-14).
/// </summary>
/// <remarks>
/// Los <c>COMMENT ON</c> y el prefijo se prueban juntos porque son una sola cosa
/// vista desde dos lados: los comentarios son el contenido y el prefijo es cómo
/// llega al modelo. Un test que verificara el prefijo con comentarios de mentira
/// diría que el renderizador funciona, no que el asistente entiende el esquema.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class PrefijoDeEsquemaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_prefijo")
{
    // ------------------------------------------------------- comentarios

    [Fact]
    public async Task Toda_tabla_concedida_por_el_manifiesto_tiene_comentario()
    {
        var manifiesto = Manifiesto.Cargar();
        var comentadas = await ComentariosDeTablaAsync();

        var sinComentario = manifiesto.Tablas
            .Where(t => t.EsConcedida)
            .Where(t => !comentadas.TryGetValue(t.Cualificado, out var texto)
                        || string.IsNullOrWhiteSpace(texto))
            .Select(t => t.Cualificado)
            .ToArray();

        // Una tabla sin comentario llega al modelo como un nombre pelado y un
        // tipo, y el modelo tiene que adivinar qué significa.
        Assert.Empty(sinComentario);
    }

    [Fact]
    public async Task Toda_columna_concedida_por_el_manifiesto_tiene_comentario()
    {
        var manifiesto = Manifiesto.Cargar();
        var comentadas = await ComentariosDeColumnaAsync();

        var sinComentario = manifiesto.Tablas
            .Where(t => t.EsConcedida)
            .SelectMany(t => t.ColumnasConcedidas.Values
                .SelectMany(columnas => columnas)
                .Distinct(StringComparer.Ordinal)
                .Select(columna => $"{t.Cualificado}.{columna}"))
            .Distinct(StringComparer.Ordinal)
            .Where(clave => !comentadas.TryGetValue(clave, out var texto)
                            || string.IsNullOrWhiteSpace(texto))
            .OrderBy(clave => clave, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(sinComentario);
    }

    [Fact]
    public async Task Ninguna_tabla_denegada_esta_comentada()
    {
        var manifiesto = Manifiesto.Cargar();
        var comentadas = await ComentariosDeTablaAsync();

        // Describirle al modelo algo que no puede leer solo sirve para que lo
        // pida y choque con permission denied, en vez de abstenerse.
        var comentadasDeMas = manifiesto.Tablas
            .Where(t => !t.EsConcedida)
            .Where(t => comentadas.ContainsKey(t.Cualificado))
            .Select(t => t.Cualificado)
            .ToArray();

        Assert.Empty(comentadasDeMas);
    }

    [Fact]
    public async Task Los_comentarios_nombran_sinonimos_del_dominio()
    {
        // No es cosmética: sin los sinónimos, «cuántos profesores tiene
        // Algoritmos» no encuentra identity.personas, porque «profesor» no
        // aparece en ningún identificador del esquema.
        var comentarios = await ComentariosDeTablaAsync();

        Assert.Contains("docente", comentarios["identity.personas"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cátedra", comentarios["identity.materias"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trámite", comentarios["designaciones.pedidos"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Los_comentarios_advierten_las_colisiones_del_dominio()
    {
        // El detector de ambigüedad —épica posterior— existe porque los nombres de
        // materia se repiten entre carreras y los apellidos entre personas. El
        // modelo tiene que saberlo desde el prompt, o va a resolver por su cuenta.
        var columnas = await ComentariosDeColumnaAsync();

        Assert.Contains("repite", columnas["identity.materias.name"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repite", columnas["identity.personas.apellido"], StringComparison.OrdinalIgnoreCase);
    }

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
    public async Task El_prefijo_lleva_los_comentarios_del_catalogo()
    {
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        // La comprobación de que los COMMENT ON efectivamente viajan: sin esto,
        // el prefijo sería una lista de nombres y tipos.
        Assert.Contains("Padrón de personas físicas", esquema.Prefijo, StringComparison.Ordinal);
        Assert.Contains("vigencia ABIERTA", esquema.Prefijo, StringComparison.Ordinal);
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

    [Fact]
    public async Task El_prefijo_prohibe_el_reloj_y_la_configuracion_de_sesion()
    {
        // La prohibición vive también en el validador. Acá es un pedido, allá una
        // imposición: pedirlo mejora la tasa de acierto y ahorra rechazos.
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        Assert.Contains("funciones de reloj", esquema.Prefijo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("set_config", esquema.Prefijo, StringComparison.Ordinal);
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
    public async Task La_base_se_consulta_una_sola_vez_por_rol()
    {
        var proveedor = ProveedorNuevo();
        var ct = TestContext.Current.CancellationToken;

        for (var turno = 0; turno < 5; turno++)
        {
            await proveedor.ObtenerAsync(false, ct);
        }

        Assert.Equal(1, proveedor.Lecturas);

        await proveedor.ObtenerAsync(true, ct);

        // El rol con datos personales tiene su propio prefijo y su propia entrada:
        // son dos, no uno compartido.
        Assert.Equal(2, proveedor.Lecturas);
    }

    [Fact]
    public async Task Varios_turnos_concurrentes_lo_calculan_una_sola_vez()
    {
        var proveedor = ProveedorNuevo();
        var ct = TestContext.Current.CancellationToken;

        var turnos = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => proveedor.ObtenerAsync(false, ct)));

        Assert.Equal(1, proveedor.Lecturas);
        Assert.All(turnos, turno => Assert.Equal(turnos[0].Prefijo, turno.Prefijo));
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

    [Fact]
    public async Task La_huella_tiene_forma_de_resumen_criptografico()
    {
        var esquema = await PrefijoAsync(conDatosPersonales: false);

        Assert.Equal(64, esquema.Huella.Length);
        Assert.All(esquema.Huella, caracter => Assert.Contains(caracter, "0123456789abcdef"));
    }

    // ------------------------------------------------------------------ apoyo

    private ProveedorDeEsquema ProveedorNuevo()
    {
        var (basica, conDatosPersonales) = CadenasDeLectura();
        return new ProveedorDeEsquema(basica, conDatosPersonales);
    }

    private Task<Modules.Asistente.Application.EsquemaParaPrompt> PrefijoAsync(bool conDatosPersonales) =>
        ProveedorNuevo().ObtenerAsync(conDatosPersonales, TestContext.Current.CancellationToken);

    private async Task<IReadOnlyDictionary<string, string>> ComentariosDeTablaAsync() =>
        await LeerDiccionarioAsync(
            """
            SELECT n.nspname || '.' || c.relname,
                   pg_catalog.obj_description(c.oid, 'pg_class')
              FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
             WHERE c.relkind = 'r'
               AND n.nspname IN ('identity', 'designaciones')
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
               AND n.nspname IN ('identity', 'designaciones')
               AND pg_catalog.col_description(c.oid, a.attnum) IS NOT NULL
            """);

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

    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
