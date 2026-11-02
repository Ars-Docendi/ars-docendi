using ArsDocendi.Host.Administracion;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class AuditoriaPrecisaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "auditoria_precisa")
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private async Task Sql(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(Ct);
    }
    private async Task<T> Escalar<T>(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (T)(await comando.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task Filtro_preciso_recupera_solo_legacy_completo_no_ambiguo_y_declara_grupos()
    {
        await Sql("""
            INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,new_row) VALUES
              ('identity','rol_permisos','rol-legado','INSERT','{"rol_id":"rol-legado","permiso_id":"permiso-uno"}'),
              ('identity','rol_permisos','rol-legado','DELETE',NULL);
            INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,old_row,new_row) VALUES
              ('identity','rol_permisos','rol-legado','UPDATE','{"rol_id":"rol-legado","permiso_id":"permiso-dos"}',
              '{"rol_id":"rol-legado","permiso_id":"permiso-uno"}');
            """);
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = new ServicioAuditoria(new RepositorioAuditoria(db));
        var preciso = await servicio.ListarAsync(new ConsultaAuditoriaDto(Schema: "identity", Tabla: "rol_permisos",
            RowPk: "{\"permiso_id\":\"permiso-uno\",\"rol_id\":\"rol-legado\"}"), Ct);
        Assert.Equal(1, preciso.Total);
        Assert.Single(preciso.Elementos);
        var grupo = await servicio.ListarAsync(new ConsultaAuditoriaDto(Schema: "identity", Tabla: "rol_permisos", RowPk: "rol-legado"), Ct);
        Assert.Equal(3, grupo.Total);
        Assert.Equal(2, grupo.Elementos.Count(x => x.Resumen.Contains("Agrupación histórica", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Habilidad_e_interes_del_mismo_perfil_y_termino_se_distinguen()
    {
        await Sql("""
            INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES
              ('11111111-1111-4111-8111-111111111111','fixture-audit','Sintético','Fixture');
            INSERT INTO portal.perfiles(id,persona_id) VALUES
              ('22222222-2222-4222-8222-222222222222','11111111-1111-4111-8111-111111111111');
            INSERT INTO portal.habilidades(id,termino,termino_norm) VALUES
              ('33333333-3333-4333-8333-333333333333','Sintético','SINTETICO');
            INSERT INTO portal.docente_habilidades(perfil_id,habilidad_id,tipo) VALUES
              ('22222222-2222-4222-8222-222222222222','33333333-3333-4333-8333-333333333333','habilidad'),
              ('22222222-2222-4222-8222-222222222222','33333333-3333-4333-8333-333333333333','interes');
            """);
        Assert.Equal(2L, await Escalar<long>("SELECT count(DISTINCT row_pk) FROM audit.change_log WHERE table_name='docente_habilidades'"));
        Assert.True(await Escalar<bool>("SELECT bool_and(row_pk_format='composite-v1' AND row_key ?& ARRAY['perfil_id','habilidad_id','tipo']) FROM audit.change_log WHERE table_name='docente_habilidades'"));
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var pagina = await new RepositorioAuditoria(db).ListarAsync(new ConsultaAuditoriaDto(Schema: "portal", Tabla: "docente_habilidades",
            RowPk: "{\"tipo\":\"interes\",\"perfil_id\":\"22222222-2222-4222-8222-222222222222\",\"habilidad_id\":\"33333333-3333-4333-8333-333333333333\"}"), Ct);
        Assert.Equal(1, pagina.Total);
        Assert.True(await Escalar<bool>("""
            SELECT created_at IS NOT NULL AND updated_at=created_at AND deleted_at IS NULL AND key_format='precise'
            FROM audit.row_history_precise('portal','docente_habilidades',
              '{"tipo":"interes","perfil_id":"22222222-2222-4222-8222-222222222222","habilidad_id":"33333333-3333-4333-8333-333333333333"}')
            """));
        Assert.Equal("legacy-group", await Escalar<string>("SELECT key_format FROM audit.row_history_group('portal','docente_habilidades','grupo-ambiguo')"));
    }

    [Fact]
    public async Task Captura_no_duplica_pii_secretos_ni_valores_sin_clasificar()
    {
        await Sql("""
            INSERT INTO identity.personas(id,documento,nombre,apellido,telefono)
              VALUES ('11111111-1111-4111-8111-111111111111','documento-sintetico','Nombre sintético','Apellido sintético','telefono-sintetico');
            UPDATE identity.personas SET telefono='telefono-nuevo' WHERE id='11111111-1111-4111-8111-111111111111';
            CREATE TABLE public.auditoria_fixture(id uuid PRIMARY KEY,token text,code text,nuevo_secreto text);
            SELECT audit.attach('public.auditoria_fixture');
            INSERT INTO public.auditoria_fixture VALUES(gen_random_uuid(),'token-sintetico','no-clasificado','secreto-sintetico');
            """);
        Assert.False(await Escalar<bool>("""
            SELECT EXISTS(SELECT 1 FROM audit.change_log WHERE table_name='personas'
                AND (new_row ?| ARRAY['documento','nombre','apellido','telefono'] OR old_row ?| ARRAY['documento','nombre','apellido','telefono']))
            """));
        Assert.True(await Escalar<bool>("""
            SELECT changed_columns @> ARRAY['telefono'] FROM audit.change_log
            WHERE table_name='personas' AND action='UPDATE' ORDER BY id DESC LIMIT 1
            """));
        Assert.Equal("{}", await Escalar<string>("SELECT new_row::text FROM audit.change_log WHERE table_name='auditoria_fixture'"));
    }

    [Fact]
    public async Task Cambios_reales_en_una_transaccion_conservan_actor_y_orden_aun_con_fecha_igual()
    {
        await Sql("""
            INSERT INTO identity.users(id,upn,display_name) VALUES
              ('11111111-1111-4111-8111-111111111111','actor-uno@example.test','Uno'),
              ('22222222-2222-4222-8222-222222222222','actor-dos@example.test','Dos'),
              ('33333333-3333-4333-8333-333333333333','actor-tres@example.test','Tres');
            BEGIN;
            SELECT set_config('app.current_user_id','11111111-1111-4111-8111-111111111111',true);
            INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES
              ('44444444-4444-4444-8444-444444444444','persona-transaccion','Sintético','Fixture');
            SELECT set_config('app.current_user_id','22222222-2222-4222-8222-222222222222',true);
            UPDATE identity.personas SET telefono='telefono-uno' WHERE id='44444444-4444-4444-8444-444444444444';
            SELECT set_config('app.current_user_id','33333333-3333-4333-8333-333333333333',true);
            UPDATE identity.personas SET telefono='telefono-dos' WHERE id='44444444-4444-4444-8444-444444444444';
            COMMIT;
            """);
        Assert.True(await Escalar<bool>("""
            SELECT created_at=updated_at AND created_by='11111111-1111-4111-8111-111111111111'::uuid
              AND updated_by='33333333-3333-4333-8333-333333333333'::uuid AND deleted_at IS NULL
            FROM audit.row_history('identity','personas','44444444-4444-4444-8444-444444444444')
            """));
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var pagina = await new RepositorioAuditoria(db).ListarAsync(new ConsultaAuditoriaDto(
            Schema: "identity", Tabla: "personas", RowPk: "44444444-4444-4444-8444-444444444444"), Ct);
        Assert.Equal(3, pagina.Total);
        Assert.Equal(new[] { "UPDATE", "UPDATE", "INSERT" }, pagina.Registros.Select(x => x.Registro.Accion));
        Assert.True(pagina.Registros[0].Registro.Id > pagina.Registros[1].Registro.Id);
    }

    [Fact]
    public async Task Historia_desempata_por_id_y_toma_fecha_y_actor_del_mismo_evento()
    {
        await Sql("""
            INSERT INTO identity.users(id,upn,display_name) VALUES
              ('11111111-1111-4111-8111-111111111111','uno@example.test','Uno'),
              ('22222222-2222-4222-8222-222222222222','dos@example.test','Dos'),
              ('33333333-3333-4333-8333-333333333333','tres@example.test','Tres');
            INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,changed_at,changed_by) VALUES
              ('identity','fixture','historia','INSERT','2026-10-13T00:00:00Z','11111111-1111-4111-8111-111111111111'),
              ('identity','fixture','historia','UPDATE','2026-10-13T00:00:00Z','22222222-2222-4222-8222-222222222222'),
              ('identity','fixture','historia','UPDATE','2026-10-13T00:00:00Z','33333333-3333-4333-8333-333333333333');
            """);
        Assert.Equal(Guid.Parse("33333333-3333-4333-8333-333333333333"), await Escalar<Guid>(
            "SELECT updated_by FROM audit.row_history('identity','fixture','historia')"));
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), await Escalar<Guid>(
            "SELECT created_by FROM audit.row_history('identity','fixture','historia')"));
        await Sql("""
            INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,changed_at,changed_by) VALUES
              ('identity','fixture','historia','DELETE','2026-10-13T00:00:00Z','22222222-2222-4222-8222-222222222222');
            """);
        Assert.True(await Escalar<bool>("""
            SELECT updated_by=deleted_by AND updated_at=deleted_at
              AND deleted_by='22222222-2222-4222-8222-222222222222'::uuid
            FROM audit.row_history('identity','fixture','historia')
            """));
    }

    [Fact]
    public async Task Permisos_del_mismo_rol_tienen_clave_precisa_y_filtro_independiente()
    {
        await Sql("""
            INSERT INTO identity.roles(id,code,name,scope) VALUES
              ('11111111-1111-4111-8111-111111111111','audit_fixture','Auditoría fixture','global');
            INSERT INTO identity.rol_permisos(rol_id,permiso_id)
              SELECT '11111111-1111-4111-8111-111111111111',id FROM identity.permisos ORDER BY id LIMIT 2;
            """);
        Assert.Equal(2L, await Escalar<long>("""
            SELECT count(DISTINCT row_pk) FROM audit.change_log WHERE table_name='rol_permisos'
              AND new_row->>'rol_id'='11111111-1111-4111-8111-111111111111'
            """));
        var clave = await Escalar<string>("SELECT row_pk FROM audit.change_log WHERE table_name='rol_permisos' ORDER BY id DESC LIMIT 1");
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var pagina = await new RepositorioAuditoria(db).ListarAsync(new ConsultaAuditoriaDto(
            Schema: "identity", Tabla: "rol_permisos", RowPk: clave), Ct);
        Assert.Equal(1, pagina.Total);
        Assert.Single(pagina.Registros);
    }
}
