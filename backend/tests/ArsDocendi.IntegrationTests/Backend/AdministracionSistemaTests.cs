using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente.Contracts;
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
        // El seed no activó mantenimiento, así que sale "inactivo" — el caso
        // "activo" y el caso de falla del asistente tienen sus propios tests
        // (design.md D7, sistema-seccion-unificada).
        Assert.Equal("inactivo", estado.GetProperty("mantenimientoAsistente").GetString());
        Assert.DoesNotContain("connection", estado.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var denegada = await docente.GetAsync("/api/administracion/sistema/estado", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);
    }

    [Fact]
    public async Task Estado_de_base_datos_muestra_activo_cuando_el_asistente_esta_en_mantenimiento_sin_razon_ni_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand(
            """
            UPDATE asistente.modo_mantenimiento
               SET activo = true, razon = 'mantenimiento programado', actor_id = @actor
             WHERE id = 1
            """, conexion))
        {
            comando.Parameters.AddWithValue("actor", AdministradorSistema);
            await comando.ExecuteNonQueryAsync(ct);
        }

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");

        using var respuesta = await administrador.GetAsync("/api/administracion/sistema/estado", ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);

        Assert.Equal("activo", estado.GetProperty("mantenimientoAsistente").GetString());
        var cuerpo = estado.GetRawText();
        Assert.DoesNotContain("mantenimiento programado", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(AdministradorSistema.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("razon", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actor", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Estado_de_base_datos_degrada_a_desconocido_si_falla_la_consulta_de_mantenimiento()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new FabricaAdministracion(Cadena, servicios =>
            servicios.AddScoped<IConsultaDeMantenimiento, ConsultaDeMantenimientoQueFalla>());
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");

        using var respuesta = await administrador.GetAsync("/api/administracion/sistema/estado", ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);

        // La falla del asistente no tira abajo la comprobación de PostgreSQL:
        // las dos corren independientes (design.md D7).
        Assert.Equal("disponible", estado.GetProperty("estado").GetString());
        Assert.Equal("desconocido", estado.GetProperty("mantenimientoAsistente").GetString());
    }

    private sealed class ConsultaDeMantenimientoQueFalla : IConsultaDeMantenimiento
    {
        public Task<EstadoDeMantenimientoPublico> ConsultarAsync(CancellationToken ct) =>
            throw new InvalidOperationException("Falla simulada para el test.");
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
        var url = $"/api/administracion/auditoria?modulo=identity&tabla=personas&rowPk={personaId}&accion=INSERT&pagina=1&tamanoPagina=1";
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
            $"/api/administracion/auditoria?modulo=identity&tabla=personas&rowPk={personaId}&tamanoPagina=10", ct);
        var paginaOrdenada = await ordenada.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(2, paginaOrdenada.GetProperty("total").GetInt64());
        Assert.Equal("UPDATE", paginaOrdenada.GetProperty("elementos")[0].GetProperty("accion").GetString());

        using var docente = Cliente(host, Docente, "docente");
        using var denegada = await docente.GetAsync("/api/administracion/auditoria", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);

        using var rolRespuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=roles&rowPk={rolId}", ct);
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
            "/api/administracion/auditoria?modulo=identity&tabla=personas&rowPk=sin-coincidencias", ct);
        var vacia = await sinResultados.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(0, vacia.GetProperty("total").GetInt64());
        Assert.Empty(vacia.GetProperty("elementos").EnumerateArray());
    }

    [Fact]
    public async Task Auditoria_evento_de_cuenta_de_usuario_muestra_booleano_como_si_no_y_nombra_a_la_persona()
    {
        // sistema-seccion-unificada, design.md D5, «Humanized identity events»
        // (tarea 2.9): escenario «Boolean values read as Sí and No».
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var personaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        await InsertarPersonaAsync(personaId, "Paula", "Gómez", ct);
        await InsertarUsuarioAsync(usuarioId, personaId, "Cuenta de Paula", ct);
        await InsertarEventoAuditoriaAsync(
            "identity", "users", usuarioId.ToString(), "UPDATE", AdministradorSistema, ct,
            filaAnterior: """{"is_active":false}""", filaNueva: """{"is_active":true}""",
            columnasCambiadas: ["is_active"]);

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        using var respuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=users&rowPk={usuarioId}&accion=UPDATE", ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var elemento = Assert.Single(pagina.GetProperty("elementos").EnumerateArray());

        Assert.Equal("Cuenta de usuario de Paula Gómez", elemento.GetProperty("objeto").GetString());
        Assert.Equal(
            "Cuenta de usuario de Paula Gómez: Activo No → Sí", elemento.GetProperty("resumen").GetString());
        var cambio = Assert.Single(
            elemento.GetProperty("cambios").EnumerateArray(),
            c => c.GetProperty("campo").GetString() == "is_active");
        Assert.Equal("No", cambio.GetProperty("valorAnterior").GetString());
        Assert.Equal("Sí", cambio.GetProperty("valorNuevo").GetString());
    }

    [Fact]
    public async Task Auditoria_asignacion_de_rol_nombra_a_la_persona_y_al_rol_al_asignar_y_al_quitar()
    {
        // sistema-seccion-unificada, design.md D5, «Humanized identity events»
        // (tarea 2.9): escenario «Role assignment names the person and the role».
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var personaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        await InsertarPersonaAsync(personaId, "Julieta", "Acosta", ct);
        await InsertarUsuarioAsync(usuarioId, personaId, "Cuenta de Julieta", ct);
        // Rol de sistema «Docente», sembrado por la migración base (id fijo,
        // 002_identity_roles.sql) — no hace falta crear uno nuevo.
        var rolDocenteId = Guid.Parse("a1000000-0000-4000-8000-000000000001");
        var rowPkAsignacion = $"asignacion-{Guid.NewGuid():N}";
        var rowPkQuita = $"asignacion-{Guid.NewGuid():N}";
        var snapshot = $$"""{"user_id":"{{usuarioId}}","role_id":"{{rolDocenteId}}"}""";

        await InsertarEventoAuditoriaAsync(
            "identity", "user_roles", rowPkAsignacion, "INSERT", AdministradorSistema, ct,
            filaNueva: snapshot, sinColumnasCambiadas: true);
        await InsertarEventoAuditoriaAsync(
            "identity", "user_roles", rowPkQuita, "DELETE", AdministradorSistema, ct,
            filaAnterior: snapshot, sinColumnasCambiadas: true);

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");

        using var asignacion = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=user_roles&rowPk={rowPkAsignacion}", ct);
        var paginaAsignacion = await asignacion.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoAsignado = Assert.Single(paginaAsignacion.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Rol Docente asignado a Julieta Acosta", eventoAsignado.GetProperty("resumen").GetString());
        Assert.Equal("Roles de Julieta Acosta", eventoAsignado.GetProperty("objeto").GetString());

        using var quita = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=user_roles&rowPk={rowPkQuita}", ct);
        var paginaQuita = await quita.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoQuitado = Assert.Single(paginaQuita.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Rol Docente quitado a Julieta Acosta", eventoQuitado.GetProperty("resumen").GetString());
    }

    [Fact]
    public async Task Auditoria_busca_por_el_nombre_actual_del_sujeto_humano_sin_incluir_el_renombre()
    {
        // Gap encontrado por el chequeo headless: el objeto humanizado
        // (design.md D5, tarea 2.9) nombra al sujeto afectado —«Cuenta de
        // usuario de {nombre}», «Persona {nombre}», «Roles de {nombre}»— pero
        // el predicado `q` sólo conocía las etiquetas ESTÁTICAS, así que
        // "gustavo ruiz" no encontraba ninguno de esos tres eventos. La
        // extensión entra por IDs resueltos aparte, nunca por el nombre en sí
        // dentro del predicado SQL — sigue siendo "nunca por valor" (D4).
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var personaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        await InsertarPersonaAsync(personaId, "Gustavo", "Ruiz", ct); // dispara un INSERT real
        await InsertarUsuarioAsync(usuarioId, personaId, "Cuenta de Gustavo", ct); // dispara un INSERT real

        // Un renombre (mismo patrón que Auditoria_renombrar_una_persona_no_filtra_el_nombre_por_la_etiqueta):
        // el propio evento cambió nombre/apellido, así que NUNCA es un hit por
        // nombre aunque el nombre actual de la persona ya sea "Gustavo Ruiz".
        await InsertarEventoAuditoriaAsync(
            "identity", "personas", personaId.ToString(), "UPDATE", AdministradorSistema, ct,
            filaAnterior: """{"nombre":"Viejo"}""", filaNueva: """{"nombre":"Gustavo"}""",
            columnasCambiadas: ["nombre"]);

        var rolDocenteId = Guid.Parse("a1000000-0000-4000-8000-000000000001");
        var rowPkAsignacion = $"asignacion-{Guid.NewGuid():N}";
        await InsertarEventoAuditoriaAsync(
            "identity", "user_roles", rowPkAsignacion, "INSERT", AdministradorSistema, ct,
            filaNueva: $$"""{"user_id":"{{usuarioId}}","role_id":"{{rolDocenteId}}"}""",
            sinColumnasCambiadas: true);

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");

        foreach (var q in new[] { "gustavo ruiz", "Gustavo Ruiz", "gústavo rúiz" })
        {
            using var respuesta = await administrador.GetAsync(
                $"/api/administracion/auditoria?q={Uri.EscapeDataString(q)}&pagina=1&tamanoPagina=50", ct);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
            var elementos = pagina.GetProperty("elementos").EnumerateArray().ToArray();

            Assert.Equal(pagina.GetProperty("total").GetInt64(), elementos.Length);
            Assert.Contains(elementos, e =>
                e.GetProperty("tabla").GetString() == "personas"
                && e.GetProperty("rowPk").GetString() == personaId.ToString()
                && e.GetProperty("accion").GetString() == "INSERT");
            Assert.Contains(elementos, e =>
                e.GetProperty("tabla").GetString() == "users"
                && e.GetProperty("rowPk").GetString() == usuarioId.ToString());
            Assert.Contains(elementos, e =>
                e.GetProperty("tabla").GetString() == "user_roles"
                && e.GetProperty("rowPk").GetString() == rowPkAsignacion);
            Assert.DoesNotContain(elementos, e =>
                e.GetProperty("tabla").GetString() == "personas"
                && e.GetProperty("accion").GetString() == "UPDATE");
        }
    }

    [Fact]
    public async Task Auditoria_asignacion_de_rol_por_el_flujo_real_nombra_al_asignar_y_al_revocar()
    {
        // sistema-seccion-unificada, design.md D5, «Humanized identity events»
        // (tarea 2.9): NO se inserta el change_log a mano — se pasa por
        // ServicioUsuarios, el mismo camino que usa producción, para probar el
        // caso real de revocación (soft-delete vía UPDATE de deleted_at, no un
        // DELETE físico).
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var rolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");
        var rolAdministrativo = Guid.Parse("a1000000-0000-4000-8000-000000000006");
        var sufijo = Guid.NewGuid().ToString("N")[..10];

        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicioUsuarios = new ServicioUsuarios(db, new RepositorioUsuarios(db));
        var datos = new GuardarUsuarioDto(
            "Julieta", "Acosta", $"DOC{sufijo}", $"LEG{sufijo}", null,
            new DateOnly(1990, 1, 1), null, $"{sufijo}@prueba.invalid",
            [new GuardarAsignacionRolDto(rolSecretaria)]);

        var creado = await servicioUsuarios.CrearAsync(datos, ct);
        var idAsignacionSecretaria = creado.Membresias.Single(m => m.RolId == rolSecretaria).Id;

        await servicioUsuarios.EditarAsync(creado.Id, datos with
        {
            Version = creado.Version,
            Membresias = [new GuardarAsignacionRolDto(rolAdministrativo)],
        }, ct);

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");

        using var alta = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=user_roles&rowPk={idAsignacionSecretaria}&accion=INSERT",
            ct);
        var paginaAlta = await alta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoAlta = Assert.Single(paginaAlta.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Rol Secretaría Académica asignado a Julieta Acosta", eventoAlta.GetProperty("resumen").GetString());

        using var baja = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=user_roles&rowPk={idAsignacionSecretaria}&accion=UPDATE",
            ct);
        var paginaBaja = await baja.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoBaja = Assert.Single(paginaBaja.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Cambio", eventoBaja.GetProperty("accionEtiqueta").GetString());
        Assert.Equal(
            "Rol Secretaría Académica quitado a Julieta Acosta", eventoBaja.GetProperty("resumen").GetString());
    }

    [Fact]
    public async Task Auditoria_renombrar_una_persona_no_filtra_el_nombre_por_la_etiqueta()
    {
        // sistema-seccion-unificada, design.md D5, «Humanized identity events»
        // (tarea 2.9): escenario «Renaming a person does not leak the name
        // through the label» — ni el resumen NI la etiqueta de objeto pueden
        // nombrar a la persona cuando el propio evento cambió su nombre.
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var personaId = Guid.NewGuid();
        await InsertarPersonaAsync(personaId, "Nombre Actual", "Apellido Actual", ct);
        await InsertarEventoAuditoriaAsync(
            "identity", "personas", personaId.ToString(), "UPDATE", AdministradorSistema, ct,
            filaAnterior: """{"nombre":"Nombre Viejo"}""", filaNueva: """{"nombre":"Nombre Actual"}""",
            columnasCambiadas: ["nombre"]);

        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        using var respuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?modulo=identity&tabla=personas&rowPk={personaId}&accion=UPDATE", ct);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var elemento = Assert.Single(pagina.GetProperty("elementos").EnumerateArray());

        Assert.Equal("Persona", elemento.GetProperty("objeto").GetString());
        Assert.Equal("Cambio de persona · Nombre", elemento.GetProperty("resumen").GetString());
        var cuerpo = pagina.GetRawText();
        Assert.DoesNotContain("Nombre Viejo", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Nombre Actual", cuerpo, StringComparison.Ordinal);
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
    public async Task Auditoria_encuentra_alta_y_baja_por_clave_del_snapshot_pero_nunca_por_su_valor()
    {
        // design.md D4: un INSERT/DELETE real del trigger no lleva
        // changed_columns — la única evidencia de qué campos tenía la fila es
        // el snapshot, y la búsqueda tiene que alcanzarlo por CLAVE (`?|`),
        // nunca por el VALOR que esa clave guarda.
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new FabricaAdministracion(Cadena);
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        var rowPkAlta = $"auditoria-{Guid.NewGuid():N}-alta";
        var rowPkBaja = $"auditoria-{Guid.NewGuid():N}-baja";
        var valorAlta = $"VALOR-UNICO-{Guid.NewGuid():N}";
        var valorBaja = $"VALOR-UNICO-{Guid.NewGuid():N}";

        await InsertarEventoAuditoriaAsync(
            "identity", "personas", rowPkAlta, "INSERT", AdministradorSistema, ct,
            filaNueva: $$"""{"documento":"{{valorAlta}}"}""", sinColumnasCambiadas: true);
        await InsertarEventoAuditoriaAsync(
            "identity", "personas", rowPkBaja, "DELETE", AdministradorSistema, ct,
            filaAnterior: $$"""{"documento":"{{valorBaja}}"}""", sinColumnasCambiadas: true);

        using var porAlta = await administrador.GetAsync(
            $"/api/administracion/auditoria?q=documento&rowPk={rowPkAlta}", ct);
        var paginaAlta = await porAlta.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, paginaAlta.GetProperty("total").GetInt64());
        Assert.Equal("INSERT", paginaAlta.GetProperty("elementos")[0].GetProperty("accion").GetString());

        using var porBaja = await administrador.GetAsync(
            $"/api/administracion/auditoria?q=documento&rowPk={rowPkBaja}", ct);
        var paginaBaja = await porBaja.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, paginaBaja.GetProperty("total").GetInt64());
        Assert.Equal("DELETE", paginaBaja.GetProperty("elementos")[0].GetProperty("accion").GetString());

        using var porValorAlta = await administrador.GetAsync(
            $"/api/administracion/auditoria?q={valorAlta}", ct);
        var resultadoValorAlta = await porValorAlta.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(0, resultadoValorAlta.GetProperty("total").GetInt64());

        using var porValorBaja = await administrador.GetAsync(
            $"/api/administracion/auditoria?q={valorBaja}", ct);
        var resultadoValorBaja = await porValorBaja.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(0, resultadoValorBaja.GetProperty("total").GetInt64());
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
        await InsertarEventoAuditoriaAsync(
            "identity", "roles", rowPkPrimero, "UPDATE", AdministradorSistema, ct,
            filaAnterior: """{"scope":"local"}""", filaNueva: """{"scope":"global"}""");
        await InsertarEventoAuditoriaAsync(
            "identity", "roles", rowPkSegundo, "UPDATE", AdministradorSistema, ct,
            filaAnterior: """{"scope":"local"}""", filaNueva: """{"scope":"global"}""");
        await InsertarEventoAuditoriaAsync("portal", "perfiles", rowPkFallback, "INSERT", cuentaSinPersona, ct);
        await InsertarEventoAuditoriaAsync("schema_futuro", "tabla_nueva", rowPkDesconocido, "DELETE", null, ct);

        using var primeraPaginaRespuesta = await administrador.GetAsync(
            "/api/administracion/auditoria?q=Vidal&pagina=1&tamanoPagina=1", ct);
        var primeraPaginaCuerpo = await primeraPaginaRespuesta.Content.ReadAsStringAsync(ct);
        Assert.True(
            primeraPaginaRespuesta.StatusCode == HttpStatusCode.OK,
            $"La consulta de auditoría falló: {primeraPaginaCuerpo}");
        var primeraPagina = JsonSerializer.Deserialize<JsonElement>(primeraPaginaCuerpo);
        var primerEvento = Assert.Single(primeraPagina.GetProperty("elementos").EnumerateArray());
        // 5, no 2: además de los dos "Vidal" creados acá por ACTOR, "q" ahora
        // también encuentra por SUJETO (fix del gap de búsqueda) los eventos
        // propios de la cuenta/persona/asignaciones de rol sembradas para
        // AdministradorSistema ("Ernesto Vidal"). Los dos de acá siguen siendo
        // los más recientes (orden por changed_at descendente), así que
        // primerEvento/segundoEvento abajo siguen siendo estos dos.
        Assert.Equal(5, primeraPagina.GetProperty("total").GetInt64());
        Assert.Equal("Ernesto Vidal", primerEvento.GetProperty("actor").GetString());
        Assert.Equal("Cambio", primerEvento.GetProperty("accionEtiqueta").GetString());
        Assert.Equal("Identidad", primerEvento.GetProperty("modulo").GetString());
        Assert.Equal("Rol: Ámbito local → global", primerEvento.GetProperty("resumen").GetString());
        Assert.NotNull(primerEvento.GetProperty("requestId").GetString());
        var cuerpoPrimeraPagina = primeraPagina.GetRawText();
        Assert.DoesNotContain("sistemas@unlam.edu.ar", cuerpoPrimeraPagina, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@unlam.edu.ar", cuerpoPrimeraPagina, StringComparison.OrdinalIgnoreCase);

        using var segundaPaginaRespuesta = await administrador.GetAsync(
            "/api/administracion/auditoria?q=Vidal&pagina=2&tamanoPagina=1", ct);
        var segundaPagina = await segundaPaginaRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var segundoEvento = Assert.Single(segundaPagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal(5, segundaPagina.GetProperty("total").GetInt64());
        Assert.NotEqual(
            primerEvento.GetProperty("id").GetString(),
            segundoEvento.GetProperty("id").GetString());

        using var fallbackRespuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?q=Cuenta%20de%20respaldo&rowPk={rowPkFallback}", ct);
        var fallback = await fallbackRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoFallback = Assert.Single(fallback.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Cuenta de respaldo", eventoFallback.GetProperty("actor").GetString());
        Assert.Equal("Portal", eventoFallback.GetProperty("modulo").GetString());

        using var desconocidoRespuesta = await administrador.GetAsync(
            $"/api/administracion/auditoria?q=no%20identificado&rowPk={rowPkDesconocido}", ct);
        var desconocido = await desconocidoRespuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        var eventoDesconocido = Assert.Single(desconocido.GetProperty("elementos").EnumerateArray());
        Assert.Equal("Actor no identificado", eventoDesconocido.GetProperty("actor").GetString());
        Assert.Equal("Eliminación", eventoDesconocido.GetProperty("accionEtiqueta").GetString());
        Assert.Equal("Schema futuro", eventoDesconocido.GetProperty("modulo").GetString());
        // "scope" figura en changed_columns pero el evento no trae snapshot
        // (old_row/new_row NULL): sin valor en ninguno de los dos lados, el
        // campo se omite del resumen y de "Qué cambió" (bug de UI corregido,
        // sistema-seccion-unificada) en vez de listar una etiqueta sin dato.
        Assert.Equal("Eliminación de tabla nueva", eventoDesconocido.GetProperty("resumen").GetString());
    }

    private sealed class FabricaAdministracion(string cadena, Action<IServiceCollection>? servicios = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
            if (servicios is not null)
            {
                builder.ConfigureTestServices(servicios);
            }
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

    private async Task InsertarPersonaAsync(Guid id, string nombre, string apellido, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES (@id, @documento, @nombre, @apellido);
            """, conexion);
        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("documento", $"DOC-{id:N}");
        comando.Parameters.AddWithValue("nombre", nombre);
        comando.Parameters.AddWithValue("apellido", apellido);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task InsertarUsuarioAsync(Guid id, Guid personaId, string displayName, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.users (id, azure_oid, upn, display_name, persona_id)
            VALUES (@id, @azureOid, @upn, @displayName, @personaId);
            """, conexion);
        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("azureOid", Guid.NewGuid());
        comando.Parameters.AddWithValue("upn", $"{id:N}@prueba.invalid");
        comando.Parameters.AddWithValue("displayName", displayName);
        comando.Parameters.AddWithValue("personaId", personaId);
        await comando.ExecuteNonQueryAsync(ct);
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
        CancellationToken ct,
        string? filaAnterior = null,
        string? filaNueva = null,
        string[]? columnasCambiadas = null,
        bool sinColumnasCambiadas = false)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO audit.change_log
                (schema_name, table_name, row_pk, action, changed_columns, changed_by, request_id,
                 old_row, new_row, changed_at)
            VALUES (@schema, @tabla, @rowPk, @accion, @columnas, @actor, @requestId,
                    @filaAnterior, @filaNueva, clock_timestamp());
            """, conexion);
        comando.Parameters.AddWithValue("schema", schema);
        comando.Parameters.AddWithValue("tabla", tabla);
        comando.Parameters.AddWithValue("rowPk", rowPk);
        comando.Parameters.AddWithValue("accion", accion);
        comando.Parameters.Add(new NpgsqlParameter("columnas", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = sinColumnasCambiadas ? DBNull.Value : (object)(columnasCambiadas ?? ["scope"]),
        });
        comando.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid)
        {
            Value = actor.HasValue ? actor.Value : DBNull.Value,
        });
        comando.Parameters.AddWithValue("requestId", $"request-{Guid.NewGuid():N}");
        comando.Parameters.Add(new NpgsqlParameter("filaAnterior", NpgsqlDbType.Jsonb)
        {
            Value = (object?)filaAnterior ?? DBNull.Value,
        });
        comando.Parameters.Add(new NpgsqlParameter("filaNueva", NpgsqlDbType.Jsonb)
        {
            Value = (object?)filaNueva ?? DBNull.Value,
        });
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
