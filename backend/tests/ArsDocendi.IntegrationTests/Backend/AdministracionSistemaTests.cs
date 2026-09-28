using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class AdministracionSistemaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "admin_sistema")
{
    private static readonly Guid AdministradorSistema =
        Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Docente =
        Guid.Parse("a0000000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Estado_de_base_datos_requiere_permiso_y_devuelve_comprobacion_segura()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        using var docente = Cliente(host, Docente, "docente");

        using var respuesta = await administrador.GetAsync(
            "/api/administracion/sistema/estado", ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);

        Assert.Equal("disponible", estado.GetProperty("estado").GetString());
        Assert.True(estado.GetProperty("comprobadoEn").GetDateTimeOffset() > DateTimeOffset.MinValue);
        Assert.True(estado.GetProperty("duracionMs").GetDouble() >= 0);
        Assert.DoesNotContain("connection", estado.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var denegada = await docente.GetAsync("/api/administracion/sistema/estado", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
    }

    [Fact]
    public async Task Migracion_de_permisos_es_no_op_en_una_base_ya_actualizada_y_no_amplia_otros_roles()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var identidad = PostgresFixture.CrearIdentity(Cadena);
        var antes = await ContarPermisosNuevosAsync(ct);

        await identidad.Database.MigrateAsync(ct);

        var despues = await ContarPermisosNuevosAsync(ct);
        Assert.Equal((2L, 2L, 0L), antes);
        Assert.Equal(antes, despues);
    }

    [Fact]
    public async Task Auditoria_pagina_filtra_y_oculta_valores_personales_desconocidos()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var personaId = Guid.NewGuid();
        var rolId = Guid.NewGuid();
        var codigoRol = $"AUD-{rolId:N}";
        var documento = $"DOC-{personaId:N}";
        var cuil = $"CUIL-{personaId:N}";
        var telefono = $"TEL-{personaId:N}";
        var nombre = $"NOMBRE-{personaId:N}";
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            INSERT INTO identity.personas
                (id, documento, cuil, nombre, apellido, telefono)
            VALUES (@id, @documento, @cuil, @nombre, 'Apellido reservado', @telefono);
            """, conexion))
        {
            comando.Parameters.AddWithValue("id", personaId);
            comando.Parameters.AddWithValue("documento", documento);
            comando.Parameters.AddWithValue("cuil", cuil);
            comando.Parameters.AddWithValue("nombre", nombre);
            comando.Parameters.AddWithValue("telefono", telefono);
            await comando.ExecuteNonQueryAsync(ct);
        }
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            UPDATE identity.personas SET telefono = @telefono WHERE id = @id;
            """, conexion))
        {
            comando.Parameters.AddWithValue("id", personaId);
            comando.Parameters.AddWithValue("telefono", $"{telefono}-actualizado");
            await comando.ExecuteNonQueryAsync(ct);
        }
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, description, scope, es_sistema)
            VALUES (@id, @code, 'Rol reservado', 'Descripción no clasificada', 'global', FALSE);
            """, conexion))
        {
            comando.Parameters.AddWithValue("id", rolId);
            comando.Parameters.AddWithValue("code", codigoRol);
            await comando.ExecuteNonQueryAsync(ct);
        }

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        var url = $"/api/administracion/auditoria?schema=identity&tabla=personas&rowPk={personaId}&accion=INSERT&pagina=1&tamanoPagina=1";
        var cambiosAntesDeLeer = await ContarCambiosAuditoriaAsync(ct);
        using var respuesta = await administrador.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var elemento = pagina.GetProperty("elementos")[0];
        var cambios = elemento.GetProperty("cambios");
        var documentoCambio = Assert.Single(cambios.EnumerateArray(), cambio =>
            cambio.GetProperty("campo").GetString() == "documento");

        Assert.Equal(1, pagina.GetProperty("pagina").GetInt32());
        Assert.Equal(1, pagina.GetProperty("tamanoPagina").GetInt32());
        Assert.Equal(1, pagina.GetProperty("total").GetInt64());
        Assert.Equal("INSERT", elemento.GetProperty("accion").GetString());
        Assert.True(documentoCambio.GetProperty("oculto").GetBoolean());
        Assert.Equal(JsonValueKind.Null, documentoCambio.GetProperty("valorNuevo").ValueKind);
        var cuerpo = pagina.GetRawText();
        Assert.DoesNotContain(documento, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(cuil, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(telefono, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(nombre, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("clientIp", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("old_row", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new_row", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cambiosAntesDeLeer, await ContarCambiosAuditoriaAsync(ct));

        using var ordenada = await administrador.GetAsync(
            $"/api/administracion/auditoria?schema=identity&tabla=personas&rowPk={personaId}&tamanoPagina=10", ct);
        var paginaOrdenada = await ordenada.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(2, paginaOrdenada.GetProperty("total").GetInt64());
        Assert.Equal("UPDATE", paginaOrdenada.GetProperty("elementos")[0].GetProperty("accion").GetString());

        using var docente = Cliente(host, Docente, "docente");
        using var denegada = await docente.GetAsync("/api/administracion/auditoria", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);

        using var rolRespuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?schema=identity&tabla=roles&rowPk={rolId}", ct);
        Assert.Equal(HttpStatusCode.OK, rolRespuesta.StatusCode);
        var rolPagina = await rolRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var rolEvento = rolPagina.GetProperty("elementos")[0];
        var rolCambios = rolEvento.GetProperty("cambios");
        var scopeCambio = Assert.Single(rolCambios.EnumerateArray(), cambio =>
            cambio.GetProperty("campo").GetString() == "scope");
        var descripcionCambio = Assert.Single(rolCambios.EnumerateArray(), cambio =>
            cambio.GetProperty("campo").GetString() == "description");
        Assert.Equal("global", scopeCambio.GetProperty("valorNuevo").GetString());
        Assert.True(descripcionCambio.GetProperty("oculto").GetBoolean());
        Assert.Equal(JsonValueKind.Null, descripcionCambio.GetProperty("valorNuevo").ValueKind);
        Assert.DoesNotContain("Rol reservado", rolPagina.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Descripción no clasificada", rolPagina.GetRawText(), StringComparison.Ordinal);

        using var sinResultados = await administrador.GetAsync(
            "/api/administracion/auditoria?schema=identity&tabla=personas&rowPk=sin-coincidencias", ct);
        var vacia = await sinResultados.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(0, vacia.GetProperty("total").GetInt64());
        Assert.Empty(vacia.GetProperty("elementos").EnumerateArray());
    }

    [Fact]
    public async Task Auditoria_rechaza_rango_invertido_y_paginacion_fuera_de_limite()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");

        using var rango = await administrador.GetAsync(
            "/api/administracion/auditoria?desde=2026-09-21T00%3A00%3A00Z&hasta=2026-09-20T00%3A00%3A00Z", ct);
        using var pagina = await administrador.GetAsync(
            "/api/administracion/auditoria?tamanoPagina=101", ct);

        Assert.Equal(HttpStatusCode.BadRequest, rango.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pagina.StatusCode);
    }

    [Fact]
    public async Task Auditoria_busca_actor_legible_sin_duplicar_paginacion_y_aplica_fallbacks()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        var cuentaSinPersona = Guid.NewGuid();
        var rowPkPrimero = $"auditoria-{Guid.NewGuid():N}-uno";
        var rowPkSegundo = $"auditoria-{Guid.NewGuid():N}-dos";
        var rowPkFallback = $"auditoria-{Guid.NewGuid():N}-fallback";
        var rowPkDesconocido = $"auditoria-{Guid.NewGuid():N}-desconocido";

        await InsertarCuentaSinPersonaAsync(cuentaSinPersona, ct);
        await InsertarEventoAuditoriaAsync("identity", "roles", rowPkPrimero, "UPDATE", AdministradorSistema, ct);
        await InsertarEventoAuditoriaAsync("identity", "roles", rowPkSegundo, "UPDATE", AdministradorSistema, ct);
        await InsertarEventoAuditoriaAsync("portal", "perfiles", rowPkFallback, "INSERT", cuentaSinPersona, ct);
        await InsertarEventoAuditoriaAsync("schema_futuro", "tabla_nueva", rowPkDesconocido, "DELETE", null, ct);

        using var primeraPaginaRespuesta = await administrador.GetAsync(
            "/api/administracion/auditoria?actor=Vidal&pagina=1&tamanoPagina=1", ct);
        var primeraPaginaCuerpo = await primeraPaginaRespuesta.Content.ReadAsStringAsync(ct);
        Assert.True(
            primeraPaginaRespuesta.StatusCode == HttpStatusCode.OK,
            $"La consulta de auditoría falló: {primeraPaginaCuerpo}");
        var primeraPagina = JsonSerializer.Deserialize<JsonElement>(primeraPaginaCuerpo);
        var primerEvento = Assert.Single(primeraPagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal(2, primeraPagina.GetProperty("total").GetInt64());
        Assert.Equal("Vidal, Ernesto", primerEvento.GetProperty("actor").GetString());
        Assert.Equal("Actualización", primerEvento.GetProperty("accionEtiqueta").GetString());
        Assert.Equal("Identidad", primerEvento.GetProperty("modulo").GetString());
        Assert.Equal("Actualización de rol · Ámbito", primerEvento.GetProperty("resumen").GetString());
        Assert.NotNull(primerEvento.GetProperty("requestId").GetString());
        var cuerpoPrimeraPagina = primeraPagina.GetRawText();
        Assert.DoesNotContain("sistemas@unlam.edu.ar", cuerpoPrimeraPagina, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@unlam.edu.ar", cuerpoPrimeraPagina, StringComparison.OrdinalIgnoreCase);

        using var segundaPaginaRespuesta = await administrador.GetAsync(
            "/api/administracion/auditoria?actor=Vidal&pagina=2&tamanoPagina=1", ct);
        var segundaPagina = await segundaPaginaRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var segundoEvento = Assert.Single(segundaPagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal(2, segundaPagina.GetProperty("total").GetInt64());
        Assert.NotEqual(
            primerEvento.GetProperty("id").GetInt64(),
            segundoEvento.GetProperty("id").GetInt64());

        using var fallbackRespuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?actor=Cuenta%20de%20respaldo&rowPk={rowPkFallback}", ct);
        var fallback = await fallbackRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoFallback = Assert.Single(fallback.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Cuenta de respaldo", eventoFallback.GetProperty("actor").GetString());
        Assert.Equal("Portal", eventoFallback.GetProperty("modulo").GetString());

        using var desconocidoRespuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?actor=no%20identificado&rowPk={rowPkDesconocido}", ct);
        var desconocido = await desconocidoRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoDesconocido = Assert.Single(desconocido.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Actor no identificado", eventoDesconocido.GetProperty("actor").GetString());
        Assert.Equal("Eliminación física", eventoDesconocido.GetProperty("accionEtiqueta").GetString());
        Assert.Equal("Schema futuro", eventoDesconocido.GetProperty("modulo").GetString());
        Assert.Equal("Eliminación física de tabla nueva · Ámbito", eventoDesconocido.GetProperty("resumen").GetString());
    }

    private sealed class FabricaAdministracion(string cadena) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        }
    }

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuarioId, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuarioId.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Join(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task<(long Permisos, long AsignacionesSysAdmin, long AsignacionesOtrosRoles)> ContarPermisosNuevosAsync(
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                (SELECT count(*) FROM identity.permisos
                 WHERE code IN ('sistema.estado.ver', 'auditoria.ver')),
                (SELECT count(*) FROM identity.rol_permisos rp
                 JOIN identity.permisos p ON p.id = rp.permiso_id
                 JOIN identity.roles r ON r.id = rp.rol_id
                 WHERE p.code IN ('sistema.estado.ver', 'auditoria.ver') AND r.code = 'sys_admin'),
                (SELECT count(*) FROM identity.rol_permisos rp
                 JOIN identity.permisos p ON p.id = rp.permiso_id
                 JOIN identity.roles r ON r.id = rp.rol_id
                 WHERE p.code IN ('sistema.estado.ver', 'auditoria.ver') AND r.code <> 'sys_admin');
            """;
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        await lector.ReadAsync(ct);
        return (lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2));
    }

    private async Task<long> ContarCambiosAuditoriaAsync(CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("SELECT count(*) FROM audit.change_log", conexion);
        return (long)(await comando.ExecuteScalarAsync(ct))!;
    }

    private async Task InsertarCuentaSinPersonaAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.users (id, azure_oid, upn, display_name, persona_id)
            VALUES (@id, @azureOid, @upn, 'Cuenta de respaldo', NULL);
            """, conexion);
        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("azureOid", Guid.NewGuid());
        comando.Parameters.AddWithValue("upn", $"{id:N}@prueba.invalid");
        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task InsertarEventoAuditoriaAsync(
        string schema,
        string tabla,
        string rowPk,
        string accion,
        Guid? actor,
        CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO audit.change_log
                (schema_name, table_name, row_pk, action, changed_columns, changed_by, request_id, changed_at)
            VALUES (@schema, @tabla, @rowPk, @accion, @columnas, @actor, @requestId, clock_timestamp());
            """, conexion);
        comando.Parameters.AddWithValue("schema", schema);
        comando.Parameters.AddWithValue("tabla", tabla);
        comando.Parameters.AddWithValue("rowPk", rowPk);
        comando.Parameters.AddWithValue("accion", accion);
        comando.Parameters.AddWithValue("columnas", new[] { "scope" });
        comando.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid)
        {
            Value = actor.HasValue ? actor.Value : DBNull.Value,
        });
        comando.Parameters.AddWithValue("requestId", $"request-{Guid.NewGuid():N}");
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Join(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
