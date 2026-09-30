using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class IntegridadAuditoriaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "integridad_auditoria")
{
    [Fact]
    public async Task Migracion_instala_cursor_y_mantiene_legacy_sin_reescribirlo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT to_regclass('audit.seal_cursor') IS NOT NULL,
                   to_regclass('audit.seal_batches') IS NOT NULL,
                   EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'audit' AND table_name = 'change_log'
                         AND column_name = 'seal_seq'
                   ),
                   NOT EXISTS (
                       SELECT 1 FROM audit.change_log WHERE seal_seq IS NOT NULL
                   )
            """, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        Assert.True(await lector.ReadAsync(ct));
        Assert.True(lector.GetBoolean(0));
        Assert.True(lector.GetBoolean(1));
        Assert.True(lector.GetBoolean(2));
        Assert.True(lector.GetBoolean(3));
    }

    [Fact]
    public async Task Reaplicar_migraciones_no_duplica_baseline_ni_rompe_cursor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        await db.Database.MigrateAsync(ct);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT count(*) FROM audit.seal_baseline WHERE id = 1
            """, conexion);
        Assert.Equal(1L, (long)(await comando.ExecuteScalarAsync(ct))!);
    }

    [Fact]
    public async Task Evento_auditado_recibe_cursor_monotono_y_snapshot_sin_identificadores_extra()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = $"sellado-{Guid.NewGuid():N}";
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (@id, @code, 'Prueba sellado', 'global', FALSE, TRUE)
            """, conexion))
        {
            insertar.Parameters.AddWithValue("id", Guid.NewGuid());
            insertar.Parameters.AddWithValue("code", codigo);
            await insertar.ExecuteNonQueryAsync(ct);
        }

        await using var consulta = new NpgsqlCommand("""
            SELECT seal_seq, new_row ? 'seal_seq'
              FROM audit.change_log
             WHERE schema_name = 'identity' AND table_name = 'roles' AND row_pk = @codigo
            """, conexion);
        consulta.Parameters.AddWithValue("codigo", (await ObtenerIdRolAsync(conexion, codigo, ct)).ToString());
        await using var lector = await consulta.ExecuteReaderAsync(ct);
        Assert.True(await lector.ReadAsync(ct));
        Assert.True(lector.GetInt64(0) > 0);
        Assert.False(lector.GetBoolean(1));
    }

    [Fact]
    public async Task Escritor_legacy_que_omite_la_columna_recibe_secuencia_por_default()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO audit.change_log
                (schema_name, table_name, row_pk, action, old_row, new_row)
            VALUES ('integration', 'legacy_writer', 'row-1', 'INSERT', NULL, '{"id":"row-1"}'::jsonb)
            RETURNING seal_seq
            """, conexion);

        Assert.True((long)(await comando.ExecuteScalarAsync(ct))! > 0);
    }

    [Fact]
    public async Task Cincuenta_escritores_concurrentes_conservan_cursors_contiguos()
    {
        var ct = TestContext.Current.CancellationToken;
        var escrituras = Enumerable.Range(0, 50).Select(async indice =>
        {
            await using var conexion = await AbrirConexionAsync();
            await using var comando = new NpgsqlCommand("""
                INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
                VALUES (@id, @code, 'Carga concurrente', 'global', FALSE, TRUE)
                """, conexion);
            comando.Parameters.AddWithValue("id", Guid.NewGuid());
            comando.Parameters.AddWithValue("code", $"cursor-carga-{indice}-{Guid.NewGuid():N}");
            await comando.ExecuteNonQueryAsync(ct);
        });
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(escrituras);
        cronometro.Stop();
        var resultadoBenchmark = $"AUDIT_SEAL_BENCHMARK writers=50 elapsed_ms={cronometro.Elapsed.TotalMilliseconds:F1}";
        Console.WriteLine(resultadoBenchmark);
        var rutaBenchmark = Environment.GetEnvironmentVariable("AUDIT_SEAL_BENCHMARK_PATH");
        if (!string.IsNullOrWhiteSpace(rutaBenchmark))
            await File.WriteAllTextAsync(rutaBenchmark, resultadoBenchmark + Environment.NewLine, ct);

        await using var consultaConexion = await AbrirConexionAsync();
        await using var consulta = new NpgsqlCommand("""
            SELECT count(*), min(seal_seq), max(seal_seq)
              FROM audit.change_log WHERE table_name = 'roles' AND seal_seq IS NOT NULL
            """, consultaConexion);
        await using var lector = await consulta.ExecuteReaderAsync(ct);
        Assert.True(await lector.ReadAsync(ct));
        Assert.Equal(50L, lector.GetInt64(0));
        Assert.Equal(1L, lector.GetInt64(1));
        Assert.Equal(50L, lector.GetInt64(2));
    }

    [Fact]
    public async Task Preparador_persiste_lote_reintentable_sin_exponer_datos_de_fila()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("preparador-test-uno", ct);
        await AgregarEventoAsync("preparador-test-dos", ct);

        var primero = await preparador.PrepararSiguienteAsync("test", 1, ct);
        var reintento = await preparador.PrepararSiguienteAsync("test", 1, ct);

        Assert.NotNull(primero.Lote);
        Assert.Equal(primero.Lote.Manifiesto, reintento.Lote!.Manifiesto);
        Assert.Equal(1L, primero.Lote.PrimeraSecuencia);
        Assert.Equal(1L, primero.Lote.UltimaSecuencia);
        var textoManifiesto = System.Text.Encoding.UTF8.GetString(primero.Lote.Manifiesto);
        Assert.DoesNotContain("preparador-test-uno", textoManifiesto);
        Assert.DoesNotContain("row_pk", textoManifiesto);

        var segundo = await preparador.PrepararSiguienteAsync("test", 1, ct);
        Assert.Equal(primero.Lote.Id, segundo.Lote!.Id);
    }

    [Fact]
    public async Task Preparador_detecta_alteracion_de_evento_antes_de_reintentar_publicacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("alterar-pendiente", ct);
        var lote = await preparador.PrepararSiguienteAsync("test", 1, ct);
        Assert.NotNull(lote.Lote);

        await using (var conexion = await AbrirConexionAsync())
        await using (var modificar = new NpgsqlCommand("""
            UPDATE audit.change_log
               SET new_row = '{"id":"modificado"}'::jsonb
             WHERE seal_seq = @secuencia
            """, conexion))
        {
            modificar.Parameters.AddWithValue("secuencia", lote.Lote.PrimeraSecuencia);
            await modificar.ExecuteNonQueryAsync(ct);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            preparador.PrepararSiguienteAsync("test", 1, ct));
    }

    [Fact]
    public async Task Verificador_distingue_hash_local_de_custodia_externa_y_detecta_alteracion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        var verificador = new VerificadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("verificador-test", ct);
        await preparador.PrepararSiguienteAsync("test", 10, ct);

        var estadoInicial = await verificador.VerificarAsync("test", ct);
        Assert.True(estadoInicial.HashesLocalesValidos);
        Assert.False(estadoInicial.DobleCustodiaConfirmada);
        Assert.Equal(1L, estadoInicial.UltimaSecuenciaVerificada);

        await using (var conexion = await AbrirConexionAsync())
        await using (var modificar = new NpgsqlCommand("""
            UPDATE audit.change_log SET new_row = '{"id":"alterado"}'::jsonb
             WHERE seal_seq = 1
            """, conexion))
        {
            await modificar.ExecuteNonQueryAsync(ct);
        }

        var estadoAlterado = await verificador.VerificarAsync("test", ct);
        Assert.False(estadoAlterado.HashesLocalesValidos);
        Assert.False(estadoAlterado.DobleCustodiaConfirmada);
        Assert.Equal(0L, estadoAlterado.UltimaSecuenciaVerificada);
    }

    [Fact]
    public async Task Verificador_detecta_evento_eliminado_sin_repararlo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        var verificador = new VerificadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("verificador-eliminacion", ct);
        var lote = await preparador.PrepararSiguienteAsync("test", 10, ct);
        Assert.NotNull(lote.Lote);

        await using (var conexion = await AbrirConexionAsync())
        await using (var eliminar = new NpgsqlCommand(
            "DELETE FROM audit.change_log WHERE seal_seq = @secuencia", conexion))
        {
            eliminar.Parameters.AddWithValue("secuencia", lote.Lote.PrimeraSecuencia);
            Assert.Equal(1, await eliminar.ExecuteNonQueryAsync(ct));
        }

        var estado = await verificador.VerificarAsync("test", ct);
        Assert.False(estado.HashesLocalesValidos);
        Assert.True(estado.HayEventosPendientesDeLote);
        Assert.Equal(0L, estado.UltimaSecuenciaVerificada);
        await using var consultaConexion = await AbrirConexionAsync();
        await using var consulta = new NpgsqlCommand(
            "SELECT count(*) FROM audit.change_log WHERE seal_seq = @secuencia", consultaConexion);
        consulta.Parameters.AddWithValue("secuencia", lote.Lote.PrimeraSecuencia);
        Assert.Equal(0L, (long)(await consulta.ExecuteScalarAsync(ct))!);
    }

    [Fact]
    public async Task Verificador_detecta_reordenamiento_de_secuencia()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        var verificador = new VerificadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("verificador-reordenamiento", ct);
        var lote = await preparador.PrepararSiguienteAsync("test", 10, ct);
        Assert.NotNull(lote.Lote);

        await using (var conexion = await AbrirConexionAsync())
        await using (var modificar = new NpgsqlCommand("""
            UPDATE audit.change_log SET seal_seq = @secuenciaNueva WHERE seal_seq = @secuencia
            """, conexion))
        {
            modificar.Parameters.AddWithValue("secuencia", lote.Lote.PrimeraSecuencia);
            modificar.Parameters.AddWithValue("secuenciaNueva", lote.Lote.PrimeraSecuencia + 1);
            Assert.Equal(1, await modificar.ExecuteNonQueryAsync(ct));
        }

        var estado = await verificador.VerificarAsync("test", ct);
        Assert.False(estado.HashesLocalesValidos);
        Assert.True(estado.HayEventosPendientesDeLote);
        Assert.Equal(0L, estado.UltimaSecuenciaVerificada);
    }

    [Fact]
    public async Task Verificador_detecta_evento_duplicado_aun_si_se_elude_el_indice_unico()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        var verificador = new VerificadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("verificador-duplicado", ct);
        var lote = await preparador.PrepararSiguienteAsync("test", 10, ct);
        Assert.NotNull(lote.Lote);

        await using (var conexion = await AbrirConexionAsync())
        {
            await using (var quitarIndice = new NpgsqlCommand(
                "DROP INDEX audit.change_log_seal_seq_idx", conexion))
                await quitarIndice.ExecuteNonQueryAsync(ct);
            await using var duplicar = new NpgsqlCommand("""
                INSERT INTO audit.change_log
                    (schema_name, table_name, row_pk, action, old_row, new_row,
                     changed_columns, changed_by, changed_at, request_id, client_ip, seal_seq)
                SELECT schema_name, table_name, row_pk, action, old_row, new_row,
                       changed_columns, changed_by, changed_at, request_id, client_ip, seal_seq
                  FROM audit.change_log
                 WHERE seal_seq = @secuencia
                """, conexion);
            duplicar.Parameters.AddWithValue("secuencia", lote.Lote.PrimeraSecuencia);
            Assert.Equal(1, await duplicar.ExecuteNonQueryAsync(ct));
        }

        var estado = await verificador.VerificarAsync("test", ct);
        Assert.False(estado.HashesLocalesValidos);
        Assert.Equal(0L, estado.UltimaSecuenciaVerificada);
    }

    [Fact]
    public async Task Verificador_informa_sello_retrocedido_como_pendiente()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(Cadena);
        var preparador = new PreparadorLotesAuditoria(dataSource);
        var verificador = new VerificadorLotesAuditoria(dataSource);
        await AgregarEventoAsync("verificador-retroceso", ct);
        var preparado = await preparador.PrepararSiguienteAsync("test", 10, ct);
        Assert.NotNull(preparado.Lote);

        await using (var conexion = await AbrirConexionAsync())
        await using (var eliminar = new NpgsqlCommand(
            "DELETE FROM audit.seal_batches WHERE id = @id", conexion))
        {
            eliminar.Parameters.AddWithValue("id", preparado.Lote.Id);
            Assert.Equal(1, await eliminar.ExecuteNonQueryAsync(ct));
        }

        var estado = await verificador.VerificarAsync("test", ct);
        Assert.True(estado.HashesLocalesValidos);
        Assert.True(estado.HayEventosPendientesDeLote);
        Assert.Contains(estado.Observaciones, observacion => observacion.Contains("pendientes de lote", StringComparison.Ordinal));
        Assert.False(estado.DobleCustodiaConfirmada);
        Assert.Equal(0L, estado.UltimaSecuenciaVerificada);
    }

    private async Task AgregarEventoAsync(string code, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (@id, @code, 'Preparador de lote', 'global', FALSE, TRUE)
            """, conexion);
        comando.Parameters.AddWithValue("id", Guid.NewGuid());
        comando.Parameters.AddWithValue("code", code);
        await comando.ExecuteNonQueryAsync(ct);
    }

    [Fact]
    public async Task Rollback_de_escritura_no_consumo_cursor_y_escrituras_serializan_hasta_commit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexionUno = await AbrirConexionAsync();
        await using var conexionDos = await AbrirConexionAsync();
        var codigoUno = $"sellado-{Guid.NewGuid():N}";
        var codigoDos = $"sellado-{Guid.NewGuid():N}";
        await using var transaccionUno = await conexionUno.BeginTransactionAsync(ct);
        await InsertarRolAsync(conexionUno, transaccionUno, codigoUno, ct);
        var secuenciaUno = await ObtenerSecuenciaAsync(conexionUno, transaccionUno, codigoUno, ct);

        await using var transaccionDos = await conexionDos.BeginTransactionAsync(ct);
        var tareaSegunda = InsertarRolAsync(conexionDos, transaccionDos, codigoDos, ct);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
            Assert.False(tareaSegunda.IsCompleted);
            await transaccionUno.RollbackAsync(ct);
            await tareaSegunda;
            var secuenciaDos = await ObtenerSecuenciaAsync(conexionDos, transaccionDos, codigoDos, ct);
            Assert.Equal(secuenciaUno, secuenciaDos);
            await transaccionDos.CommitAsync(ct);
        }
        finally
        {
            if (transaccionUno.Connection is not null)
            {
                await transaccionUno.DisposeAsync();
            }
        }
    }

    private static async Task InsertarRolAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        string codigo,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, es_sistema, is_active)
            VALUES (@id, @code, 'Prueba sellado', 'global', FALSE, TRUE)
            """, conexion, transaccion);
        comando.Parameters.AddWithValue("id", Guid.NewGuid());
        comando.Parameters.AddWithValue("code", codigo);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> ObtenerSecuenciaAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        string codigo,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT al.seal_seq
              FROM audit.change_log al
              JOIN identity.roles r ON al.row_pk = r.id::text
             WHERE al.table_name = 'roles' AND r.code = @code
            """, conexion, transaccion);
        comando.Parameters.AddWithValue("code", codigo);
        return (long)(await comando.ExecuteScalarAsync(ct))!;
    }

    private static async Task<Guid> ObtenerIdRolAsync(
        NpgsqlConnection conexion,
        string codigo,
        CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT id FROM identity.roles WHERE code = @code", conexion);
        comando.Parameters.AddWithValue("code", codigo);
        return (Guid)(await comando.ExecuteScalarAsync(ct))!;
    }
}
