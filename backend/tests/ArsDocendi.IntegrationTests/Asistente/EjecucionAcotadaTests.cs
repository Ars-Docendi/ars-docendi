using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la ejecución envuelta de la consulta generada.
/// </summary>
/// <remarks>
/// Tres capas independientes, y cada test mira una: el rol sin privilegios de
/// mutación, la transacción <c>READ ONLY</c> y las policies con el actor fijado
/// transaction-local. El validador es una cuarta que corre antes y se prueba
/// aparte, en memoria.
///
/// Datos del seed sintético: ocho pedidos en total, siete de la carrera INF y uno
/// de IND.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class EjecucionAcotadaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_ejecucion")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    /// <summary>Identificador del directorio externo de la secretaria.</summary>
    /// <remarks>
    /// Es el par exacto del anterior: el mismo usuario, visto por el proveedor de
    /// identidad. Los dos son UUID, así que confundirlos compila y ejecuta.
    /// </remarks>
    private static readonly Guid OidDeAzure = Guid.Parse("a9000000-0000-4000-8000-000000000004");

    private const string ContarPedidos = "SELECT count(*) AS cantidad FROM designaciones.pedidos";

    // ------------------------------------------------------------- el alcance

    [Fact]
    public async Task El_actor_acota_el_resultado()
    {
        await SembrarAsync();

        var global = await EjecutarAsync(ContarPedidos, Secretaria);
        var deCarrera = await EjecutarAsync(ContarPedidos, Coordinador);

        Assert.Equal(8L, global.Filas[0][0]);
        Assert.Equal(7L, deCarrera.Filas[0][0]);
    }

    [Fact]
    public async Task Dos_turnos_consecutivos_no_heredan_el_actor()
    {
        await SembrarAsync();
        var ejecutor = Ejecutor();
        var ct = TestContext.Current.CancellationToken;

        var primero = await ejecutor.EjecutarAsync(ContarPedidos, Secretaria, false, ct);
        var segundo = await ejecutor.EjecutarAsync(ContarPedidos, Coordinador, false, ct);

        // Con un ajuste de sesión en vez de transaction-local, el segundo turno
        // habría heredado el actor del primero y contestado 8. Ese fallo no tira
        // error: responde con el alcance equivocado.
        Assert.Equal(8L, primero.Filas[0][0]);
        Assert.Equal(7L, segundo.Filas[0][0]);
    }

    [Fact]
    public async Task El_ajuste_del_actor_no_sobrevive_a_la_ejecucion()
    {
        await SembrarAsync();
        await EjecutarAsync(ContarPedidos, Secretaria);

        // Conexión nueva del mismo rol: el ajuste murió con el COMMIT.
        await using var conexion = await AbrirConexionComoAsistenteAsync(false);
        await using var comando = new NpgsqlCommand(
            "SELECT coalesce(current_setting('app.asistente_user_id', true), '')", conexion);

        Assert.Equal(
            string.Empty,
            await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Un_identificador_del_directorio_externo_falla_de_forma_visible()
    {
        await SembrarAsync();

        // Sin esta comprobación, el turno respondería "no encontré nada" sobre una
        // base llena: el oid no corresponde a ninguna fila de identity.users, las
        // policies filtran todo y el error se disfraza de respuesta plausible.
        await Assert.ThrowsAsync<ActorNoResuelto>(() =>
            Perfiles().ObtenerAsync(OidDeAzure, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task El_perfil_distingue_el_alcance_global_del_acotado()
    {
        await SembrarAsync();
        var perfiles = Perfiles();
        var ct = TestContext.Current.CancellationToken;

        Assert.True((await perfiles.ObtenerAsync(Secretaria, ct)).EsGlobal);
        Assert.False((await perfiles.ObtenerAsync(Coordinador, ct)).EsGlobal);
        Assert.False((await perfiles.ObtenerAsync(Docente, ct)).EsGlobal);
    }

    [Fact]
    public async Task El_acceso_a_datos_personales_exige_alcance_global_ademas_del_permiso()
    {
        await SembrarAsync();
        var perfiles = Perfiles();
        var ct = TestContext.Current.CancellationToken;

        // El coordinador no es global: aunque tuviera el permiso, no le
        // corresponde la conexión con datos personales. identity.personas no tiene
        // RLS, así que con esa conexión leería documento y teléfono de todo el
        // padrón — algo que la interfaz le niega.
        Assert.False((await perfiles.ObtenerAsync(Coordinador, ct)).VeDatosPersonales);
    }

    // --------------------------------------------------- solo lectura

    [Fact]
    public async Task Una_escritura_no_sobrevive_a_la_envoltura()
    {
        await SembrarAsync();

        // El validador ya habría rechazado esto. Se prueba igual porque la
        // garantía tiene que valer AUNQUE el validador falle entero.
        //
        // Resultado adicional que vale la pena dejar escrito: la envoltura en
        // subconsulta hace estructuralmente imposible colar DML. PostgreSQL admite
        // una CTE que modifica datos solo en el nivel superior de la sentencia, y
        // acá siempre queda adentro de un SELECT ... FROM (...). Ni siquiera llega
        // a la capa de solo lectura.
        await Assert.ThrowsAsync<PostgresException>(() =>
            EjecutarAsync(
                "SELECT 1 AS uno",
                Secretaria,
                escrituraAdicional: "DELETE FROM designaciones.pedidos"));

        var despues = await EjecutarAsync(ContarPedidos, Secretaria);
        Assert.Equal(8L, despues.Filas[0][0]);
    }

    [Fact]
    public async Task El_rol_del_asistente_no_puede_escribir_ni_fuera_de_la_envoltura()
    {
        await SembrarAsync();

        // La primera capa, sin ninguna de las otras: conexión directa del rol, sin
        // envoltura, sin transacción de solo lectura, sin validador. Si esto
        // pasara, todo lo demás sería decoración.
        await using var conexion = await AbrirConexionComoAsistenteAsync(false);
        await using var comando = new NpgsqlCommand(
            "DELETE FROM designaciones.pedidos", conexion);

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal("42501", excepcion.SqlState);
    }

    [Fact]
    public async Task La_transaccion_de_solo_lectura_rechaza_una_escritura()
    {
        await SembrarAsync();

        // La segunda capa, aislada de la primera: se usa una tabla temporal, que el
        // rol SÍ podría crear si la transacción no fuera de solo lectura. Así el
        // rechazo lo produce READ ONLY y no la falta de privilegio.
        var excepcion = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            await using var conexion = await AbrirConexionComoAsistenteAsync(false);
            await using var transaccion = await conexion.BeginTransactionAsync(ct);

            await using (var soloLectura = new NpgsqlCommand(
                "SET TRANSACTION READ ONLY", conexion, transaccion))
            {
                await soloLectura.ExecuteNonQueryAsync(ct);
            }

            await using var comando = new NpgsqlCommand(
                "CREATE TEMP TABLE colada (id int)", conexion, transaccion);
            await comando.ExecuteNonQueryAsync(ct);
        });

        Assert.Equal("25006", excepcion.SqlState);
    }

    [Fact]
    public async Task La_transaccion_se_declara_de_solo_lectura()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT current_setting('transaction_read_only') AS modo", Secretaria);

        Assert.Equal("on", resultado.Filas[0][0]);
    }

    [Fact]
    public async Task El_timeout_de_sentencia_queda_fijado_en_la_transaccion()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT current_setting('statement_timeout') AS tope", Secretaria);

        // No el valor por omisión del servidor, que es cero: sin él, una consulta
        // con producto cartesiano ocuparía un backend mucho después de que el
        // cliente se haya ido.
        Assert.NotEqual("0", resultado.Filas[0][0]);
    }

    // ------------------------------------------------------ fila sonda

    [Fact]
    public async Task Un_resultado_por_debajo_del_tope_no_se_marca_truncado()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT numero FROM designaciones.pedidos ORDER BY numero", Secretaria, tope: 100);

        Assert.Equal(8, resultado.Filas.Count);
        Assert.False(resultado.Truncado);
    }

    [Fact]
    public async Task Un_resultado_exactamente_en_el_tope_no_se_marca_truncado()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT numero FROM designaciones.pedidos ORDER BY numero", Secretaria, tope: 8);

        // Ocho filas y tope ocho: la fila sonda —la novena— no existe, así que no
        // hubo recorte. Con un límite exacto este caso sería indistinguible del
        // siguiente.
        Assert.Equal(8, resultado.Filas.Count);
        Assert.False(resultado.Truncado);
    }

    [Fact]
    public async Task Un_resultado_por_encima_del_tope_se_recorta_y_se_marca()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT numero FROM designaciones.pedidos ORDER BY numero", Secretaria, tope: 3);

        Assert.Equal(3, resultado.Filas.Count);
        Assert.True(resultado.Truncado);
    }

    [Fact]
    public async Task La_fila_sonda_nunca_sale_del_ejecutor()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT numero FROM designaciones.pedidos ORDER BY numero", Secretaria, tope: 5);

        // Exactamente las del tope, nunca una más: la sonda sirve para SABER que
        // hubo recorte, no para devolverla.
        Assert.Equal(5, resultado.Filas.Count);
        Assert.True(resultado.Truncado);
    }

    [Fact]
    public async Task El_indicador_de_truncado_es_booleano_y_no_un_conteo()
    {
        // «Ves 3 de 124» es un canal de inferencia sobre datos que el usuario no
        // puede ver. Que el tipo sea bool es lo que hace imposible filtrarlo.
        var truncado = typeof(ResultadoDeConsulta).GetProperty(nameof(ResultadoDeConsulta.Truncado));

        Assert.NotNull(truncado);
        Assert.Equal(typeof(bool), truncado.PropertyType);

        var conteos = typeof(ResultadoDeConsulta).GetProperties()
            .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(long))
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(conteos);
    }

    // ------------------------------------------------- guard de vacío

    [Fact]
    public async Task Cero_filas_se_reconoce_como_vacio()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'", Secretaria);

        Assert.Empty(resultado.Filas);
        Assert.True(resultado.EstaVacio);
    }

    [Fact]
    public async Task Una_agregacion_sobre_nada_devuelve_una_fila_de_nulos_y_es_vacio()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT max(horas) AS tope FROM designaciones.designaciones WHERE horas < 0",
            Secretaria);

        // Ésta es la forma que no es obvia: la consulta devuelve UNA fila, no cero.
        // Un guard que solo mirara el conteo la daría por resultado con datos y la
        // redacción hablaría de un máximo que no existe.
        Assert.Single(resultado.Filas);
        Assert.Null(resultado.Filas[0][0]);
        Assert.True(resultado.EstaVacio);
    }

    [Fact]
    public async Task Un_conteo_en_cero_no_es_vacio()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT count(*) AS cantidad FROM designaciones.pedidos WHERE numero = 'no-existe'",
            Secretaria);

        // count(*) sobre nada devuelve cero, y cero SÍ dice algo.
        Assert.Single(resultado.Filas);
        Assert.Equal(0L, resultado.Filas[0][0]);
        Assert.False(resultado.EstaVacio);
    }

    [Fact]
    public async Task Las_columnas_llegan_con_su_nombre()
    {
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            """SELECT count(*) AS "cantidad de pedidos" FROM designaciones.pedidos""", Secretaria);

        Assert.Equal(["cantidad de pedidos"], resultado.Columnas);
    }

    // ------------------------------------------------------------------ apoyo

    private EjecutorDeConsulta Ejecutor(int tope = 200)
    {
        var (basica, conDatosPersonales) = CadenasDeLectura();

        return new EjecutorDeConsulta(
            basica,
            conDatosPersonales,
            ClasificadorDeSensibilidad(),
            Options.Create(new OpcionesAsistente { TopeDeFilas = tope }));
    }

    private ConsultorDeAlcance Perfiles() => new(CadenasDeLectura().Basica);

    private Task<ResultadoDeConsulta> EjecutarAsync(
        string sql, Guid actor, int tope = 200, string? escrituraAdicional = null)
    {
        var consulta = escrituraAdicional is null
            ? sql
            // La escritura va DENTRO de la envoltura, en una CTE que el ejecutor no
            // inspecciona: es la forma de llegar al motor sin pasar por el
            // validador, que es justamente lo que hay que probar.
            : $"""
              WITH escritura AS ({escrituraAdicional})
              {sql}
              """;

        return Ejecutor(tope).EjecutarAsync(
            consulta, actor, false, TestContext.Current.CancellationToken);
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
