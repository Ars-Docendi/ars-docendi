using System.Security.Claims;
using ArsDocendi.Host.Autenticacion;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Ingreso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class EventosSesionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "eventos_sesion")
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("invalido", "11111111-2222-4333-8444-555555555555")]
    [InlineData("11111111-2222-4333-8444-555555555555", null)]
    public async Task Principal_ausente_o_incompleto_rechaza_el_ingreso_sin_consultar_servicios(
        string? tenant, string? objeto)
    {
        // Sin ServicioIngreso registrado: un principal inválido debe rechazarse antes de consultarlo.
        await using var servicios = new ServiceCollection().BuildServiceProvider();
        var contexto = CrearContexto(servicios);
        if (tenant is not null)
        {
            var claims = new List<Claim> { new("tid", tenant) };
            if (objeto is not null) claims.Add(new Claim("oid", objeto));
            contexto.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Microsoft"));
        }

        await EventosSesion.AceptarIngresoAsync(contexto);

        Assert.True(contexto.Result?.Handled);
        Assert.Equal(StatusCodes.Status302Found, contexto.Response.StatusCode);
        Assert.Equal("/login?error=forbidden", contexto.Response.Headers.Location.ToString());
        Assert.False(contexto.HttpContext.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Ingreso_aceptado_conserva_el_usuario_y_registra_el_vinculo()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var objeto = Guid.NewGuid();
        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            db.Usuarios.Add(new Usuario
            {
                Id = usuario,
                Upn = "ada@dominio.edu.ar",
                NombreParaMostrar = "Ada",
                Activo = true,
                CreadoEn = DateTimeOffset.UtcNow,
            });
            db.UsuarioRoles.Add(new UsuarioRol
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuario,
                RolId = Guid.Parse("a1000000-0000-4000-8000-000000000004"),
                OtorgadoEn = DateTimeOffset.UtcNow,
                CreadoEn = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        await using var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddDbContext<IdentityDbContext>(o => o.UseNpgsql(Cadena))
            .AddScoped<RepositorioIngreso>()
            .AddScoped<ServicioSesion>()
            .AddScoped<ServicioIngreso>()
            .BuildServiceProvider();
        await using var alcance = servicios.CreateAsyncScope();
        var contexto = CrearContexto(alcance.ServiceProvider);
        contexto.Principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new("tid", tenant.ToString()), new("oid", objeto.ToString()),
                new("email", "ada@dominio.edu.ar"), new("xms_edov", "true")], "Microsoft"));

        await EventosSesion.AceptarIngresoAsync(contexto);

        Assert.Null(contexto.Result);
        Assert.Equal(usuario.ToString(), contexto.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(RegistroAutenticacion.EsquemaSesion, contexto.Principal.Identity?.AuthenticationType);
        Assert.NotNull(contexto.Principal.FindFirstValue(EventosSesion.ClaimInicio));
        Assert.Same(contexto.Principal, contexto.HttpContext.User);
        await using var actualizado = PostgresFixture.CrearIdentity(Cadena);
        var vinculado = await actualizado.Usuarios.SingleAsync(u => u.Id == usuario, ct);
        Assert.Equal(tenant, vinculado.AzureTid);
        Assert.Equal(objeto, vinculado.AzureOid);
        Assert.NotNull(vinculado.UltimoLoginEn);
    }

    private static TokenValidatedContext CrearContexto(IServiceProvider servicios) => new(
        new DefaultHttpContext { RequestServices = servicios },
        new AuthenticationScheme("Microsoft", null, typeof(OpenIdConnectHandler)),
        new OpenIdConnectOptions(), new ClaimsPrincipal(), new AuthenticationProperties())
        { Principal = null };
}
