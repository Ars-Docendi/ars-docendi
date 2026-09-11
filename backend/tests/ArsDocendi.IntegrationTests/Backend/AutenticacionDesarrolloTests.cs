using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Identity.Desarrollo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class AutenticacionDesarrolloTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "auth_dev")
{
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Administrativo = Guid.Parse("a0000000-0000-4000-8000-000000000006");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Inactivo = Guid.Parse("a0000000-0000-4000-8000-000000000008");
    private static readonly Guid MateriaAjena = Guid.Parse("70000000-0000-4000-8000-000000000201");
    private static readonly Guid CarreraVisible = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid CarreraAjena = Guid.Parse("c0000000-0000-4000-8000-000000000202");
    private static readonly Guid RolDocente = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    private static readonly Guid CargoAdjunto = Guid.Parse("c3000000-0000-4000-8000-000000000003");
    private static readonly Guid Dedicacion = Guid.Parse("d6000000-0000-4000-8000-000000000002");
    private static readonly Guid[] MateriasDelJefe =
    [
        Guid.Parse("70000000-0000-4000-8000-000000000101"),
        Guid.Parse("70000000-0000-4000-8000-000000000102"),
        Guid.Parse("70000000-0000-4000-8000-000000000103"),
    ];

    [Fact]
    public async Task Catalogo_y_handler_aceptan_usuario_activo_con_rol_asignado()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();

        var identidades = await cliente.GetFromJsonAsync<IdentidadDesarrolloDto[]>(
            "/api/desarrollo/identidades", ct);

        Assert.NotNull(identidades);
        Assert.DoesNotContain(identidades, i => i.UsuarioId == Inactivo);
        var jefe = Assert.Single(identidades, i => i.UsuarioId == Jefe);
        Assert.Contains(jefe.Roles, r => r.Codigo == "jefe_catedra" && r.Materias.Count > 0);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
        using var respuesta = await cliente.SendAsync(solicitud, ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Jefe_accede_a_docentes_solo_en_sus_materias_y_no_puede_modificarlos()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var (personaMixta, personaAjena) = await AgregarDocentesConAmbitoMixtoAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");

        var docentes = await cliente.GetFromJsonAsync<DocenteAdministracionDto[]>(
            "/api/administracion/docentes", ct);
        var catalogos = await cliente.GetFromJsonAsync<CatalogosDocentesDto>(
            "/api/administracion/docentes/catalogos", ct);
        using var detalleFueraDeAmbito = await cliente.GetAsync(
            "/api/administracion/docentes/d0000000-0000-4000-8000-000000000010", ct);
        using var detalleMixto = await cliente.GetAsync(
            $"/api/administracion/docentes/{personaMixta}", ct);
        using var detalleAjeno = await cliente.GetAsync(
            $"/api/administracion/docentes/{personaAjena}", ct);
        using var alta = await cliente.PostAsJsonAsync(
            "/api/administracion/docentes", new { }, ct);

        Assert.NotNull(docentes);
        Assert.NotEmpty(docentes);
        Assert.All(docentes, docente =>
            Assert.Contains(docente.Asignaciones, asignacion => MateriasDelJefe.Contains(asignacion.MateriaId)));
        var mixta = Assert.Single(docentes, docente => docente.PersonaId == personaMixta);
        Assert.DoesNotContain(mixta.Asignaciones, asignacion => asignacion.MateriaId == MateriaAjena);
        Assert.Contains(mixta.Membresias, membresia => membresia.MateriaId == MateriasDelJefe[0]);
        Assert.DoesNotContain(mixta.Membresias, membresia => membresia.MateriaId == MateriaAjena);
        Assert.DoesNotContain(docentes, docente => docente.PersonaId == personaAjena);
        Assert.NotNull(catalogos);
        Assert.Equal(MateriasDelJefe.Length, catalogos.Materias.Count);
        Assert.All(catalogos.Materias, materia => Assert.Contains(materia.Id, MateriasDelJefe));
        Assert.Empty(catalogos.PersonasElegibles);
        Assert.Equal(HttpStatusCode.OK, detalleMixto.StatusCode);
        var detalle = await detalleMixto.Content.ReadFromJsonAsync<DocenteAdministracionDto>(ct);
        Assert.NotNull(detalle);
        Assert.DoesNotContain(detalle.Asignaciones, asignacion => asignacion.MateriaId == MateriaAjena);
        Assert.DoesNotContain(detalle.Membresias, membresia => membresia.MateriaId == MateriaAjena);
        Assert.Equal(HttpStatusCode.NotFound, detalleAjeno.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detalleFueraDeAmbito.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);
    }

    [Fact]
    public async Task Docente_sin_permiso_no_accede_a_la_administracion_de_docentes()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, "/api/administracion/docentes");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Docente.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "docente");

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/api/administracion/roles")]
    [InlineData("/api/administracion/permisos")]
    public async Task Administrativo_accede_a_los_catalogos_de_roles_y_permisos(string ruta)
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        solicitud.Headers.Add(
            AutenticacionDesarrolloHandler.HeaderUsuario,
            Administrativo.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "administrativo");

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Catalogo_de_permisos_y_defaults_expone_las_pantallas_explicitas()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Administrativo.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "administrativo");

        var permisos = await cliente.GetFromJsonAsync<PermisoAdministracionDto[]>(
            "/api/administracion/permisos", ct);
        var roles = await cliente.GetFromJsonAsync<RolAdministracionDto[]>(
            "/api/administracion/roles", ct);

        Assert.NotNull(permisos);
        Assert.Contains(permisos, permiso => permiso.Codigo == "docentes.ver");
        Assert.Contains(permisos, permiso => permiso.Codigo == "designaciones.revisar");
        Assert.Contains(Assert.Single(roles!, rol => rol.Codigo == "administrativo").Permisos,
            permiso => permiso.Codigo == "docentes.ver");
        Assert.Contains(Assert.Single(roles!, rol => rol.Codigo == "administrativo").Permisos,
            permiso => permiso.Codigo == "designaciones.revisar");
    }

    [Fact]
    public async Task Catalogo_y_handler_aceptan_rol_personalizado_con_permisos_y_omiten_inactivo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var rolActivo = Guid.NewGuid();
        var rolInactivo = Guid.NewGuid();
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope, is_active)
            VALUES (@activo, 'revisor_custom', 'Revisor personalizado', 'global', TRUE),
                   (@inactivo, 'inactivo_custom', 'Inactivo personalizado', 'global', FALSE);
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT @activo, id FROM identity.permisos
            WHERE code IN ('designaciones.ver', 'docentes.ver', 'designaciones.revisar');
            INSERT INTO identity.user_roles (user_id, role_id)
            VALUES (@usuario, @activo);
            """, conexion))
        {
            comando.Parameters.AddWithValue("activo", rolActivo);
            comando.Parameters.AddWithValue("inactivo", rolInactivo);
            comando.Parameters.AddWithValue("usuario", Jefe);
            await comando.ExecuteNonQueryAsync(ct);
        }

        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        var identidades = await cliente.GetFromJsonAsync<IdentidadDesarrolloDto[]>(
            "/api/desarrollo/identidades", ct);
        var jefe = Assert.Single(identidades!, identidad => identidad.UsuarioId == Jefe);
        var custom = Assert.Single(jefe.Roles, rol => rol.Codigo == "revisor_custom");
        Assert.Contains("docentes.ver", custom.Permisos);
        Assert.DoesNotContain(jefe.Roles, rol => rol.Codigo == "inactivo_custom");

        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "revisor_custom");
        using var docentes = await cliente.GetAsync("/api/administracion/docentes", ct);
        Assert.Equal(HttpStatusCode.OK, docentes.StatusCode);
    }

    [Theory]
    [InlineData("a0000000-0000-4000-8000-000000000002", "rol_inventado")]
    [InlineData("a0000000-0000-4000-8000-000000000008", "docente")]
    [InlineData("ffffffff-ffff-4fff-8fff-ffffffffffff", "jefe_catedra")]
    public async Task Handler_rechaza_rol_inventado_usuario_inactivo_o_inexistente(
        string usuarioId,
        string rol)
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuarioId);
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Handler_rechaza_usuario_activo_ajeno_al_dataset()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var usuario = Guid.NewGuid();
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES (@id, @documento, 'Fuera', 'Del seed');
            INSERT INTO identity.users (id, azure_oid, upn, display_name, is_active, persona_id)
            VALUES (@id, @oid, @upn, 'Fuera del seed', TRUE, @id);
            INSERT INTO identity.user_roles (id, user_id, role_id, materia_id, carrera_id)
            VALUES (@asignacion, @id, 'a1000000-0000-4000-8000-000000000002',
                    '70000000-0000-4000-8000-000000000101',
                    'c0000000-0000-4000-8000-000000000201');
            """, conexion))
        {
            comando.Parameters.AddWithValue("id", usuario);
            comando.Parameters.AddWithValue("documento", $"T-{usuario:N}");
            comando.Parameters.AddWithValue("oid", Guid.NewGuid());
            comando.Parameters.AddWithValue("upn", $"{usuario:N}@example.test");
            comando.Parameters.AddWithValue("asignacion", Guid.NewGuid());
            await comando.ExecuteNonQueryAsync(ct);
        }
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Production_no_registra_endpoint_ni_esquema_de_desarrollo()
    {
        using var host = CrearHost("Production", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/desarrollo/identidades");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");

        using var respuesta = await cliente.SendAsync(
            solicitud, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Staging_con_opt_in_registra_catalogo_y_esquema_de_desarrollo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Staging", true);
        using var cliente = host.CreateClient();

        var identidades = await cliente.GetFromJsonAsync<IdentidadDesarrolloDto[]>(
            "/api/desarrollo/identidades", ct);

        Assert.NotNull(identidades);
        Assert.Contains(identidades, identidad => identidad.UsuarioId == Jefe);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
        using var respuesta = await cliente.SendAsync(solicitud, ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private WebApplicationFactory<Program> CrearHost(string ambiente, bool habilitada) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting(
                $"{AutenticacionDesarrolloOptions.Seccion}:Enabled",
                habilitada.ToString());
        });

    private async Task<(Guid PersonaMixta, Guid PersonaAjena)> AgregarDocentesConAmbitoMixtoAsync(
        CancellationToken ct)
    {
        var personaMixta = Guid.NewGuid();
        var personaAjena = Guid.NewGuid();
        var usuarioMixto = Guid.NewGuid();
        var ahora = DateTimeOffset.UtcNow;
        await using (var identity = PostgresFixture.CrearIdentity(Cadena))
        {
            identity.Personas.AddRange(
                new Persona
                {
                    Id = personaMixta,
                    Documento = $"M-{personaMixta:N}",
                    Nombre = "Docente",
                    Apellido = "Mixto",
                    Legajo = $"M-{personaMixta:N}",
                    CreadoEn = ahora,
                },
                new Persona
                {
                    Id = personaAjena,
                    Documento = $"A-{personaAjena:N}",
                    Nombre = "Docente",
                    Apellido = "Ajeno",
                    Legajo = $"A-{personaAjena:N}",
                    CreadoEn = ahora,
                });
            identity.Usuarios.Add(new Usuario
            {
                Id = usuarioMixto,
                AzureOid = Guid.NewGuid(),
                Upn = $"mixto-{usuarioMixto:N}@example.test",
                NombreParaMostrar = "Docente Mixto",
                Activo = true,
                PersonaId = personaMixta,
                CreadoEn = ahora,
            });
            identity.UsuarioRoles.AddRange(
                new UsuarioRol
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuarioMixto,
                    RolId = RolDocente,
                    MateriaId = MateriasDelJefe[0],
                    CarreraId = CarreraVisible,
                    OtorgadoEn = ahora,
                    CreadoEn = ahora,
                },
                new UsuarioRol
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuarioMixto,
                    RolId = RolDocente,
                    MateriaId = MateriaAjena,
                    CarreraId = CarreraAjena,
                    OtorgadoEn = ahora,
                    CreadoEn = ahora,
                });
            await identity.SaveChangesAsync(ct);
        }

        await using var designaciones = PostgresFixture.CrearDesignaciones(Cadena);
        designaciones.Designaciones.AddRange(
            CrearDesignacion(personaMixta, MateriasDelJefe[0]),
            CrearDesignacion(personaMixta, MateriaAjena),
            CrearDesignacion(personaAjena, MateriaAjena));
        await designaciones.SaveChangesAsync(ct);
        return (personaMixta, personaAjena);
    }

    private static Designacion CrearDesignacion(Guid personaId, Guid materiaId) => new()
    {
        Id = Guid.NewGuid(),
        PersonaId = personaId,
        MateriaId = materiaId,
        CargoId = CargoAdjunto,
        DedicacionId = Dedicacion,
        Horas = 10,
        VigenteDesde = new DateOnly(2026, 8, 1),
        CreadoEn = DateTimeOffset.UtcNow,
    };

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
