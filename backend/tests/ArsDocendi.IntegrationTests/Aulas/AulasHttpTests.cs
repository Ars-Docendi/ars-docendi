using System.Net;
using System.Net.Http.Json;
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
    private static readonly Guid Administrativo = Guid.Parse("a0000000-0000-4000-8000-000000000006");
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

    [Fact]
    public async Task No_se_puede_cancelar_dos_veces_ni_una_solicitud_ajena()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var clienteDocente = host.CreateClient();
        Autenticar(clienteDocente, Docente, "docente");

        var creada = await CrearSolicitud(clienteDocente, ct);

        using var clienteAdministrativo = host.CreateClient();
        Autenticar(clienteAdministrativo, Administrativo, "administrativo");
        using var cancelarAjena = await clienteAdministrativo.PostAsync(
            $"/api/aulas/solicitudes/{creada.Id}/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, cancelarAjena.StatusCode);

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
            new AsignarAulaDto("Aula 204"), ct);
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
            $"/api/aulas/solicitudes/{aprobar.Id}/asignar", new AsignarAulaDto("Aula 204"), ct))
                .StatusCode);

        var actualizada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{aprobar.Id}/asignar", new AsignarAulaDto("Aula 305"), ct);
        Assert.Equal(HttpStatusCode.OK, actualizada.StatusCode);
        var dto = (await actualizada.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(ct))!;
        Assert.Equal("aprobada", dto.Estado);
        Assert.Equal("Aula 305", dto.AulaAsignada);

        var sobreCancelada = await clienteAdministrativo.PostAsJsonAsync(
            $"/api/aulas/solicitudes/{cancelar.Id}/asignar", new AsignarAulaDto("Aula 999"), ct);
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
            $"/api/aulas/solicitudes/{creada.Id}/asignar", new AsignarAulaDto("Aula 204"), ct);
        Assert.Equal(HttpStatusCode.Conflict, sobreRechazada.StatusCode);
    }

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
            $"/api/aulas/solicitudes/{aprobada.Id}/asignar", new AsignarAulaDto("Aula 204"), ct))
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

    private static object DatosSolicitud(Guid? materiaId = null) => new
    {
        dia = "2026-10-01",
        horarioDesde = "08:00:00",
        horarioHasta = "10:00:00",
        cantidadAlumnosAprox = 30,
        materiaId = materiaId ?? MateriaDelDocente,
        comision = "K3001",
    };

    private static async Task<SolicitudReservaAulaDto> CrearSolicitud(HttpClient cliente, CancellationToken ct)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/aulas/solicitudes", DatosSolicitud(), ct);
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
