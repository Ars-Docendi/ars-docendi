using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Web;
using ArsDocendi.Host.Autenticacion;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// Composición de esquemas, rutas <c>/api/auth/*</c> y validación de la cookie en cada
/// solicitud. Microsoft se reemplaza por una configuración OIDC estática y la cookie se
/// emite con un sign-in que sólo existe en el host de pruebas.
/// </summary>
public sealed class AutenticacionMicrosoftTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "auth_ms")
{
    private readonly RelojAjustable _reloj = new(DateTimeOffset.UtcNow);
    private const string AutorizacionFalsa = "https://login.example.test/authorize";
    private const string RutaIngresoPruebas = "/__pruebas/ingresar";
    private const string HeaderIpPruebas = "X-Ip-Pruebas";
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");
    private static readonly Guid JefeSembrado = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid RolDecanato = Guid.Parse("a1000000-0000-4000-8000-000000000005");

    [Fact]
    public async Task Con_el_ingreso_deshabilitado_no_existen_las_rutas()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHost(microsoft: false, desarrollo: true);
        using var cliente = CrearCliente(host);

        using var sesion = await cliente.GetAsync("/api/auth/sesion", ct);
        using var login = await cliente.GetAsync("/api/auth/login", ct);

        Assert.Equal(HttpStatusCode.NotFound, sesion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
    }

    [Fact]
    public async Task Con_ambos_accesos_los_headers_de_desarrollo_siguen_autenticando()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost(microsoft: true, desarrollo: true);
        using var cliente = CrearCliente(host);

        using var sinSesion = await cliente.GetAsync("/api/designaciones/catalogos", ct);
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, JefeSembrado.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
        using var conHeaders = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
        Assert.Equal(HttpStatusCode.OK, conHeaders.StatusCode);
    }

    [Fact]
    public async Task Sesion_valida_expone_rol_y_permisos_y_autoriza_la_api()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);

        using var sinCookie = await cliente.GetAsync("/api/auth/sesion", ct);
        await IngresarAsync(cliente, usuario, ct);
        var sesion = await cliente.GetFromJsonAsync<SesionDto>("/api/auth/sesion", ct);
        using var catalogos = await cliente.GetAsync("/api/designaciones/catalogos", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, sinCookie.StatusCode);
        Assert.NotNull(sesion);
        Assert.Equal(usuario, sesion.UsuarioId);
        Assert.Equal("secretaria", sesion.RolCodigo);
        Assert.Equal("Secretaría Académica", sesion.RolNombre);
        Assert.NotEmpty(sesion.Permisos);
        Assert.Equal(HttpStatusCode.OK, catalogos.StatusCode);
    }

    [Fact]
    public async Task Usuario_desactivado_recibe_401_en_la_solicitud_siguiente()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);
        using var antes = await cliente.GetAsync("/api/auth/sesion", ct);

        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            await db.Usuarios.Where(u => u.Id == usuario)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false), ct);
        }

        using var despues = await cliente.GetAsync("/api/auth/sesion", ct);

        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task Cambio_de_rol_aplica_sin_volver_a_ingresar()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);

        await using (var db = PostgresFixture.CrearIdentity(Cadena))
        {
            await db.UsuarioRoles.Where(a => a.UsuarioId == usuario)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.EliminadoEn, DateTimeOffset.UtcNow), ct);
            db.UsuarioRoles.Add(NuevaAsignacion(usuario, RolDecanato));
            await db.SaveChangesAsync(ct);
        }

        var sesion = await cliente.GetFromJsonAsync<SesionDto>("/api/auth/sesion", ct);

        Assert.NotNull(sesion);
        Assert.Equal("decanato", sesion.RolCodigo);
    }

    [Fact]
    public async Task Cerrar_sesion_borra_la_cookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);
        var token = await ObtenerTokenAsync(cliente, ct);

        using var logout = await PostConTokenAsync(cliente, "/api/auth/logout", token, ct);
        using var sesion = await cliente.GetAsync("/api/auth/sesion", ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sesion.StatusCode);
    }

    [Fact]
    public async Task Sesion_por_http_local_entrega_el_token_sin_fallar()
    {
        // En local se corre por http://localhost y el navegador igual envía la cookie Secure.
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var https = CrearCliente(host);
        using var ingreso = await https.PostAsync($"{RutaIngresoPruebas}/{usuario}", null, ct);
        var cookie = ingreso.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(RegistroAutenticacion.NombreCookie, StringComparison.Ordinal))
            .Split(';')[0];
        using var http = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = false,
        });

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sesion");
        solicitud.Headers.Add("Cookie", cookie);
        using var respuesta = await http.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(respuesta.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith($"{AntifalsificacionSesion.CookieToken}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mutacion_por_cookie_sin_token_se_rechaza_sin_aplicar_cambios()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);
        await ObtenerTokenAsync(cliente, ct);

        using var sinToken = await cliente.PostAsync("/api/auth/logout", null, ct);
        using var tokenInvalido = await PostConTokenAsync(cliente, "/api/auth/logout", "inventado", ct);
        using var sesion = await cliente.GetAsync("/api/auth/sesion", ct);

        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tokenInvalido.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
    }

    [Fact]
    public async Task Mutacion_con_headers_de_desarrollo_no_requiere_token()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: true);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, JefeSembrado.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("172.18.0.5", "https://")]
    [InlineData("203.0.113.9", "http://")]
    public async Task Detras_del_proxy_solo_la_red_interna_puede_indicar_https(string ip, string esquema)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
        });

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/auth/login");
        solicitud.Headers.Add(HeaderIpPruebas, ip);
        solicitud.Headers.Add("X-Forwarded-Proto", "https");
        solicitud.Headers.Add("X-Forwarded-For", "198.51.100.7");
        using var respuesta = await cliente.SendAsync(solicitud, ct);

        var redirect = HttpUtility.ParseQueryString(respuesta.Headers.Location!.Query)["redirect_uri"];
        Assert.StartsWith($"{esquema}localhost{RegistroAutenticacion.RutaCallback}", redirect);
    }

    [Fact]
    public async Task Detras_del_proxy_el_token_antifalsificacion_sale_secure()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var https = CrearCliente(host);
        using var ingreso = await https.PostAsync($"{RutaIngresoPruebas}/{usuario}", null, ct);
        var cookie = ingreso.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(RegistroAutenticacion.NombreCookie, StringComparison.Ordinal))
            .Split(';')[0];
        using var http = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = false,
        });

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sesion");
        solicitud.Headers.Add("Cookie", cookie);
        solicitud.Headers.Add(HeaderIpPruebas, "172.18.0.5");
        solicitud.Headers.Add("X-Forwarded-Proto", "https");
        using var respuesta = await http.SendAsync(solicitud, ct);

        var token = respuesta.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{AntifalsificacionSesion.CookieToken}=", StringComparison.Ordinal));
        Assert.Contains("secure", token, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_sesion_sobrevive_a_reiniciar_el_backend_con_las_claves_persistidas()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        var claves = Directory.CreateTempSubdirectory("ars-claves-");
        try
        {
            string cookie;
            using (var antes = ConClaves(CrearHost(microsoft: true, desarrollo: false), claves.FullName))
            using (var cliente = CrearCliente(antes))
            {
                using var ingreso = await cliente.PostAsync($"{RutaIngresoPruebas}/{usuario}", null, ct);
                cookie = ingreso.Headers.GetValues("Set-Cookie")
                    .Single(c => c.StartsWith(RegistroAutenticacion.NombreCookie, StringComparison.Ordinal))
                    .Split(';')[0];
            }

            using var despues = ConClaves(CrearHost(microsoft: true, desarrollo: false), claves.FullName);
            using var nuevo = despues.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false,
            });
            using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sesion");
            solicitud.Headers.Add("Cookie", cookie);
            using var respuesta = await nuevo.SendAsync(solicitud, ct);

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.NotEmpty(claves.GetFiles("*.xml"));
        }
        finally
        {
            claves.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task La_sesion_vence_tras_el_periodo_de_inactividad()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);

        _reloj.Avanzar(TimeSpan.FromMinutes(59));
        using var antes = await cliente.GetAsync("/api/auth/sesion", ct);
        _reloj.Avanzar(TimeSpan.FromMinutes(61));
        using var despues = await cliente.GetAsync("/api/auth/sesion", ct);

        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task La_sesion_vence_en_la_duracion_maxima_aunque_haya_actividad()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuario = await CrearUsuarioAsync(RolSecretaria, ct);
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);
        await IngresarAsync(cliente, usuario, ct);

        // Una solicitud cada 40 minutos renueva la cookie por inactividad durante 9 h 20.
        for (var i = 0; i < 14; i++)
        {
            _reloj.Avanzar(TimeSpan.FromMinutes(40));
            using var activa = await cliente.GetAsync("/api/auth/sesion", ct);
            Assert.Equal(HttpStatusCode.OK, activa.StatusCode);
        }

        _reloj.Avanzar(TimeSpan.FromMinutes(41));
        using var vencida = await cliente.GetAsync("/api/auth/sesion", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, vencida.StatusCode);
    }

    [Theory]
    [InlineData("https://otro.sitio/robar", "/portal")]
    [InlineData("//otro.sitio", "/portal")]
    [InlineData("/\\otro.sitio", "/portal")]
    [InlineData("/designaciones/revision", "/designaciones/revision")]
    public async Task Login_desafia_a_Microsoft_y_solo_conserva_rutas_locales(string returnUrl, string esperado)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHost(microsoft: true, desarrollo: false);
        using var cliente = CrearCliente(host);

        using var respuesta = await cliente.GetAsync(
            $"/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", ct);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var destino = respuesta.Headers.Location!;
        Assert.StartsWith(AutorizacionFalsa, destino.ToString());
        var consulta = HttpUtility.ParseQueryString(destino.Query);
        Assert.Equal("code", consulta["response_type"]);
        Assert.Equal("S256", consulta["code_challenge_method"]);
        Assert.EndsWith(RegistroAutenticacion.RutaCallback, consulta["redirect_uri"]);
        var opciones = host.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(RegistroAutenticacion.EsquemaMicrosoft);
        var propiedades = opciones.StateDataFormat.Unprotect(consulta["state"]);
        Assert.Equal(esperado, propiedades?.RedirectUri);
    }

    private WebApplicationFactory<Program> CrearHost(bool microsoft, bool desarrollo) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", desarrollo.ToString());
            builder.UseSetting($"{AutenticacionMicrosoftOptions.Seccion}:Habilitada", microsoft.ToString());
            builder.UseSetting($"{AutenticacionMicrosoftOptions.Seccion}:ClientId", "cliente-pruebas");
            builder.UseSetting($"{AutenticacionMicrosoftOptions.Seccion}:ClientSecret", "secreto-pruebas");
            builder.ConfigureTestServices(servicios =>
            {
                servicios.Configure<OpenIdConnectOptions>(RegistroAutenticacion.EsquemaMicrosoft, o =>
                    o.Configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = AutorizacionFalsa,
                        Issuer = "https://login.example.test/v2.0",
                    });
                servicios.AddSingleton<IStartupFilter, FiltroIngresoPruebas>();
                servicios.AddSingleton<IStartupFilter, FiltroIpRemotaPruebas>();
                servicios.AddSingleton<TimeProvider>(_reloj);
            });
        });

    private static WebApplicationFactory<Program> ConClaves(WebApplicationFactory<Program> host, string directorio) =>
        host.WithWebHostBuilder(builder => builder.UseSetting("DataProtection:DirectorioClaves", directorio));

    private static HttpClient CrearCliente(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            // La cookie es Secure: sólo viaja por https.
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    private static async Task IngresarAsync(HttpClient cliente, Guid usuario, CancellationToken ct)
    {
        using var respuesta = await cliente.PostAsync($"{RutaIngresoPruebas}/{usuario}", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    /// <summary>Un GET autenticado por cookie entrega el token que el frontend devuelve en el header.</summary>
    private static async Task<string> ObtenerTokenAsync(HttpClient cliente, CancellationToken ct)
    {
        using var respuesta = await cliente.GetAsync("/api/auth/sesion", ct);
        var cookie = respuesta.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{AntifalsificacionSesion.CookieToken}=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie.Split(';')[0][(AntifalsificacionSesion.CookieToken.Length + 1)..]);
    }

    private static Task<HttpResponseMessage> PostConTokenAsync(
        HttpClient cliente, string ruta, string token, CancellationToken ct)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        solicitud.Headers.Add(AntifalsificacionSesion.HeaderToken, token);
        return cliente.SendAsync(solicitud, ct);
    }

    private async Task<Guid> CrearUsuarioAsync(Guid rol, CancellationToken ct)
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var ahora = DateTimeOffset.UtcNow;
        var persona = new Persona
        {
            Id = Guid.NewGuid(),
            Documento = Guid.NewGuid().ToString("N")[..12],
            Nombre = "Ada",
            Apellido = "Lovelace",
            CreadoEn = ahora,
        };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Upn = $"{Guid.NewGuid():N}@dominio.edu.ar",
            NombreParaMostrar = "Ada Lovelace",
            Activo = true,
            PersonaId = persona.Id,
            CreadoEn = ahora,
        };
        db.Personas.Add(persona);
        db.Usuarios.Add(usuario);
        db.UsuarioRoles.Add(NuevaAsignacion(usuario.Id, rol));

        await db.SaveChangesAsync(ct);
        return usuario.Id;
    }

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "AGENTS.md")))
        {
            directorio = directorio.Parent;
        }

        var sql = await File.ReadAllTextAsync(
            Path.Combine(directorio!.FullName, "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static UsuarioRol NuevaAsignacion(Guid usuario, Guid rol) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuario,
        RolId = rol,
        OtorgadoEn = DateTimeOffset.UtcNow,
        CreadoEn = DateTimeOffset.UtcNow,
    };

    /// <summary>Emite la cookie como lo haría un ingreso aceptado, sin pasar por Microsoft.</summary>
    private sealed class FiltroIngresoPruebas : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            app.Use(async (contexto, proximo) =>
            {
                if (!contexto.Request.Path.StartsWithSegments(RutaIngresoPruebas, out var resto))
                {
                    await proximo();
                    return;
                }

                var identidad = new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, resto.Value!.Trim('/')),
                        new Claim(
                            EventosSesion.ClaimInicio,
                            contexto.RequestServices.GetRequiredService<TimeProvider>()
                                .GetUtcNow().ToString("O", CultureInfo.InvariantCulture)),
                    ],
                    RegistroAutenticacion.EsquemaSesion);
                await contexto.SignInAsync(RegistroAutenticacion.EsquemaSesion, new ClaimsPrincipal(identidad));
                contexto.Response.StatusCode = StatusCodes.Status204NoContent;
            });
            siguiente(app);
        };
    }

    private sealed class RelojAjustable(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _ahora = inicio;

        public void Avanzar(TimeSpan lapso) => _ahora += lapso;

        public override DateTimeOffset GetUtcNow() => _ahora;
    }

    /// <summary>TestServer no tiene IP remota: la fija desde un header para simular quién llama.</summary>
    private sealed class FiltroIpRemotaPruebas : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            app.Use(async (contexto, proximo) =>
            {
                if (contexto.Request.Headers.TryGetValue(HeaderIpPruebas, out var ip))
                {
                    contexto.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip.ToString());
                }

                await proximo();
            });
            siguiente(app);
        };
    }
}
