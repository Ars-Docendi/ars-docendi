using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class IntegridadTareasSqlTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "tareas_integridad_sql")
{
    private async Task EjecutarAsync(string sql, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string Insertar(Guid id) => $"""
        INSERT INTO tareas.tareas(id,titulo,fecha_inicio,fecha_fin,prioridad_id,tipo_id,estado_id,responsable_id,creado_por_id)
        SELECT '{id:D}','Sintética','2026-01-01','2026-12-31',
            (SELECT id FROM tareas.prioridades WHERE codigo='media'),
            (SELECT id FROM tareas.tipos_tarea WHERE codigo='administrativa'),
            (SELECT id FROM tareas.estados_tarea WHERE codigo='pendiente'),gen_random_uuid(),gen_random_uuid();
        """;

    [Fact]
    public async Task SQL_rechaza_ciclo_y_proyecto_distinto_al_padre_con_ids_generados()
    {
        var ct = TestContext.Current.CancellationToken;
        var raiz = Guid.NewGuid();
        var hija = Guid.NewGuid();
        var proyecto = Guid.NewGuid();
        var otroProyecto = Guid.NewGuid();
        await EjecutarAsync($"""
            INSERT INTO tareas.proyectos(id,nombre,fecha_inicio,fecha_fin,estado_id,responsable_id)
            SELECT '{proyecto:D}','P','2026-01-01','2026-12-31',id,gen_random_uuid()
            FROM tareas.estados_proyecto WHERE codigo='abierto';
            INSERT INTO tareas.proyectos(id,nombre,fecha_inicio,fecha_fin,estado_id,responsable_id)
            SELECT '{otroProyecto:D}','Q','2026-01-01','2026-12-31',id,gen_random_uuid()
            FROM tareas.estados_proyecto WHERE codigo='abierto';
            """ + Insertar(raiz) + $"""
            UPDATE tareas.tareas SET proyecto_id='{proyecto:D}' WHERE id='{raiz:D}';
            INSERT INTO tareas.tareas(id,titulo,fecha_inicio,fecha_fin,prioridad_id,tipo_id,estado_id,responsable_id,creado_por_id,proyecto_id,tarea_padre_id)
            SELECT '{hija:D}','Hija',fecha_inicio,fecha_fin,prioridad_id,tipo_id,estado_id,responsable_id,creado_por_id,proyecto_id,id
            FROM tareas.tareas WHERE id='{raiz:D}';
            """, ct);

        var ciclo = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
            $"UPDATE tareas.tareas SET tarea_padre_id='{hija:D}' WHERE id='{raiz:D}'", ct));
        Assert.Equal("23514", ciclo.SqlState);
        Assert.Equal("tareas_jerarquia_aciclica", ciclo.ConstraintName);

        var proyectoIncoherente = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
            $"UPDATE tareas.tareas SET proyecto_id='{otroProyecto:D}' WHERE id='{hija:D}'", ct));
        Assert.Equal("23514", proyectoIncoherente.SqlState);
        Assert.Equal("tareas_proyecto_heredado", proyectoIncoherente.ConstraintName);
    }

    [Fact]
    public async Task SQL_Resuelta_exige_solucion_usando_ID_del_catalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarAsync(Insertar(Guid.NewGuid()), ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync("""
            UPDATE tareas.tareas SET estado_id=(SELECT id FROM tareas.estados_tarea WHERE codigo='resuelta'), solucion='   '
            """, ct));
        Assert.Equal("23514", error.SqlState);
    }

    [Fact]
    public async Task SQL_protege_codigo_de_catalogo_referenciado()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarAsync(Insertar(Guid.NewGuid()), ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync("""
            UPDATE tareas.estados_tarea SET codigo='pendiente_renombrado'
            WHERE codigo='pendiente'
            """, ct));
        Assert.Equal("23514", error.SqlState);
        Assert.Contains("tareas-catalogo-referenciado", error.MessageText);
        await using var conexion = await AbrirConexionAsync();
        await using var consulta = new NpgsqlCommand("SELECT codigo FROM tareas.estados_tarea WHERE codigo='pendiente'", conexion);
        Assert.Equal("pendiente", await consulta.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task Dos_cambios_directos_concurrentes_de_jerarquia_no_confirman_ciclo()
    {
        var ct = TestContext.Current.CancellationToken;
        var primeroId = Guid.NewGuid();
        var segundoId = Guid.NewGuid();
        await EjecutarAsync(Insertar(primeroId) + $"""
            INSERT INTO tareas.tareas(id,titulo,fecha_inicio,fecha_fin,prioridad_id,tipo_id,estado_id,responsable_id,creado_por_id)
            SELECT '{segundoId:D}','Segunda raíz','2026-01-01','2026-12-31',
                prioridad_id,tipo_id,estado_id,responsable_id,creado_por_id FROM tareas.tareas
            WHERE id='{primeroId:D}';
            """, ct);
        await using var primero = await AbrirConexionAsync();
        await using var txPrimero = await primero.BeginTransactionAsync(ct);
        await using (var update = new NpgsqlCommand("UPDATE tareas.tareas SET tarea_padre_id=@padre WHERE id=@id", primero, txPrimero))
        {
            update.Parameters.AddWithValue("padre", segundoId);
            update.Parameters.AddWithValue("id", primeroId);
            await update.ExecuteNonQueryAsync(ct);
        }

        var segundo = Task.Run(async () =>
        {
            await using var conexion = await AbrirConexionAsync();
            await using var tx = await conexion.BeginTransactionAsync(ct);
            try
            {
                await using var update = new NpgsqlCommand("UPDATE tareas.tareas SET tarea_padre_id=@padre WHERE id=@id", conexion, tx);
                update.Parameters.AddWithValue("padre", primeroId);
                update.Parameters.AddWithValue("id", segundoId);
                await update.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
                return (Exception?)null;
            }
            catch (Exception error)
            {
                return error;
            }
        }, ct);
        await using (var observador = await AbrirConexionAsync())
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(30));
            while (true)
            {
                await using var comando = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'", observador);
                if (Convert.ToInt64(await comando.ExecuteScalarAsync(limite.Token)) >= 1) break;
                await Task.Delay(20, limite.Token);
            }
        }
        await txPrimero.CommitAsync(ct);
        var errorSegundo = await segundo;
        Assert.IsType<PostgresException>(errorSegundo);
        var postgres = (PostgresException)errorSegundo!;
        Assert.Equal("23514", postgres.SqlState);
        Assert.Contains("tareas_jerarquia_aciclica", postgres.ConstraintName);
        await using var verificar = await AbrirConexionAsync();
        await using var consulta = new NpgsqlCommand("""
            WITH RECURSIVE cadena AS (
                SELECT id,tarea_padre_id,ARRAY[id] camino,false ciclo FROM tareas.tareas WHERE id IN
                    (@primero,@segundo)
                UNION ALL
                SELECT t.id,t.tarea_padre_id,c.camino||t.id,t.id=ANY(c.camino)
                FROM cadena c JOIN tareas.tareas t ON t.id=c.tarea_padre_id WHERE NOT c.ciclo
            ) SELECT count(*) FROM cadena WHERE ciclo
            """, verificar);
        consulta.Parameters.AddWithValue("primero", primeroId);
        consulta.Parameters.AddWithValue("segundo", segundoId);
        Assert.Equal(0L, (long)(await consulta.ExecuteScalarAsync(ct))!);
    }

    [Fact]
    public async Task Numeracion_de_proyectos_y_tareas_avanza_con_secuencias()
    {
        var ct = TestContext.Current.CancellationToken;
        var proyecto1 = Guid.NewGuid();
        var proyecto2 = Guid.NewGuid();
        var tarea1 = Guid.NewGuid();
        var tarea2 = Guid.NewGuid();
        await EjecutarAsync($"""
            INSERT INTO tareas.proyectos(id,nombre,fecha_inicio,fecha_fin,estado_id,responsable_id)
            SELECT '{proyecto1:D}','Proyecto A','2026-01-01','2026-12-31',id,gen_random_uuid()
            FROM tareas.estados_proyecto WHERE codigo='abierto';
            INSERT INTO tareas.proyectos(id,nombre,fecha_inicio,fecha_fin,estado_id,responsable_id)
            SELECT '{proyecto2:D}','Proyecto B','2026-01-01','2026-12-31',id,gen_random_uuid()
            FROM tareas.estados_proyecto WHERE codigo='abierto';
            """ + Insertar(tarea1) + Insertar(tarea2), ct);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT
                (SELECT numero FROM tareas.proyectos WHERE id=@proyecto1),
                (SELECT numero FROM tareas.proyectos WHERE id=@proyecto2),
                (SELECT numero FROM tareas.tareas WHERE id=@tarea1),
                (SELECT numero FROM tareas.tareas WHERE id=@tarea2)
            """, conexion);
        comando.Parameters.AddWithValue("proyecto1", proyecto1);
        comando.Parameters.AddWithValue("proyecto2", proyecto2);
        comando.Parameters.AddWithValue("tarea1", tarea1);
        comando.Parameters.AddWithValue("tarea2", tarea2);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        Assert.True(await lector.ReadAsync(ct));
        Assert.True(lector.GetInt32(1) > lector.GetInt32(0));
        Assert.True(lector.GetInt32(3) > lector.GetInt32(2));
    }

    [Fact]
    public async Task Todas_las_tablas_de_negocio_tienen_trigger_de_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT c.relname
            FROM pg_trigger t
            JOIN pg_class c ON c.oid=t.tgrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='tareas' AND NOT t.tgisinternal AND t.tgname LIKE 'trg_%_audit_log'
            ORDER BY c.relname
            """, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var tablas = new List<string>();
        while (await lector.ReadAsync(ct)) tablas.Add(lector.GetString(0));

        Assert.Equal(
            new[] { "proyectos", "tarea_comentarios", "tarea_historial", "tarea_relaciones", "tareas" },
            tablas);
    }

    [Fact]
    public async Task Snapshots_de_auditoria_de_Tareas_omiten_campos_libres()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        var casos = new (string Tabla, string Fila)[]
        {
            ("proyectos", """{"id":"1","numero":1,"nombre":"SENSITIVE_SENTINEL","descripcion":"SENSITIVE_SENTINEL","fecha_inicio":"2026-01-01","fecha_fin":"2026-12-31","estado_id":1,"responsable_id":"00000000-0000-0000-0000-000000000001","created_at":"2026-01-01T00:00:00Z"}"""),
            ("tareas", """{"id":"1","numero":1,"titulo":"SENSITIVE_SENTINEL","descripcion":"SENSITIVE_SENTINEL","solucion":"SENSITIVE_SENTINEL","fecha_inicio":"2026-01-01","fecha_fin":"2026-12-31","prioridad_id":1,"tipo_id":1,"estado_id":1,"porcentaje_avance":0,"responsable_id":"00000000-0000-0000-0000-000000000001","creado_por_id":"00000000-0000-0000-0000-000000000001","proyecto_id":null,"tarea_padre_id":null,"created_at":"2026-01-01T00:00:00Z"}"""),
            ("tarea_relaciones", """{"tarea_id":"1","relacionada_id":"2","nota":"SENSITIVE_SENTINEL","created_at":"2026-01-01T00:00:00Z"}"""),
            ("tarea_comentarios", """{"id":"1","tarea_id":"1","autor_id":"00000000-0000-0000-0000-000000000001","autor_rol":"docente","texto":"SENSITIVE_SENTINEL","created_at":"2026-01-01T00:00:00Z"}"""),
            ("tarea_historial", """{"id":"1","tarea_id":"1","accion":"editar","actor_id":"00000000-0000-0000-0000-000000000001","actor_rol":"docente","estado_id":1,"detalle":"SENSITIVE_SENTINEL","created_at":"2026-01-01T00:00:00Z"}"""),
        };

        foreach (var (tabla, fila) in casos)
        {
            await using var comando = new NpgsqlCommand(
                "SELECT audit.safe_snapshot('tareas', @tabla, @fila::jsonb)::text", conexion);
            comando.Parameters.AddWithValue("tabla", tabla);
            comando.Parameters.AddWithValue("fila", fila);
            var snapshot = (string)(await comando.ExecuteScalarAsync(ct))!;
            Assert.DoesNotContain("SENSITIVE_SENTINEL", snapshot, StringComparison.Ordinal);
        }
    }
}
