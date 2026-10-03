using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Ingreso;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

/// <summary>
/// Regla de ingreso con cuentas Microsoft (fase 1): reconoce por mail verificado,
/// sólo a usuarios existentes, activos y con rol vigente, y nunca escribe al rechazar.
/// </summary>
public sealed class IngresoMicrosoftTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "ingreso_ms")
{
    private static readonly Guid TenantOrganizacion = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid RolDocente = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Acepta_usuario_activo_con_rol_y_mail_verificado()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync("ada@dominio.edu.ar", activo: true, [RolSecretaria], ct);

        var resultado = await ResolverAsync(CuentaOrganizacional("ada@dominio.edu.ar"), ct);

        Assert.True(resultado.Aceptado);
        Assert.Equal(usuario, resultado.UsuarioId);
    }

    [Fact]
    public async Task Rechaza_mail_sin_dominio_verificado()
    {
        var ct = TestContext.Current.CancellationToken;
        await CrearUsuarioAsync("ada@dominio.edu.ar", activo: true, [RolSecretaria], ct);

        var sinVerificar = await ResolverAsync(
            CuentaOrganizacional("ada@dominio.edu.ar") with { EmailDominioVerificado = false }, ct);
        var sinClaim = await ResolverAsync(
            CuentaOrganizacional("ada@dominio.edu.ar") with { EmailDominioVerificado = null }, ct);
        var sinMail = await ResolverAsync(CuentaOrganizacional(null), ct);

        Assert.Equal(MotivoRechazoIngreso.MailNoVerificado, sinVerificar.Motivo);
        Assert.Equal(MotivoRechazoIngreso.MailNoVerificado, sinClaim.Motivo);
        Assert.Equal(MotivoRechazoIngreso.MailNoVerificado, sinMail.Motivo);
    }

    [Fact]
    public async Task Acepta_cuenta_personal_aunque_no_llegue_xms_edov()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync("persona@outlook.es", activo: true, [RolDocente], ct);

        var resultado = await ResolverAsync(
            new DatosCuentaMicrosoft(ServicioIngreso.TenantCuentasPersonales, Guid.NewGuid(), "persona@outlook.es", null),
            ct);

        Assert.Equal(usuario, resultado.UsuarioId);
    }

    [Fact]
    public async Task Reconoce_mail_con_mayusculas_y_espacios()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync("nombre@dominio.edu.ar", activo: true, [RolDocente], ct);

        var resultado = await ResolverAsync(CuentaOrganizacional("  Nombre@Dominio.edu.ar "), ct);

        Assert.Equal(usuario, resultado.UsuarioId);
    }

    [Fact]
    public async Task Rechaza_mail_no_registrado_inactivo_y_sin_rol_sin_escribir()
    {
        var ct = TestContext.Current.CancellationToken;
        await CrearUsuarioAsync("inactivo@dominio.edu.ar", activo: false, [RolDocente], ct);
        await CrearUsuarioAsync("sinrol@dominio.edu.ar", activo: true, [], ct);
        await CrearUsuarioAsync("revocado@dominio.edu.ar", activo: true, [RolDocente], ct, revocado: true);
        var antes = await ContarEscriturasAsync(ct);

        var noRegistrado = await ResolverAsync(CuentaOrganizacional("nadie@dominio.edu.ar"), ct);
        var inactivo = await ResolverAsync(CuentaOrganizacional("inactivo@dominio.edu.ar"), ct);
        var sinRol = await ResolverAsync(CuentaOrganizacional("sinrol@dominio.edu.ar"), ct);
        var revocado = await ResolverAsync(CuentaOrganizacional("revocado@dominio.edu.ar"), ct);
        var noVerificado = await ResolverAsync(
            CuentaOrganizacional("sinrol@dominio.edu.ar") with { EmailDominioVerificado = false }, ct);

        Assert.Equal(MotivoRechazoIngreso.NoRegistrado, noRegistrado.Motivo);
        Assert.Equal(MotivoRechazoIngreso.Inactivo, inactivo.Motivo);
        Assert.Equal(MotivoRechazoIngreso.SinRol, sinRol.Motivo);
        Assert.Equal(MotivoRechazoIngreso.SinRol, revocado.Motivo);
        Assert.Equal(MotivoRechazoIngreso.MailNoVerificado, noVerificado.Motivo);
        Assert.Equal(antes, await ContarEscriturasAsync(ct));
    }

    [Fact]
    public async Task Sesion_con_un_rol_en_varios_ambitos_expone_ese_rol_y_sus_permisos()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(
            "docente@dominio.edu.ar", activo: true, [RolDocente, RolDocente], ct);

        var sesion = await CrearSesion().ObtenerAsync(usuario, ct);

        Assert.NotNull(sesion);
        Assert.Equal("docente", sesion.RolCodigo);
        Assert.Equal("Docente", sesion.RolNombre);
        Assert.Equal(1, sesion.CantidadRoles);
        Assert.Equal(await PermisosDeRolAsync(RolDocente, ct), sesion.Permisos.Order().ToArray());
    }

    [Fact]
    public async Task Sesion_con_varios_roles_usa_el_primero_por_nombre()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(
            "varios@dominio.edu.ar", activo: true, [RolSecretaria, RolDocente], ct);

        var sesion = await CrearSesion().ObtenerAsync(usuario, ct);
        var resultado = await ResolverAsync(CuentaOrganizacional("varios@dominio.edu.ar"), ct);

        Assert.NotNull(sesion);
        Assert.Equal("docente", sesion.RolCodigo);
        Assert.Equal(2, sesion.CantidadRoles);
        Assert.Equal(await PermisosDeRolAsync(RolDocente, ct), sesion.Permisos.Order().ToArray());
        Assert.True(resultado.Aceptado);
    }

    [Fact]
    public async Task Sesion_de_usuario_desactivado_es_nula()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync("baja@dominio.edu.ar", activo: false, [RolDocente], ct);

        Assert.Null(await CrearSesion().ObtenerAsync(usuario, ct));
    }

    [Fact]
    public async Task Primer_ingreso_vincula_la_cuenta_y_registra_el_ingreso_con_el_usuario_como_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync("ada@dominio.edu.ar", activo: true, [RolSecretaria], ct);
        var cuenta = CuentaOrganizacional("ada@dominio.edu.ar");
        var personas = await ContarPersonasAsync(ct);

        var resultado = await ResolverAsync(cuenta, ct);
        await RegistrarAsync(usuario, cuenta, ct);

        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var vinculado = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario, ct);
        Assert.Equal(usuario, resultado.UsuarioId);
        Assert.Equal(cuenta.ObjectId, vinculado.AzureOid);
        Assert.Equal(cuenta.TenantId, vinculado.AzureTid);
        Assert.NotNull(vinculado.UltimoLoginEn);
        Assert.Equal(personas, await ContarPersonasAsync(ct));
        var cambio = await db.RegistrosDeCambio.AsNoTracking()
            .Where(r => r.NombreTabla == "users" && r.ClaveFila.Contains(usuario.ToString()))
            .OrderByDescending(r => r.Id)
            .FirstAsync(ct);
        Assert.Equal(usuario, cambio.CambiadoPor);
    }

    [Fact]
    public async Task Ingreso_posterior_se_reconoce_por_el_vinculo_aunque_cambien_mail_y_upn()
    {
        var ct = TestContext.Current.CancellationToken;
        var objeto = Guid.NewGuid();
        var usuario = await CrearUsuarioAsync(
            "upn-viejo@dominio.edu.ar", activo: true, [RolSecretaria], ct,
            cuenta: (TenantOrganizacion, objeto));

        var conMailNuevo = await ResolverAsync(
            new DatosCuentaMicrosoft(TenantOrganizacion, objeto, "mail-nuevo@dominio.edu.ar", true), ct);
        var sinMail = await ResolverAsync(
            new DatosCuentaMicrosoft(TenantOrganizacion, objeto, null, null), ct);

        Assert.Equal(usuario, conMailNuevo.UsuarioId);
        Assert.Equal(usuario, sinMail.UsuarioId);
    }

    [Fact]
    public async Task Otra_cuenta_con_el_mismo_mail_se_rechaza_sin_tocar_el_vinculo()
    {
        var ct = TestContext.Current.CancellationToken;
        var objeto = Guid.NewGuid();
        var usuario = await CrearUsuarioAsync(
            "ada@dominio.edu.ar", activo: true, [RolSecretaria], ct,
            cuenta: (TenantOrganizacion, objeto));
        var antes = await ContarEscriturasAsync(ct);

        var resultado = await ResolverAsync(CuentaOrganizacional("ada@dominio.edu.ar"), ct);

        await using var db = PostgresFixture.CrearIdentity(Cadena);
        Assert.Equal(MotivoRechazoIngreso.CuentaDistinta, resultado.Motivo);
        Assert.Equal(objeto, (await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario, ct)).AzureOid);
        Assert.Equal(antes, await ContarEscriturasAsync(ct));
    }

    private static DatosCuentaMicrosoft CuentaOrganizacional(string? email) =>
        new(TenantOrganizacion, Guid.NewGuid(), email, true);

    private async Task<ResultadoIngreso> ResolverAsync(DatosCuentaMicrosoft cuenta, CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var repositorio = new RepositorioIngreso(db);
        var servicio = new ServicioIngreso(
            repositorio,
            new ServicioSesion(repositorio),
            NullLogger<ServicioIngreso>.Instance);
        return await servicio.ResolverAsync(cuenta, ct);
    }

    private async Task RegistrarAsync(Guid usuario, DatosCuentaMicrosoft cuenta, CancellationToken ct)
    {
        // Como en el Host: el ingreso se registra con el propio usuario como actor de la auditoría.
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Cadena)
            .AddInterceptors(new AuditDbConnectionInterceptor(new UsuarioActual(usuario), new HttpContextAccessor()))
            .Options;
        await using var db = new IdentityDbContext(opciones);
        var repositorio = new RepositorioIngreso(db);
        await new ServicioIngreso(repositorio, new ServicioSesion(repositorio), NullLogger<ServicioIngreso>.Instance)
            .RegistrarIngresoAsync(usuario, cuenta, ct);
    }

    private async Task<int> ContarPersonasAsync(CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        return await db.Personas.CountAsync(ct);
    }

    private sealed class UsuarioActual(Guid id) : ICurrentUser
    {
        public string? UserId => id.ToString();
        public string? Email => null;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }

    private ServicioSesion CrearSesion() =>
        new(new RepositorioIngreso(PostgresFixture.CrearIdentity(Cadena)));

    private async Task<Guid> CrearUsuarioAsync(
        string upn,
        bool activo,
        Guid[] roles,
        CancellationToken ct,
        bool revocado = false,
        (Guid Tenant, Guid Objeto)? cuenta = null)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var ahora = DateTimeOffset.UtcNow;
        var carrera = new Carrera
        {
            Id = Guid.NewGuid(),
            Codigo = $"C-{Guid.NewGuid():N}"[..12],
            Nombre = "Carrera",
            Activo = true,
            CreadoEn = ahora,
        };
        var materias = roles.Select(_ => new Materia
        {
            Id = Guid.NewGuid(),
            Codigo = $"M-{Guid.NewGuid():N}"[..12],
            Nombre = "Materia",
            CarreraId = carrera.Id,
            Activo = true,
            CreadoEn = ahora,
        }).ToArray();
        db.Carreras.Add(carrera);
        db.Materias.AddRange(materias);
        var persona = new Persona
        {
            Id = Guid.NewGuid(),
            Documento = Guid.NewGuid().ToString("N")[..12],
            Nombre = "Nombre",
            Apellido = "Apellido",
            CreadoEn = ahora,
        };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            AzureOid = cuenta?.Objeto,
            AzureTid = cuenta?.Tenant,
            Upn = upn,
            NombreParaMostrar = "Nombre Apellido",
            Activo = activo,
            PersonaId = persona.Id,
            CreadoEn = ahora,
        };
        db.Personas.Add(persona);
        db.Usuarios.Add(usuario);
        for (var i = 0; i < roles.Length; i++)
        {
            var esDocente = roles[i] == RolDocente;
            db.UsuarioRoles.Add(new UsuarioRol
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                RolId = roles[i],
                MateriaId = esDocente ? materias[i].Id : null,
                CarreraId = esDocente ? materias[i].CarreraId : null,
                OtorgadoEn = ahora,
                CreadoEn = ahora,
                EliminadoEn = revocado ? ahora : null,
            });
        }

        await db.SaveChangesAsync(ct);
        return usuario.Id;
    }

    private async Task<string[]> PermisosDeRolAsync(Guid rolId, CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        return await db.RolPermisos.AsNoTracking()
            .Where(rp => rp.RolId == rolId)
            .Select(rp => rp.Permiso!.Codigo)
            .OrderBy(c => c)
            .ToArrayAsync(ct);
    }

    private async Task<(long Usuarios, long Cambios)> ContarEscriturasAsync(CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT (SELECT count(*) FROM identity.users), (SELECT count(*) FROM audit.change_log)",
            conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        await lector.ReadAsync(ct);
        return (lector.GetInt64(0), lector.GetInt64(1));
    }
}
