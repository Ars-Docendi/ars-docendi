using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la puerta del turno: permiso, idempotencia y forma de la respuesta.
/// </summary>
/// <remarks>
/// Va por HTTP de verdad y no llamando al servicio: lo que se prueba acá es
/// exactamente lo que el borde agrega —autorización, cabeceras, códigos de estado y
/// la traducción al contrato— y nada de eso se ejercita invocando la capa.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class EndpointDeConsultasTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_endpoint")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    private const string Ruta = "/api/asistente/consultas";

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------------------------- el permiso

    [Fact]
    public async Task Sin_identidad_el_endpoint_rechaza()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_el_permiso_del_asistente_el_endpoint_rechaza()
    {
        // `docente` es el único rol de sistema al que la siembra NO le concede
        // `asistente.consultar`. La exclusión es provisional y se revierte desde
        // /membresia-roles, sin migración.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Docente, "docente");

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        // Y no gastó nada: el rechazo es antes del pipeline.
        Assert.Equal(0, proveedor.Llamadas);
    }

    // -------------------------------------------------------- la idempotencia

    [Fact]
    public async Task Sin_la_clave_de_idempotencia_el_pedido_se_rechaza_nombrandola()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?", clave: null);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(
            AsistenteController.CabeceraDeIdempotencia, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_clave_en_blanco_no_cuenta_como_clave()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        using var respuesta = await Preguntar(cliente, "¿cuántos docentes hay?", clave: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_pedido_repetido_devuelve_lo_mismo_sin_volver_a_llamar_al_proveedor()
    {
        // Cada turno cuesta dos o tres llamadas al modelo: un doble submit se factura
        // completo dos veces. Se cuenta contra el proveedor y no contra el cuerpo de
        // la respuesta, porque devolver lo mismo habiendo gastado igual no resuelve
        // nada de lo que este requisito existe para resolver.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var clave = Guid.NewGuid().ToString();

        var primera = await LeerAsync(await Preguntar(cliente, "¿cuántos docentes hay?", clave));
        var gastadas = proveedor.Llamadas;

        var segunda = await LeerAsync(await Preguntar(cliente, "¿cuántos docentes hay?", clave));

        Assert.Equal(primera.Respuesta, segunda.Respuesta);
        Assert.Equal(primera.Hilo, segunda.Hilo);
        Assert.Equal(gastadas, proveedor.Llamadas);
    }

    [Fact]
    public async Task Una_clave_distinta_si_procesa()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString());
        var gastadas = proveedor.Llamadas;

        await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString());

        Assert.True(proveedor.Llamadas > gastadas);
    }

    [Fact]
    public async Task La_clave_de_un_actor_no_le_sirve_a_otro()
    {
        // Sin acotar por actor, la clave de un usuario le devolvería a otro una
        // respuesta calculada con el alcance del primero: una fuga trivial de
        // disparar y difícil de notar, porque el segundo recibe algo que parece
        // correcto.
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);
        var clave = Guid.NewGuid().ToString();

        using var deSecretaria = host.CreateClient();
        Autenticar(deSecretaria, Secretaria, "secretaria");
        await Preguntar(deSecretaria, "¿cuántos docentes hay?", clave);
        var gastadas = proveedor.Llamadas;

        using var deCoordinador = host.CreateClient();
        Autenticar(deCoordinador, Coordinador, "coordinador_carrera");
        var segunda = await LeerAsync(
            await Preguntar(deCoordinador, "¿cuántos docentes hay?", clave));

        Assert.True(proveedor.Llamadas > gastadas,
            "La clave del primer actor le devolvió al segundo una respuesta ya calculada.");
        Assert.NotEqual(Guid.Empty, segunda.Hilo);
    }

    [Fact]
    public void La_caducidad_de_la_clave_devuelve_a_procesar_el_turno()
    {
        // La caché es en memoria y con expiración corta, así que la caducidad se
        // prueba donde vive el reloj y no por HTTP: mover el tiempo de un Host
        // levantado exigiría inyectarle un TimeProvider a todo el proceso.
        var reloj = new RelojFijo(new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero));
        var idempotencia = new IdempotenciaEnMemoria(
            Microsoft.Extensions.Options.Options.Create(
                new OpcionesAsistente { VigenciaDeIdempotenciaMinutos = 5 }),
            reloj);

        var turno = TurnoCualquiera();
        idempotencia.Guardar(Secretaria, "k", turno);

        Assert.NotNull(idempotencia.Recordar(Secretaria, "k"));

        reloj.Avanzar(TimeSpan.FromMinutes(6));

        Assert.Null(idempotencia.Recordar(Secretaria, "k"));
    }

    [Fact]
    public void La_idempotencia_no_persiste_ninguna_fila()
    {
        // El requisito no es «que ande»: es que NO se copie
        // designaciones.idempotencia_comandos, que guarda el cuerpo completo de la
        // respuesta HTTP — exactamente lo que este módulo decidió no persistir.
        var tipo = typeof(IdempotenciaEnMemoria);

        Assert.DoesNotContain(
            tipo.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType.Name),
            nombre => nombre.Contains("Cadena", StringComparison.Ordinal));
    }

    // ------------------------------------------------------- lo que devuelve

    [Fact]
    public async Task La_respuesta_trae_el_estado_el_hilo_y_las_metricas()
    {
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await LeerAsync(
            await Preguntar(cliente, "¿cuántos docentes hay?", Guid.NewGuid().ToString()));

        Assert.Equal("respondida", cuerpo.Estado);
        Assert.NotEqual(Guid.Empty, cuerpo.Hilo);
        Assert.Equal(2, cuerpo.Metricas.LlamadasAlModelo);
        Assert.Null(cuerpo.Sql);
    }

    [Fact]
    public async Task El_estado_del_contrato_no_es_el_nombre_del_enum()
    {
        // El nombre del enum es un detalle interno del backend: renombrarlo no puede
        // romper a los clientes en silencio.
        await SembrarAsync();
        using var host = CrearHost(out _);
        using var cliente = host.CreateClient();
        Autenticar(cliente, Secretaria, "secretaria");

        var cuerpo = await LeerAsync(
            await Preguntar(cliente, "hola", Guid.NewGuid().ToString()));

        Assert.Equal("respondida", cuerpo.Estado);
        Assert.DoesNotContain("Respondida", cuerpo.Estado, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_endpoint_de_capacidades_responde_por_actor()
    {
        await SembrarAsync();
        using var host = CrearHost(out var proveedor);

        using var deSecretaria = host.CreateClient();
        Autenticar(deSecretaria, Secretaria, "secretaria");

        using var deCoordinador = host.CreateClient();
        Autenticar(deCoordinador, Coordinador, "coordinador_carrera");

        var ct = TestContext.Current.CancellationToken;
        var global = await deSecretaria.GetFromJsonAsync<CapacidadesDto>(
            "/api/asistente/capacidades", ct);
        var acotado = await deCoordinador.GetFromJsonAsync<CapacidadesDto>(
            "/api/asistente/capacidades", ct);

        Assert.NotNull(global);
        Assert.NotNull(acotado);
        Assert.True(global.Columnas > acotado.Columnas);
        Assert.NotEqual(global.Alcance, acotado.Alcance);
        Assert.NotEmpty(acotado.Ejemplos);

        // Cero tokens: el catálogo sale de la base, no del modelo.
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task El_actor_sale_de_la_sesion_y_no_del_cuerpo_del_pedido()
    {
        // El pedido no tiene dónde poner un actor, y ése es el punto: un
        // identificador tomado del cuerpo sería un selector de alcance controlado por
        // el cliente.
        Assert.DoesNotContain(
            typeof(ConsultaDelAsistente).GetProperties().Select(p => p.Name),
            nombre => nombre.Contains("Actor", StringComparison.OrdinalIgnoreCase)
                || nombre.Contains("Usuario", StringComparison.OrdinalIgnoreCase));

        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ apoyo

    private static Modules.Asistente.Application.ResultadoDelTurno TurnoCualquiera() =>
        new(Modules.Asistente.Application.EstadoDelTurno.Respondida,
            "listo",
            Razonamiento: string.Empty,
            PreguntaInterpretada: null,
            [],
            [],
            Truncado: false,
            [],
            "consulta_simple",
            LlamadasAlModelo: 0,
            Guid.NewGuid());

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private static async Task<HttpResponseMessage> Preguntar(
        HttpClient cliente, string mensaje, string? clave = "clave-de-prueba")
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, Ruta)
        {
            Content = JsonContent.Create(new ConsultaDelAsistente(mensaje, null)),
        };

        if (clave is not null)
        {
            pedido.Headers.TryAddWithoutValidation(
                AsistenteController.CabeceraDeIdempotencia, clave);
        }

        return await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
    }

    private static async Task<RespuestaDelAsistente> LeerAsync(HttpResponseMessage respuesta)
    {
        var ct = TestContext.Current.CancellationToken;
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.True(respuesta.IsSuccessStatusCode, cuerpo);

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaDelAsistente>(ct))!;
    }

    private WebApplicationFactory<Program> CrearHost(out ProveedorGuionado proveedor)
    {
        // Guion largo: cada turno del carril consume generación + redacción, y el
        // proveedor guionado repite su última respuesta al agotarse. Un guion corto
        // haría que un turno de más terminara no contestable por una razón que no es
        // la que el test mide.
        var guion = Enumerable.Range(0, 12).SelectMany(_ => new[]
        {
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes designados.",
        }).ToArray();

        var guionado = new ProveedorGuionado(guion);
        proveedor = guionado;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
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

            // La cuota apagada: acá se mide la puerta, no el presupuesto.
            builder.UseSetting(
                $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.CupoDeLlamadasPorActor)}",
                "0");

            // El proveedor guionado reemplaza al simulado: contar sus llamadas es lo
            // único que prueba de verdad que la idempotencia no volvió a gastar.
            builder.ConfigureTestServices(servicios =>
                servicios.AddSingleton(new ProveedorBase(guionado)));
        });
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
