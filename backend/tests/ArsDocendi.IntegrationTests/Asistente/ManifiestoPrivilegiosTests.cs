using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verificación del manifiesto de privilegios del asistente en las tres direcciones.
///
/// La dirección 3 —tablas y columnas sin clasificar— corre hoy contra el esquema real y es
/// la que hace que una tabla nueva rompa el CI en vez de quedar concedida en silencio.
/// Las direcciones 1 y 2 comparan contra los privilegios efectivos y necesitan que existan
/// los roles y sus GRANT: se ejercitan acá sobre el comparador y quedan pendientes contra
/// la base real hasta que esas migraciones existan.
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

        Assert.True(desviaciones.Count == 0, Describir(desviaciones));
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

        Assert.True(desviaciones.Count == 0, Describir(desviaciones));
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

    // ------------------------------------------------------- el manifiesto dice lo que promete

    [Fact]
    public void La_cache_de_idempotencia_esta_denegada_con_motivo()
    {
        var manifiesto = Manifiesto.Cargar();

        var entrada = Assert.Single(
            manifiesto.Tablas, t => t is { Schema: "designaciones", Tabla: "idempotencia_comandos" });

        Assert.Equal("denegada-explicita", entrada.Estado);
        Assert.False(string.IsNullOrWhiteSpace(entrada.Motivo));
        Assert.Empty(entrada.Declarados());
    }

    [Fact]
    public void El_schema_de_auditoria_esta_denegado_con_motivo()
    {
        var manifiesto = Manifiesto.Cargar();

        var audit = Assert.Single(manifiesto.Schemas, s => s.Nombre == "audit");

        Assert.Equal("denegado", audit.Estado);
        Assert.False(string.IsNullOrWhiteSpace(audit.Motivo));
        Assert.DoesNotContain("audit", manifiesto.SchemasExpuestos);
    }

    [Fact]
    public void Toda_denegacion_explicita_lleva_motivo_escrito()
    {
        var manifiesto = Manifiesto.Cargar();

        var sinMotivo = manifiesto.Tablas
            .Where(t => !t.EsConcedida && string.IsNullOrWhiteSpace(t.Motivo))
            .Select(t => t.Cualificado)
            .Concat(manifiesto.Tablas
                .SelectMany(t => t.ColumnasDenegadas
                    .Where(c => string.IsNullOrWhiteSpace(c.Motivo))
                    .Select(c => $"{t.Cualificado}.{c.Columna}")))
            .ToList();

        Assert.True(sinMotivo.Count == 0,
            "Denegaciones sin motivo escrito: " + string.Join(", ", sinMotivo));
    }

    [Fact]
    public void Las_columnas_personales_solo_las_lee_el_rol_con_acceso_a_datos_personales()
    {
        var manifiesto = Manifiesto.Cargar();
        string[] personales = ["documento", "cuil", "fecha_nacimiento", "telefono"];

        var personas = Assert.Single(
            manifiesto.Tablas, t => t is { Schema: "identity", Tabla: "personas" });

        foreach (var columna in personales)
        {
            Assert.DoesNotContain(columna, personas.ColumnasConcedidas[RolBasico]);
            Assert.Contains(columna, personas.ColumnasConcedidas[RolPii]);
        }
    }

    // ------------------------------------------------- direcciones 1 y 2, sobre el comparador

    [Fact]
    public void Un_privilegio_concedido_fuera_del_manifiesto_hace_fallar_la_comparacion()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Select(d => new PrivilegioEfectivo(d.Schema, d.Tabla, d.Columna, d.Rol))
            .Append(new PrivilegioEfectivo("identity", "personas", "documento", RolBasico))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, []);

        var detectada = Assert.Single(desviaciones, d => d.Tipo == TipoDesviacion.PrivilegioNoDeclarado);
        Assert.Equal("identity.personas.documento", detectada.Objeto);
        Assert.Contains(RolBasico, detectada.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_privilegio_declarado_que_desaparecio_hace_fallar_la_comparacion()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Where(d => !(d.Schema == "designaciones" && d.Tabla == "designaciones" && d.Columna == "horas"))
            .Select(d => new PrivilegioEfectivo(d.Schema, d.Tabla, d.Columna, d.Rol))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, []);

        var faltantes = desviaciones
            .Where(d => d.Tipo == TipoDesviacion.PrivilegioDeclaradoInexistente)
            .ToList();
        Assert.All(faltantes, d => Assert.Equal("designaciones.designaciones.horas", d.Objeto));
        Assert.NotEmpty(faltantes);
    }

    [Fact]
    public void Un_manifiesto_que_coincide_con_la_base_no_produce_desviaciones()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Select(d => new PrivilegioEfectivo(d.Schema, d.Tabla, d.Columna, d.Rol))
            .ToList();
        var columnas = manifiesto.Tablas
            .Where(t => t.EsConcedida)
            .SelectMany(t => t.ColumnasClasificadas.Select(c => new ColumnaReal(t.Schema, t.Tabla, c)))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, columnas);

        Assert.True(desviaciones.Count == 0, Describir(desviaciones));
    }

    // --------------------------------------------- direcciones 1 y 2, contra los GRANT reales

    [Fact(Skip = "Pendiente ARS-17 y ARS-18: los roles del asistente y sus GRANT todavía no existen. "
                 + "Quitar el Skip en el PR que agrega la migración de privilegios.")]
    public async Task Los_privilegios_efectivos_coinciden_con_el_manifiesto()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = await LeerPrivilegiosEfectivosAsync();
        var columnas = await LeerColumnasRealesAsync(manifiesto.SchemasExpuestos);

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, columnas);

        Assert.True(desviaciones.Count == 0, Describir(desviaciones));
    }

    [Fact(Skip = "Pendiente ARS-17 y ARS-18: los roles del asistente todavía no existen.")]
    public async Task Los_roles_del_asistente_no_tienen_ningun_privilegio_de_mutacion()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT grantee, table_schema, table_name, privilege_type
              FROM information_schema.role_table_grants
             WHERE grantee LIKE 'asistente_ro%'
               AND privilege_type <> 'SELECT'
            """, conexion);

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
             WHERE grantee LIKE 'asistente_ro%'
               AND privilege_type = 'SELECT'
             ORDER BY table_schema, table_name, column_name
            """, conexion);

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var privilegios = new List<PrivilegioEfectivo>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            privilegios.Add(new PrivilegioEfectivo(
                lector.GetString(0), lector.GetString(1), lector.GetString(2), lector.GetString(3)));
        }

        return privilegios;
    }

    private static string Describir(IReadOnlyCollection<Desviacion> desviaciones) =>
        desviaciones.Count == 0
            ? string.Empty
            : $"{desviaciones.Count} desviación(es):\n" + string.Join("\n", desviaciones);
}
