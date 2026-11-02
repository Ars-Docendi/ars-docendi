using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Aulas.Api;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Aulas;

public sealed class AulasHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "aulas_http")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    /// <summary>Otro solicitante (rol "jefe_catedra", con <c>aulas.solicitar</c>).</summary>
    private static readonly Guid JefeCatedra = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Administrativo = Guid.Parse("a0000000-0000-4000-8000-000000000006");
    /// <summary>Another requester (jefe_catedra) who does not own the solicitudes created by Docente.</summary>
    private static readonly Guid OtroSolicitante = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    /// <summary>Materia asignada a Docente (rol "docente") en el seed sintético.</summary>
    private static readonly Guid MateriaDelDocente = Guid.Parse("70000000-0000-4000-8000-000000000101");
    /// <summary>Materia que Docente NO tiene asignada, para probar el rechazo.</summary>
    private static readonly Guid MateriaAjena = Guid.Parse("70000000-0000-4000-8000-000000000102");

    [Fact]
    public async Task Docente_crea_lista_y_cancela_su_propia_solicitud()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Docente, "docente");

        var creada = await CrearSolicitud(cliente, ct);
        Assert.Equal("pendiente", creada.Estado);
        Assert.Null(creada.AulaAsignada);

        var mias = await cliente.GetFromJsonAsync<SolicitudReservaAulaDto[]>(
            "/api/aulas/solicitudes/mias", ct);
        Assert.Equal(creada.Id, Assert.Single(mias!).Id);

        using var cancelar = await cliente.PostAsync(
            $"/api/aulas/solicitudes/{creada.Id}/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, cancelar.StatusCode);

        var luego = await cliente.GetFromJsonAsync<SolicitudReservaAulaDto[]>(
            "/api/aulas/solicitudes/mias", ct);
        Assert.Equal("cancelada", Assert.Single(luego!).Estado);
    }

    // [BR-aulas-004]
    [Fact]
    public async Task No_se_puede_cancelar_dos_veces_ni_una_solicitud_ajena()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");

        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteOtroDocente = host.CreateClient();
        Autenticar(clienteOtroDocente, JefeCatedra, "jefe_catedra");
        using var cancelarAjena = await clienteOtroDocente.PostAsync(
            $"/api/aulas/solicitudes/{creada.Id}/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, cancelarAjena.StatusCode);
        Assert.Contains("solicitud-ajena", await cancelarAjena.Content.ReadAsStringAsync(ct));
        var intacta = await clienteDocente.GetFromJsonAsync<SolicitudReservaAulaDto[]>(
            "/api/aulas/solicitudes/mias", ct);
        Assert.Equal("pendiente", Assert.Single(intacta!).Estado);

        Assert.Equal(HttpStatusCode.NoContent,
            (await clienteDocente.PostAsync($"/api/aulas/solicitudes/{creada.Id}/cancelar", null, ct))
                .StatusCode);

        using var segundaCancelacion = await clienteDocente.PostAsync(
            $"/api/aulas/solicitudes/{creada.Id}/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.Conflict, segundaCancelacion.StatusCode);
    }

    [Fact]
    public async Task Administrativo_ve_todas_las_solicitudes_y_asigna_aula()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");

        var todas = await clienteAdministrativo.GetFromJsonAsync<SolicitudReservaAulaDto[]>(
            "/api/aulas/solicitudes", ct);
        var propia = Assert.Single(todas!, s => s.Id == creada.Id);
        Assert.NotNull(propia.Docente);

        var asignada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar",
            new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct)), ct);
        Assert.Equal(HttpStatusCode.OK, asignada.StatusCode);
        var dto = (await asignada.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!;
        Assert.Equal("aprobada", dto.Estado);
        Assert.Equal("Aula 204", dto.AulaAsignada);

        var mias = await clienteDocente.GetFromJsonAsync<SolicitudReservaAulaDto[]>(
            "/api/aulas/solicitudes/mias", ct);
        Assert.Equal("Aula 204", Assert.Single(mias!).AulaAsignada);
    }

    [Fact]
    public async Task Administrativo_puede_actualizar_el_aula_de_una_solicitud_aprobada_pero_no_cancelada()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var aprobar = await CrearSolicitud(clienteDocente, ct);
        var cancelar = await CrearSolicitud(clienteDocente, ct);
        Assert.Equal(HttpStatusCode.NoContent,
            (await clienteDocente.PostAsync($"/api/aulas/solicitudes/{cancelar.Id}/cancelar", null, ct))
                .StatusCode);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobar.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct)), ct))
                .StatusCode);

        var actualizada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobar.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 305", ct)), ct);
        Assert.Equal(HttpStatusCode.OK, actualizada.StatusCode);
        var dto = (await actualizada.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!;
        Assert.Equal("aprobada", dto.Estado);
        Assert.Equal("Aula 305", dto.AulaAsignada);

        var sobreCancelada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{cancelar.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 999", ct)), ct);
        Assert.Equal(HttpStatusCode.Conflict, sobreCancelada.StatusCode);
    }

    [Fact]
    public async Task Administrativo_rechaza_una_solicitud_pendiente_con_motivo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");

        var rechazada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/rechazar",
            new RechazarSolicitudDto("El aula solicitada no está disponible en ese horario"), ct);
        Assert.Equal(HttpStatusCode.OK, rechazada.StatusCode);
        var dto = (await rechazada.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!;
        Assert.Equal("rechazada", dto.Estado);
        Assert.Equal("El aula solicitada no está disponible en ese horario", dto.MotivoRechazo);
        Assert.Null(dto.AulaAsignada);

        var mias = await clienteDocente.GetFromJsonAsync<SolicitudReservaAulaDto[]>(
            "/api/aulas/solicitudes/mias", ct);
        var propia = Assert.Single(mias!);
        Assert.Equal("rechazada", propia.Estado);
        Assert.Equal("El aula solicitada no está disponible en ese horario", propia.MotivoRechazo);

        var sobreRechazada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct)), ct);
        Assert.Equal(HttpStatusCode.Conflict, sobreRechazada.StatusCode);
    }

    // [BR-aulas-003]
    [Fact]
    public async Task No_se_puede_rechazar_sin_motivo_ni_una_solicitud_que_no_este_pendiente()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var pendiente = await CrearSolicitud(clienteDocente, ct);
        var aprobada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");

        var sinMotivo = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{pendiente.Id}/rechazar", new RechazarSolicitudDto(""), ct);
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobada.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct)), ct))
                .StatusCode);
        var sobreAprobada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobada.Id}/rechazar", new RechazarSolicitudDto("Motivo"), ct);
        Assert.Equal(HttpStatusCode.Conflict, sobreAprobada.StatusCode);
    }

    [Fact]
    public async Task Un_actor_sin_permiso_de_aulas_no_accede_a_los_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Coordinador, "coordinador_carrera");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.GetAsync("/api/aulas/solicitudes/mias", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.GetAsync("/api/aulas/solicitudes", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PostAsJsonAsync("/api/aulas/solicitudes", DatosSolicitud(), ct)).StatusCode);
    }

    [Fact]
    public async Task Un_docente_sin_aulas_aprobar_no_asigna_ni_rechaza()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        // "docente" tiene aulas.ver y aulas.solicitar, pero no aulas.aprobar.
        Autenticar(cliente, Docente, "docente");
        var creada = await CrearSolicitud(cliente, ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto(Guid.NewGuid()), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/rechazar", new RechazarSolicitudDto("Motivo"), ct)).StatusCode);
    }

    // [BR-aulas-002]
    [Fact]
    public async Task Rechaza_horario_invalido_y_cantidad_de_alumnos_invalida()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Docente, "docente");

        var horarioInvalido = await cliente.PostAsJsonAsync("/api/aulas/solicitudes", new
        {
            dia = "2026-10-01",
            horarioDesde = "10:00:00",
            horarioHasta = "09:00:00",
            cantidadAlumnosAprox = 30,
            materiaId = MateriaDelDocente,
            comision = "K3001",
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, horarioInvalido.StatusCode);

        var alumnosInvalidos = await cliente.PostAsJsonAsync("/api/aulas/solicitudes", new
        {
            dia = "2026-10-01",
            horarioDesde = "08:00:00",
            horarioHasta = "10:00:00",
            cantidadAlumnosAprox = 0,
            materiaId = MateriaDelDocente,
            comision = "K3001",
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, alumnosInvalidos.StatusCode);
    }

    // [BR-aulas-001]
    [Fact]
    public async Task Lista_las_materias_propias_y_rechaza_una_solicitud_con_materia_ajena()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        Autenticar(cliente, Docente, "docente");

        var materias = await cliente.GetFromJsonAsync<MateriaOpcionDto[]>(
            "/api/aulas/solicitudes/materias-propias", ct);
        Assert.Contains(materias!, m => m.Id == MateriaDelDocente);
        Assert.DoesNotContain(materias!, m => m.Id == MateriaAjena);

        var rechazada = await cliente.PostAsJsonAsync(
            "/api/aulas/solicitudes", DatosSolicitud(MateriaAjena), ct);
        Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
    }

    [Fact]
    public async Task El_detalle_es_visible_para_el_dueno_y_para_quien_aprueba_y_oculta_la_existencia_al_resto()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var creada = await CrearSolicitud(clienteDocente, ct);

        var propio = (await clienteDocente.GetFromJsonAsync<SolicitudReservaAulaDto>(
            $"/api/aulas/solicitudes/{creada.Id}", ct))!;
        Assert.True(propio.EsPropia);
        Assert.NotNull(propio.Docente);

        // The creating session's active role is persisted for the notifications.
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand(
            "SELECT rol_solicitante FROM aulas.solicitudes_reserva WHERE id = @id", conexion))
        {
            comando.Parameters.AddWithValue("id", creada.Id);
            Assert.Equal("docente", await comando.ExecuteScalarAsync(ct));
        }

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        var visto = (await clienteAdministrativo.GetFromJsonAsync<SolicitudReservaAulaDto>(
            $"/api/aulas/solicitudes/{creada.Id}", ct))!;
        Assert.False(visto.EsPropia);
        Assert.NotNull(visto.Docente);

        // Another requester and a role without Aulas permissions get the same 404 as a missing id.
        using var clienteOtroDocente = host.CreateClient();
        Autenticar(clienteOtroDocente, OtroSolicitante, "jefe_catedra");
        using var clienteCoordinador = host.CreateClient();
        Autenticar(clienteCoordinador, Coordinador, "coordinador_carrera");
        foreach (var cliente in new[] { clienteOtroDocente, clienteCoordinador })
        {
            var ajena = await cliente.GetAsync($"/api/aulas/solicitudes/{creada.Id}", ct);
            var inexistente = await cliente.GetAsync($"/api/aulas/solicitudes/{Guid.NewGuid()}", ct);
            Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
            Assert.Contains("solicitud-not-found", await ajena.Content.ReadAsStringAsync(ct));
        }
    }

    [Fact]
    public async Task Administrativo_cancela_una_reserva_aprobada_con_motivo_y_se_libera_el_aula()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct)), ct)).StatusCode);

        var cancelada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/cancelar-reserva",
            new CancelarReservaDto("El aula quedó en mantenimiento"), ct);
        Assert.Equal(HttpStatusCode.OK, cancelada.StatusCode);
        var dto = (await cancelada.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!;
        Assert.Equal("cancelada", dto.Estado);
        Assert.Null(dto.AulaAsignada);
        Assert.Equal("El aula quedó en mantenimiento", dto.MotivoCancelacion);

        var detalle = (await clienteDocente.GetFromJsonAsync<SolicitudReservaAulaDto>(
            $"/api/aulas/solicitudes/{creada.Id}", ct))!;
        Assert.Equal("El aula quedó en mantenimiento", detalle.MotivoCancelacion);

        // A cancelled reservation is terminal: it cannot be cancelled again or reassigned.
        var otraVez = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/cancelar-reserva", new CancelarReservaDto("Otra vez"), ct);
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
        Assert.Contains("solicitud-no-aprobada", await otraVez.Content.ReadAsStringAsync(ct));
    }

    // [BR-aulas-003]
    [Fact]
    public async Task No_se_cancela_una_reserva_sin_motivo_ni_fuera_de_aprobada_ni_sin_permiso()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var pendiente = await CrearSolicitud(clienteDocente, ct);
        var aprobada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobada.Id}/asignar", new AsignarAulaDto(await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct)), ct)).StatusCode);

        var sinMotivo = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobada.Id}/cancelar-reserva", new CancelarReservaDto("   "), ct);
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        // The body is parsed (not string-matched) because the serializer escapes accented characters.
        using var problema = JsonDocument.Parse(await sinMotivo.Content.ReadAsStringAsync(ct));
        var raiz = problema.RootElement;
        Assert.Contains("solicitud-invalida", raiz.GetRawText());
        Assert.Equal("Debés indicar el motivo de la cancelación.",
            raiz.GetProperty("errors").GetProperty("motivo")[0].GetString());

        var sobrePendiente = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{pendiente.Id}/cancelar-reserva", new CancelarReservaDto("Motivo"), ct);
        Assert.Equal(HttpStatusCode.Conflict, sobrePendiente.StatusCode);
        using var conflicto = JsonDocument.Parse(await sobrePendiente.Content.ReadAsStringAsync(ct));
        Assert.Equal("Sólo se puede cancelar una reserva en estado Aprobada.",
            conflicto.RootElement.GetProperty("detail").GetString());

        // The owner has aulas.solicitar but not aulas.aprobar: the policy denies the call.
        var propietario = await clienteDocente.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobada.Id}/cancelar-reserva", new CancelarReservaDto("Motivo"), ct);
        Assert.Equal(HttpStatusCode.Forbidden, propietario.StatusCode);
        var enBase = (await clienteAdministrativo.GetFromJsonAsync<SolicitudReservaAulaDto>(
            $"/api/aulas/solicitudes/{aprobada.Id}", ct))!;
        Assert.Equal("aprobada", enBase.Estado);
    }

    [Fact]
    public async Task La_base_rechaza_un_motivo_de_cancelacion_fuera_del_estado_cancelada()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var creada = await CrearSolicitud(clienteDocente, ct);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "UPDATE aulas.solicitudes_reserva SET motivo_cancelacion = 'x' WHERE id = @id", conexion);
        comando.Parameters.AddWithValue("id", creada.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(ct));
        Assert.Equal("solicitudes_reserva_motivo_cancelacion_si_cancelada", error.ConstraintName);
    }

    // [BR-aulas-005]
    [Fact]
    public async Task Un_aula_no_se_asigna_a_dos_reservas_superpuestas_pero_si_a_horarios_contiguos()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var primera = await CrearSolicitud(clienteDocente, ct);
        var superpuesta = await CrearSolicitud(clienteDocente, ct, "09:00:00", "11:00:00");
        var contigua = await CrearSolicitud(clienteDocente, ct, "10:00:00", "12:00:00");

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        var aula = await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct);
        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{primera.Id}/asignar", new AsignarAulaDto(aula), ct)).StatusCode);

        var ocupada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{superpuesta.Id}/asignar", new AsignarAulaDto(aula), ct);
        Assert.Equal(HttpStatusCode.Conflict, ocupada.StatusCode);
        Assert.Contains("aula-ocupada", await ocupada.Content.ReadAsStringAsync(ct));
        var intacta = (await clienteAdministrativo.GetFromJsonAsync<SolicitudReservaAulaDto>(
            $"/api/aulas/solicitudes/{superpuesta.Id}", ct))!;
        Assert.Equal("pendiente", intacta.Estado);

        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{contigua.Id}/asignar", new AsignarAulaDto(aula), ct)).StatusCode);
    }

    // [BR-aulas-005] El chequeo previo no alcanza bajo carrera: la exclusión de la base decide.
    [Fact]
    public async Task Dos_asignaciones_concurrentes_del_mismo_aula_dejan_una_sola_reserva_aprobada()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var solicitudes = new[] { await CrearSolicitud(clienteDocente, ct), await CrearSolicitud(clienteDocente, ct) };

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        var aula = await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct);

        var respuestas = await Task.WhenAll(solicitudes.Select(s => clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{s.Id}/asignar", new AsignarAulaDto(aula), ct)));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM aulas.solicitudes_reserva WHERE aula_id = @aula AND estado = 'aprobada'", conexion);
        comando.Parameters.AddWithValue("aula", aula);
        Assert.Equal(1L, await comando.ExecuteScalarAsync(ct));
    }

    // [BR-aulas-006] [BR-aulas-007]
    [Fact]
    public async Task Se_asigna_solo_un_aula_activa_aunque_la_capacidad_quede_corta()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        var chica = await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula chica", ct, capacidad: 10);
        var inactiva = await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula cerrada", ct);
        var aulaInactiva = (await clienteAdministrativo.GetFromJsonAsync<AulaDto[]>("/api/aulas/catalogo", ct))!
            .Single(a => a.Id == inactiva);
        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/catalogo/{inactiva}/desactivar", new CambiarEstadoAulaDto(aulaInactiva.Version), ct)).StatusCode);

        var sobreInactiva = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto(inactiva), ct);
        Assert.Equal(HttpStatusCode.BadRequest, sobreInactiva.StatusCode);
        Assert.Contains("aulaId", await sobreInactiva.Content.ReadAsStringAsync(ct));

        // 30 alumnos en un aula de 10: la interfaz advierte, el backend no bloquea.
        var asignada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto(chica), ct);
        Assert.Equal(HttpStatusCode.OK, asignada.StatusCode);
        Assert.Equal("Aula chica", (await asignada.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!.AulaAsignada);
    }

    // [BR-aulas-006]
    [Fact]
    public async Task El_catalogo_exige_aulas_gestionar_nombres_unicos_y_no_borra_un_aula_en_uso()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");
        Assert.Equal(HttpStatusCode.Forbidden, (await clienteDocente.GetAsync("/api/aulas/catalogo", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clienteDocente.PostAsJsonAsync(
            "/api/aulas/catalogo", new GuardarAulaDto("Aula 1", "aula", 30, null), ct)).StatusCode);
        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        var enUso = await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula 204", ct);
        var repetida = await clienteAdministrativo.PostAsJsonAsync(
            "/api/aulas/catalogo", new GuardarAulaDto(" aula 204 ", "aula", 30, null), ct);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Contains("aula-nombre-repetido", await repetida.Content.ReadAsStringAsync(ct));

        Assert.Equal(HttpStatusCode.OK, (await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto(enUso), ct)).StatusCode);
        var borrarEnUso = await clienteAdministrativo.DeleteAsync($"/api/aulas/catalogo/{enUso}", ct);
        Assert.Equal(HttpStatusCode.Conflict, borrarEnUso.StatusCode);
        Assert.Contains("aula-en-uso", await borrarEnUso.Content.ReadAsStringAsync(ct));

        var libre = await CatalogoAulasPrueba.IdAsync(clienteAdministrativo, "Aula libre", ct);
        Assert.Equal(HttpStatusCode.NoContent,
            (await clienteAdministrativo.DeleteAsync($"/api/aulas/catalogo/{libre}", ct)).StatusCode);
    }

    private static object DatosSolicitud(
        Guid? materiaId = null, string desde = "08:00:00", string hasta = "10:00:00") => new
    {
        dia = "2026-10-01",
        horarioDesde = desde,
        horarioHasta = hasta,
        cantidadAlumnosAprox = 30,
        materiaId = materiaId ?? MateriaDelDocente,
        comision = "K3001",
    };

    private static async Task<SolicitudReservaAulaDto> CrearSolicitud(
        HttpClient cliente, CancellationToken ct, string desde = "08:00:00", string hasta = "10:00:00")
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/aulas/solicitudes", DatosSolicitud(desde: desde, hasta: hasta), ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!;
    }

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting(
                $"{AutenticacionDesarrolloOptions.Seccion}:Enabled",
                "true");
        });

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
