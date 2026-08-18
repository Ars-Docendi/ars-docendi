using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
public sealed class DesignacionesPersistenciaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "designaciones")
{
    [Fact]
    public async Task Segundo_periodo_activo_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();
        await InsertarPeriodoAsync(conexion, Guid.NewGuid(), true);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarPeriodoAsync(conexion, Guid.NewGuid(), true));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("periodos_unico_activo", error.ConstraintName);
    }

    [Fact]
    public async Task Cargo_fuera_del_catalogo_es_rechazado_en_pedido_y_designacion()
    {
        await using var conexion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(conexion);
        var cargoInexistente = Guid.NewGuid();

        var pedido = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(conexion, """
            INSERT INTO designaciones.pedidos
                (numero, periodo_id, persona_id, materia_id, novedad, cargo_solicitado_id, horas)
            VALUES ('TEST-CARGO-P', @periodo, @persona, @materia, 'Alta', @cargo, 10)
            """, new NpgsqlParameter("periodo", datos.Periodo), new NpgsqlParameter("persona", datos.Persona),
            new NpgsqlParameter("materia", datos.Materia1), new NpgsqlParameter("cargo", cargoInexistente)));
        var designacion = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(conexion, """
            INSERT INTO designaciones.designaciones
                (persona_id, materia_id, cargo_id, horas, vigente_desde)
            VALUES (@persona, @materia, @cargo, 10, DATE '2026-01-01')
            """, new NpgsqlParameter("persona", datos.Persona), new NpgsqlParameter("materia", datos.Materia1),
            new NpgsqlParameter("cargo", cargoInexistente)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, pedido.SqlState);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, designacion.SqlState);
    }

    [Fact]
    public async Task Segundo_pedido_misma_persona_periodo_y_otra_materia_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(conexion);
        await InsertarPedidoAsync(conexion, "TEST-0001", datos, datos.Materia1, "borrador");

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertarPedidoAsync(conexion, "TEST-0002", datos, datos.Materia2, "borrador"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("pedidos_uno_por_docente_periodo", error.ConstraintName);
    }

    [Fact]
    public async Task Dos_pedidos_concurrentes_hacen_fallar_exactamente_uno()
    {
        await using var preparacion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(preparacion);

        var resultados = await Task.WhenAll(
            IntentarInsertarPedidoConcurrenteAsync("TEST-RACE-1", datos, datos.Materia1),
            IntentarInsertarPedidoConcurrenteAsync("TEST-RACE-2", datos, datos.Materia2));

        Assert.Equal(1, resultados.Count(e => e is null));
        var error = Assert.Single(resultados.OfType<PostgresException>());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("pedidos_uno_por_docente_periodo", error.ConstraintName);
    }

    [Fact]
    public async Task Pedido_rechazado_libera_el_cupo_del_periodo()
    {
        await using var conexion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(conexion);
        await InsertarPedidoAsync(conexion, "TEST-RECH-1", datos, datos.Materia1, "rechazado");

        await InsertarPedidoAsync(conexion, "TEST-RECH-2", datos, datos.Materia2, "borrador");

        Assert.Equal(2L, await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM designaciones.pedidos
            WHERE periodo_id = @periodo AND persona_id = @persona
            """, new NpgsqlParameter("periodo", datos.Periodo), new NpgsqlParameter("persona", datos.Persona)));
    }

    [Fact]
    public async Task Designacion_cerrada_no_bloquea_una_nueva_sin_solapamiento()
    {
        await using var conexion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(conexion);
        await InsertarDesignacionAsync(
            conexion, datos.Persona, datos.Materia1, null,
            new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1));

        await InsertarDesignacionAsync(
            conexion, datos.Persona, datos.Materia1, null,
            new DateOnly(2026, 1, 1), null);

        Assert.Equal(2L, await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM designaciones.designaciones
            WHERE persona_id = @persona AND materia_id = @materia
            """, new NpgsqlParameter("persona", datos.Persona), new NpgsqlParameter("materia", datos.Materia1)));
    }

    [Fact]
    public async Task Segunda_designacion_vigente_en_la_misma_materia_es_rechazada()
    {
        await using var conexion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(conexion);
        await InsertarDesignacionAsync(
            conexion, datos.Persona, datos.Materia1, null, new DateOnly(2025, 1, 1), null);

        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertarDesignacionAsync(
            conexion, datos.Persona, datos.Materia1, null, new DateOnly(2026, 1, 1), null));

        Assert.Equal(PostgresErrorCodes.ExclusionViolation, error.SqlState);
        Assert.Equal("designaciones_sin_solapamiento", error.ConstraintName);
    }

    [Fact]
    public async Task Origen_pedido_distingue_circuito_de_carga_administrativa()
    {
        await using var conexion = await AbrirConexionAsync();
        var datos = await PrepararDatosAsync(conexion);
        var pedido = await InsertarPedidoAsync(
            conexion, "TEST-ORIGEN", datos, datos.Materia1, "rechazado");

        await InsertarDesignacionAsync(
            conexion, datos.Persona, datos.Materia1, pedido,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1));
        await InsertarDesignacionAsync(
            conexion, datos.Persona, datos.Materia2, null,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1));

        Assert.Equal(1L, await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM designaciones.designaciones WHERE origen_pedido_id = @pedido
            """, new NpgsqlParameter("pedido", pedido)));
        Assert.Equal(1L, await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM designaciones.designaciones WHERE origen_pedido_id IS NULL
            """));
    }

    private async Task<PostgresException?> IntentarInsertarPedidoConcurrenteAsync(
        string numero, DatosPrueba datos, Guid materia)
    {
        await using var conexion = await AbrirConexionAsync();
        try
        {
            await InsertarPedidoAsync(conexion, numero, datos, materia, "borrador");
            return null;
        }
        catch (PostgresException error)
        {
            return error;
        }
    }

    private static async Task<DatosPrueba> PrepararDatosAsync(NpgsqlConnection conexion)
    {
        var carrera = Guid.NewGuid();
        var materia1 = Guid.NewGuid();
        var materia2 = Guid.NewGuid();
        var persona = Guid.NewGuid();
        var periodo = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.carreras (id, code, name) VALUES (@id, @code, 'Carrera test')
            """, new NpgsqlParameter("id", carrera), new NpgsqlParameter("code", $"C-{carrera:N}"));
        await EjecutarAsync(conexion, """
            INSERT INTO identity.materias (id, code, name, carrera_id) VALUES
                (@materia1, @codigo1, 'Materia uno', @carrera),
                (@materia2, @codigo2, 'Materia dos', @carrera)
            """, new NpgsqlParameter("materia1", materia1), new NpgsqlParameter("codigo1", $"M1-{materia1:N}"),
            new NpgsqlParameter("materia2", materia2), new NpgsqlParameter("codigo2", $"M2-{materia2:N}"), new NpgsqlParameter("carrera", carrera));
        await EjecutarAsync(conexion, """
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES (@id, @documento, 'Grace', 'Hopper')
            """, new NpgsqlParameter("id", persona), new NpgsqlParameter("documento", $"D-{persona:N}"));
        await InsertarPeriodoAsync(conexion, periodo, false);
        return new DatosPrueba(persona, carrera, materia1, materia2, periodo);
    }

    private static async Task InsertarPeriodoAsync(
        NpgsqlConnection conexion, Guid id, bool activo) => await EjecutarAsync(conexion, """
        INSERT INTO designaciones.periodos
            (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta, activo)
        VALUES
            (@id, @nombre, DATE '2026-01-01', DATE '2026-02-01',
             DATE '2026-03-01', DATE '2026-07-31', @activo)
        """, new NpgsqlParameter("id", id), new NpgsqlParameter("nombre", $"Periodo {id:N}"), new NpgsqlParameter("activo", activo));

    private static async Task<Guid> InsertarPedidoAsync(
        NpgsqlConnection conexion,
        string numero,
        DatosPrueba datos,
        Guid materia,
        string estado)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO designaciones.pedidos
                (id, numero, periodo_id, persona_id, materia_id, novedad, estado)
            VALUES (@id, @numero, @periodo, @persona, @materia, 'Sin novedad', @estado)
            """, new NpgsqlParameter("id", id), new NpgsqlParameter("numero", numero), new NpgsqlParameter("periodo", datos.Periodo),
            new NpgsqlParameter("persona", datos.Persona), new NpgsqlParameter("materia", materia), new NpgsqlParameter("estado", estado));
        return id;
    }

    private static async Task InsertarDesignacionAsync(
        NpgsqlConnection conexion,
        Guid persona,
        Guid materia,
        Guid? origen,
        DateOnly desde,
        DateOnly? hasta)
    {
        await EjecutarAsync(conexion, """
            INSERT INTO designaciones.designaciones
                (persona_id, materia_id, cargo_id, horas, vigente_desde, vigente_hasta, origen_pedido_id)
            VALUES
                (@persona, @materia, 'c3000000-0000-4000-8000-000000000004',
                 10, @desde, @hasta, @origen)
            """, new NpgsqlParameter("persona", persona), new NpgsqlParameter("materia", materia), new NpgsqlParameter("desde", desde),
            new NpgsqlParameter("hasta", (object?)hasta ?? DBNull.Value), new NpgsqlParameter("origen", (object?)origen ?? DBNull.Value));
    }

    private static async Task EjecutarAsync(
        NpgsqlConnection conexion, string sql, params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<T> EscalarAsync<T>(
        NpgsqlConnection conexion, string sql, params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    private sealed record DatosPrueba(
        Guid Persona,
        Guid Carrera,
        Guid Materia1,
        Guid Materia2,
        Guid Periodo);
}
