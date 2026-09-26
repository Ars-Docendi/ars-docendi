using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El acceso de soporte al historial ajeno, por HTTP de verdad
/// (asistente-acceso-de-soporte-al-historial).
/// </summary>
public sealed class SoporteHistorialControllerTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_soporte_historial")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly DateTimeOffset Ancla = new(2027, 6, 1, 10, 0, 0, TimeSpan.Zero);

    // -------------------------------------------------------------- el permiso

    [Fact]
    public async Task Sin_el_permiso_de_soporte_el_listado_se_rechaza()
    {
        await SembrarAsync();
        var sujeto = await SembrarHiloAsync(Coordinador, "algo", Ancla);
        _ = sujeto;

        // Secretaría tiene asistente.consultar, pero NO asistente.leer_historial_ajeno.
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ listar

    [Fact]
    public async Task Con_el_permiso_y_razon_el_listado_devuelve_las_conversaciones_del_sujeto()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await LeerAsync<List<ConversacionResumenDto>>(await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken));

        Assert.Single(lista);
        Assert.Equal(conversacion, lista[0].Id);
    }

    [Fact]
    public async Task Sin_razon_el_listado_se_rechaza_y_no_audita()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("   "),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_acceso_historial"));
    }

    [Fact]
    public async Task Listar_escribe_exactamente_una_fila_de_auditoria_sin_conversacion_puntual()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);
        await SembrarHiloAsync(Coordinador, "una charla", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, await EscalarAsync<long>("SELECT count(*) FROM asistente.auditoria_acceso_historial"));

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT lector_id, sujeto_id, hilo_historico_id, razon
              FROM asistente.auditoria_acceso_historial
            """, conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await lector.ReadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Secretaria, lector.GetGuid(0));
        Assert.Equal(Coordinador, lector.GetGuid(1));
        Assert.True(lector.IsDBNull(2));
        Assert.Equal("un reclamo de soporte", lector.GetString(3));
    }

    // ----------------------------------------- archivadas y pendientes de borrado

    [Fact]
    public async Task Una_conversacion_archivada_se_lista_marcada()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var archivada = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await MarcarArchivadaAsync(archivada);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await LeerAsync<List<ConversacionResumenDto>>(await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken));

        Assert.True(Assert.Single(lista).Archivada);
    }

    [Fact]
    public async Task Una_conversacion_dentro_de_su_ventana_de_borrado_se_lista_marcada()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var pendiente = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await MarcarPendienteAsync(pendiente, Ancla.AddSeconds(-3), Guid.NewGuid());

        var reloj = new RelojFijo(Ancla);
        using var host = CrearHost(reloj: reloj);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await LeerAsync<List<ConversacionResumenDto>>(await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken));

        var marcada = Assert.Single(lista);
        Assert.True(marcada.PendienteDeBorrado);
        Assert.False(marcada.Archivada);
    }

    [Fact]
    public async Task Una_conversacion_cuya_ventana_de_borrado_vencio_es_invisible_para_soporte()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var vencida = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await MarcarPendienteAsync(vencida, Ancla.AddSeconds(-16), Guid.NewGuid());

        var reloj = new RelojFijo(Ancla);
        using var host = CrearHost(reloj: reloj);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var lista = await LeerAsync<List<ConversacionResumenDto>>(await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/listar",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken));
        Assert.Empty(lista);

        var respuestaLeer = await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/{vencida}/leer",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, respuestaLeer.StatusCode);
    }

    [Fact]
    public async Task Leer_una_conversacion_pendiente_dentro_de_la_ventana_audita_igual()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var pendiente = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await MarcarPendienteAsync(pendiente, Ancla.AddSeconds(-3), Guid.NewGuid());

        var reloj = new RelojFijo(Ancla);
        using var host = CrearHost(reloj: reloj);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/{pendiente}/leer",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken);

        Assert.True(respuesta.IsSuccessStatusCode);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.auditoria_acceso_historial WHERE hilo_historico_id = @id",
            ("id", pendiente)));
    }

    // -------------------------------------------------------------------- leer

    [Fact]
    public async Task Leer_devuelve_pregunta_sql_estado_y_momentos_sin_filas()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await SembrarTurnoAsync(conversacion, "¿cuántos hay?", "SELECT count(*) FROM x");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var detalle = await LeerAsync<ConversacionDetalleDto>(await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/{conversacion}/leer",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken));

        Assert.Single(detalle.Turnos);
        Assert.Equal("¿cuántos hay?", detalle.Turnos[0].Pregunta);
        Assert.Equal("SELECT count(*) FROM x", detalle.Turnos[0].Sql);
        Assert.Equal("respondida", detalle.Turnos[0].Estado);
    }

    [Fact]
    public async Task Leer_no_expone_menciones_aunque_el_turno_tenga_referencias_persistidas()
    {
        // Decisión 15 del PO (design.md D11 de asistente-rediseno-v3): quien lee
        // acá nunca es el actor cuyo alcance decide si una mención se ve, así
        // que este endpoint no gana el campo — ni siquiera vacío — a
        // diferencia del lado propio (`HistorialControllerTests`).
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var materiaAlgoritmos = Guid.Parse("70000000-0000-4000-8000-000000000102");
        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await SembrarTurnoAsync(
            conversacion,
            "¿qué docentes están designados en @Algoritmos y Estructuras de Datos?",
            "SELECT p.apellido FROM designaciones.designaciones d "
                + "JOIN identity.personas p ON p.id = d.persona_id WHERE d.materia_id = $ref1",
            referencias: ReferenciaJson("materia", materiaAlgoritmos));

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await (await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/{conversacion}/leer",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("menciones", cuerpo, StringComparison.OrdinalIgnoreCase);

        var detalle = await LeerAsync<ConversacionDetalleDto>(await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/{conversacion}/leer",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken));

        Assert.Null(detalle.Turnos[0].Menciones);
    }

    [Fact]
    public async Task Leer_audita_nombrando_esa_conversacion_puntual()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await cliente.PostAsJsonAsync(
            $"/api/asistente/soporte/historial/{Coordinador}/{conversacion}/leer",
            new RazonDto("un reclamo de soporte"),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            conversacion,
            await EscalarAsync<Guid>("SELECT hilo_historico_id FROM asistente.auditoria_acceso_historial"));
    }

    [Fact]
    public async Task Un_fallo_de_auditoria_bloquea_la_lectura_sin_devolver_datos()
    {
        // MISMO CRITERIO QUE EscrituraDelHistorialTests, INVERTIDO: acá el
        // fallo de escritura SÍ tiene que propagar y bloquear la lectura
        // (design.md D10) — es la disciplina opuesta a IRegistroDelTurno.
        await SembrarAsync();
        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);

        var consultas = new ConsultasDeAuditoriaDeSoporte(
            new CadenaDuena("Host=localhost;Port=1;Database=inalcanzable;Timeout=1"),
            new RelojFijo(Ancla),
            Options.Create(new OpcionesAsistente()),
            NullLogger<ConsultasDeAuditoriaDeSoporte>.Instance);

        await Assert.ThrowsAnyAsync<NpgsqlException>(() => consultas.LeerAsync(
            Secretaria, Coordinador, conversacion, "un reclamo", TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------- disciplina de logging

    [Fact]
    public async Task El_log_de_acceso_de_soporte_nunca_combina_al_sujeto_con_la_pregunta_o_la_sql()
    {
        // EXTIENDE LA DISCIPLINA D3 de asistente-feedback-export-seguimiento:
        // la fila de auditoría YA es el registro completo de qué se leyó y
        // por qué; el log es sólo observabilidad operativa, y nunca puede
        // reconstruir "a fulano se le leyó la pregunta X".
        await SembrarAsync();

        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        await SembrarTurnoAsync(conversacion, "una pregunta muy identificable", "SELECT secreta_de_x");

        var registro = new RegistroDeCapturas();
        var consultas = new ConsultasDeAuditoriaDeSoporte(
            new CadenaDuena(Cadena),
            new RelojFijo(Ancla),
            Options.Create(new OpcionesAsistente()),
            registro.Logger<ConsultasDeAuditoriaDeSoporte>());

        await consultas.ListarAsync(Secretaria, Coordinador, "un reclamo", TestContext.Current.CancellationToken);
        await consultas.LeerAsync(
            Secretaria, Coordinador, conversacion, "un reclamo", TestContext.Current.CancellationToken);

        var todoElLog = registro.Todo();

        Assert.DoesNotContain("una pregunta muy identificable", todoElLog, StringComparison.Ordinal);
        Assert.DoesNotContain("secreta_de_x", todoElLog, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------- sin re-ejecución

    [Fact]
    public async Task Tener_el_permiso_de_soporte_no_habilita_reejecutar_un_turno_ajeno()
    {
        await SembrarAsync();
        await ConcederPermisoDeSoporteAAsync(Secretaria);

        var conversacion = await SembrarHiloAsync(Coordinador, "una charla", Ancla);
        var turno = await SembrarTurnoAsync(conversacion, "¿cuántos hay?", "SELECT 1");

        using var host = CrearHost();
        using var cliente = host.CreateClient();
        // Secretaría, que AHORA tiene el permiso de soporte, intenta usar el
        // endpoint de re-ejecución PROPIO contra un turno que no es suyo.
        Autenticar(cliente, Secretaria, "secretaria");

        var respuesta = await cliente.PostAsync(
            $"/api/asistente/historial/turnos/{turno}/reejecutar", null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public void No_hay_ningun_endpoint_de_reejecucion_en_el_controller_de_soporte()
    {
        var acciones = typeof(SoporteHistorialController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name);

        Assert.DoesNotContain(
            acciones, nombre => nombre.Contains("reejecut", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------ ningún «quién me vio» al sujeto

    [Fact]
    public void Ningun_endpoint_propio_devuelve_quien_accedio_al_historial()
    {
        var acciones = typeof(HistorialController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name);

        Assert.DoesNotContain(
            acciones,
            nombre => nombre.Contains("acceso", StringComparison.OrdinalIgnoreCase)
                || nombre.Contains("auditoria", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void El_lado_propio_del_historial_no_depende_de_la_auditoria_de_soporte()
    {
        // Si HistorialController alguna vez pidiera IConsultasDeAuditoriaDeSoporte,
        // tendría el canal para exponerla — aunque hoy no lo use. Que no esté
        // ni siquiera en el constructor es la garantía estructural.
        var parametros = typeof(HistorialController)
            .GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(IConsultasDeAuditoriaDeSoporte), parametros);
    }

    // ------------------------------------------------------------------ apoyo

    private async Task ConcederPermisoDeSoporteAAsync(Guid actor)
    {
        // No hay ningún rol con este permiso por default: se otorga a mano,
        // sobre el USUARIO (vía un rol dedicado de prueba), exactamente como
        // lo haría /membresia-roles.
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT ur.role_id, p.id
              FROM identity.user_roles ur
              JOIN identity.permisos p ON p.code = 'asistente.leer_historial_ajeno'
             WHERE ur.user_id = @actor
            ON CONFLICT (rol_id, permiso_id) DO NOTHING
            """, conexion);

        comando.Parameters.AddWithValue("actor", actor);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        var ct = TestContext.Current.CancellationToken;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private WebApplicationFactory<Program> CrearHost(TimeProvider? reloj = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Reemplaza al `TimeProvider.System` que el módulo registra con
            // `TryAddSingleton` — la última registración gana — para poder
            // adelantar la ventana de «Deshacer» sin esperarla de verdad.
            if (reloj is not null)
            {
                builder.ConfigureTestServices(servicios => servicios.AddSingleton(reloj));
            }

            builder.UseEnvironment("Development");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");

            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLectura)}",
                RolSoloLectura);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.RolSoloLecturaPii)}",
                RolSoloLecturaPii);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLectura)}",
                PostgresFixture.PasswordDeRol);
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.PasswordSoloLecturaPii)}",
                PostgresFixture.PasswordDeRol);
        });

    private async Task<Guid> SembrarHiloAsync(Guid actor, string titulo, DateTimeOffset actividad)
    {
        var id = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.hilo_historico (id, actor_id, titulo, creado_en, ultima_actividad)
            VALUES (@id, @actor, @titulo, @actividad, @actividad)
            """, conexion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("titulo", titulo);
        comando.Parameters.AddWithValue("actividad", actividad);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task MarcarArchivadaAsync(Guid hiloId)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "UPDATE asistente.hilo_historico SET archivada_en = now() WHERE id = @hilo", conexion);
        comando.Parameters.AddWithValue("hilo", hiloId);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task MarcarPendienteAsync(Guid hiloId, DateTimeOffset desde, Guid lote)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            UPDATE asistente.hilo_historico
               SET borrado_pendiente_desde = @desde, lote_de_borrado = @lote
             WHERE id = @hilo
            """, conexion);

        comando.Parameters.AddWithValue("desde", desde);
        comando.Parameters.AddWithValue("lote", lote);
        comando.Parameters.AddWithValue("hilo", hiloId);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SembrarTurnoAsync(
        Guid hiloId, string pregunta, string? sql, string? referencias = null)
    {
        var id = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.turno_historico
                (id, hilo_id, pregunta, sql_resuelto, estado, ocurrido_en, referencias)
            VALUES (@id, @hilo, @pregunta, @sql, 'Respondida', @ahora, @referencias)
            """, conexion);

        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("hilo", hiloId);
        comando.Parameters.AddWithValue("pregunta", pregunta);
        comando.Parameters.AddWithValue(
            "sql", NpgsqlTypes.NpgsqlDbType.Text, (object?)sql ?? DBNull.Value);
        comando.Parameters.AddWithValue("ahora", Ancla);
        comando.Parameters.AddWithValue(
            "referencias", NpgsqlTypes.NpgsqlDbType.Jsonb, (object?)referencias ?? DBNull.Value);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }

    /// <summary>
    /// El JSON de <c>turno_historico.referencias</c> para un único marcador
    /// <c>$ref1</c> (el mismo formato que <c>SerializacionDeReferencias</c>).
    /// </summary>
    private static string ReferenciaJson(string tipo, Guid id) =>
        "{\"$ref1\":{\"tipo\":\"" + tipo + "\",\"id\":\"" + id + "\"}}";
}
