using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verificación del manifiesto de privilegios del asistente en las tres direcciones.
///
/// La dirección 3 —tablas y columnas sin clasificar— corre contra el esquema real y es
/// la que hace que una tabla nueva rompa el CI en vez de quedar concedida en silencio.
/// Las direcciones 1 y 2 comparan el manifiesto contra los privilegios efectivos: se
/// ejercitan primero sobre el comparador, con desviaciones sintéticas, y después contra
/// los GRANT que aplicó de verdad la migración del módulo sobre la base de prueba.
///
/// Los roles reales llevan sufijo por ambiente (y en los tests, uno por base), así que
/// hay que traducirlos a los nombres lógicos del manifiesto antes de comparar.
/// </summary>
[Collection(ColeccionPostgres.Nombre)]
public sealed class ManifiestoPrivilegiosTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_manifiesto")
{
    private const string RolBasico = "asistente_ro";
    private const string RolPii = "asistente_ro_pii";

    // ---------------------------------------------------------------- dirección 3, base real

    [Fact]
    public async Task Toda_tabla_de_un_schema_expuesto_esta_clasificada()
    {
        var manifiesto = Manifiesto.Cargar();
        var columnas = await LeerColumnasRealesAsync(manifiesto.SchemasExpuestos);

        var desviaciones = ComparadorManifiesto
            .Comparar(manifiesto, [], columnas)
            .Where(d => d.Tipo == TipoDesviacion.TablaSinClasificar)
            .ToList();

        Assert.True(desviaciones.Count == 0, ComparadorManifiesto.Describir(desviaciones));
    }

    [Fact]
    public async Task Toda_columna_de_una_tabla_concedida_esta_clasificada()
    {
        var manifiesto = Manifiesto.Cargar();
        var columnas = await LeerColumnasRealesAsync(manifiesto.SchemasExpuestos);

        var desviaciones = ComparadorManifiesto
            .Comparar(manifiesto, [], columnas)
            .Where(d => d.Tipo == TipoDesviacion.ColumnaSinClasificar)
            .ToList();

        Assert.True(desviaciones.Count == 0, ComparadorManifiesto.Describir(desviaciones));
    }

    [Fact]
    public async Task Una_tabla_nueva_sin_clasificar_hace_fallar_la_comparacion()
    {
        var manifiesto = Manifiesto.Cargar();
        var columnas = await LeerColumnasRealesAsync(manifiesto.SchemasExpuestos);
        var conTablaNueva = columnas
            .Append(new ColumnaReal("designaciones", "tabla_que_alguien_agrego", "payload"))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, [], conTablaNueva);

        var detectada = Assert.Single(desviaciones, d => d.Tipo == TipoDesviacion.TablaSinClasificar);
        Assert.Contains("tabla_que_alguien_agrego", detectada.Objeto, StringComparison.Ordinal);
    }

    // ------------------------------------------- dirección 4, el schema que nadie clasificó

    [Fact]
    public async Task Todo_schema_de_la_base_esta_clasificado_en_el_manifiesto()
    {
        // LA DIRECCIÓN QUE FALTABA, y el agujero que tapa es de schema entero.
        //
        // Las otras tres recorren SOLO los schemas que el manifiesto marca como
        // expuestos, o miran privilegios efectivos. Entre las dos cosas queda un
        // hueco: un schema nuevo, sin ningún GRANT todavía y sin entrada en el
        // manifiesto, es invisible para las cuatro. La suite pasa en verde y la
        // decisión de exponerlo o no queda registrada únicamente en la cabeza de
        // quien escribió el PR.
        //
        // Pasó con `portal`, que entró con un merge y estuvo sin clasificar. Va a
        // volver a pasar con `tareas` y con `aulas`.
        //
        // Se exige la ENTRADA, no un estado en particular: `denegado` con motivo es
        // una respuesta perfectamente válida, y es la que corresponde casi siempre.
        // Lo que no se acepta es el silencio.
        var manifiesto = Manifiesto.Cargar();
        var declarados = manifiesto.Schemas
            .Select(schema => schema.Nombre)
            .ToHashSet(StringComparer.Ordinal);

        var sinClasificar = (await LeerSchemasRealesAsync())
            .Where(schema => !declarados.Contains(schema))
            .ToList();

        Assert.True(
            sinClasificar.Count == 0,
            "Hay schemas en la base que el manifiesto de privilegios no clasifica: "
            + string.Join(", ", sinClasificar)
            + ". Agregá una entrada por cada uno, con estado 'expuesto' o 'denegado' "
            + "y su motivo. Un schema sin entrada no lo mira ninguna de las otras "
            + "tres direcciones.");
    }

    // --------------------------------------------- direcciones 1 y 2, contra los GRANT reales

    [Fact]
    public async Task Los_privilegios_efectivos_coinciden_con_el_manifiesto()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = await LeerPrivilegiosEfectivosAsync();
        var columnas = await LeerColumnasRealesAsync(manifiesto.SchemasExpuestos);

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, columnas);

        Assert.True(desviaciones.Count == 0, ComparadorManifiesto.Describir(desviaciones));
    }

    [Fact]
    public async Task Los_roles_del_asistente_no_tienen_ningun_privilegio_de_mutacion()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT grantee, table_schema, table_name, privilege_type
              FROM information_schema.role_table_grants
             WHERE grantee = ANY(@roles)
               AND privilege_type <> 'SELECT'
            """, conexion);
        comando.Parameters.AddWithValue("roles", new[] { RolSoloLectura, RolSoloLecturaPii });

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var mutaciones = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            mutaciones.Add(
                $"{lector.GetString(0)} tiene {lector.GetString(3)} sobre {lector.GetString(1)}.{lector.GetString(2)}");
        }

        Assert.True(mutaciones.Count == 0, string.Join("\n", mutaciones));
    }

    // ------------------------------------------------------------------------------ lectura

    /// <summary>
    /// Los schemas de usuario que la base tiene realmente.
    /// </summary>
    /// <remarks>
    /// Se excluyen los del motor —<c>pg_*</c> e <c>information_schema</c>— porque no
    /// son decisiones nuestras y clasificarlos no diría nada. Todo lo demás es algo
    /// que alguien de este repositorio creó, y entonces alguien tiene que haber
    /// decidido si el asistente lo ve.
    /// </remarks>
    private async Task<List<string>> LeerSchemasRealesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT nspname
              FROM pg_catalog.pg_namespace
             WHERE nspname NOT LIKE 'pg\_%'
               AND nspname <> 'information_schema'
             ORDER BY nspname
            """, conexion);

        await using var lector = await comando.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);

        var schemas = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            schemas.Add(lector.GetString(0));
        }

        return schemas;
    }

    private async Task<List<ColumnaReal>> LeerColumnasRealesAsync(IReadOnlyList<string> schemas)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT c.table_schema, c.table_name, c.column_name
              FROM information_schema.columns c
              JOIN information_schema.tables t
                ON t.table_schema = c.table_schema
               AND t.table_name = c.table_name
             WHERE c.table_schema = ANY(@schemas)
               AND t.table_type = 'BASE TABLE'
             ORDER BY c.table_schema, c.table_name, c.column_name
            """, conexion);
        comando.Parameters.AddWithValue("schemas", schemas.ToArray());

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var columnas = new List<ColumnaReal>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            columnas.Add(new ColumnaReal(lector.GetString(0), lector.GetString(1), lector.GetString(2)));
        }

        return columnas;
    }

    private async Task<List<PrivilegioEfectivo>> LeerPrivilegiosEfectivosAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT table_schema, table_name, column_name, grantee
              FROM information_schema.column_privileges
             WHERE grantee = ANY(@roles)
               AND privilege_type = 'SELECT'
             ORDER BY table_schema, table_name, column_name
            """, conexion);
        comando.Parameters.AddWithValue("roles", new[] { RolSoloLectura, RolSoloLecturaPii });

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var privilegios = new List<PrivilegioEfectivo>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            privilegios.Add(new PrivilegioEfectivo(
                lector.GetString(0), lector.GetString(1), lector.GetString(2),
                NombreLogicoDelRol(lector.GetString(3))));
        }

        return privilegios;
    }

    /// <summary>
    /// Traduce el nombre real del rol al nombre lógico del manifiesto. El manifiesto
    /// habla de <c>asistente_ro</c> y <c>asistente_ro_pii</c>; la base tiene
    /// <c>asistente_ro_pr_123</c> o, acá, un sufijo por base de prueba.
    /// </summary>
    private string NombreLogicoDelRol(string rolReal) =>
        rolReal == RolSoloLecturaPii ? RolPii
        : rolReal == RolSoloLectura ? RolBasico
        : rolReal;
}
