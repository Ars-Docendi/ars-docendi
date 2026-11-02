using System.Net;
using System.Net.Http.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Modules.Designaciones.Api;
using Modules.Notificaciones.Contracts;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class EmisionDesignacionesTests(PostgresFixture postgres)
    : BaseNotificaciones(postgres, "emision_designaciones")
{
    private static readonly Guid EnCoordinacion = Guid.Parse("d5000000-0000-4000-8000-000000000002");
    private static readonly Guid EnSecretaria = Guid.Parse("d5000000-0000-4000-8000-000000000003");
    private static readonly Guid EnDecanato = Guid.Parse("d5000000-0000-4000-8000-000000000004");
    private const string DecanatoRolId = "a1000000-0000-4000-8000-000000000005";

    /// <summary>The seeded request in Coordinación has no attachments; an alta needs them to be accepted.</summary>
    private Task CompletarDocumentacionAsync() => EjecutarAsync($"""
        INSERT INTO designaciones.pedido_adjuntos (pedido_id, tipo, nombre, uri) VALUES
            ('{EnCoordinacion}', 'cv', 'cv.pdf', 'synthetic://pedidos/9002/cv'),
            ('{EnCoordinacion}', 'dni_frente', 'f.pdf', 'synthetic://pedidos/9002/f'),
            ('{EnCoordinacion}', 'dni_dorso', 'd.pdf', 'synthetic://pedidos/9002/d')
        """);

    private static Task<HttpResponseMessage> AccionAsync(
        HttpClient cliente, Guid pedido, string accion, string? comentario = null, Guid? clave = null)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"/api/designaciones/pedidos/{pedido}/{accion}")
        {
            Content = JsonContent.Create(new AccionPedidoDto { Comentario = comentario }),
        };
        peticion.Headers.Add("Idempotency-Key", (clave ?? Guid.NewGuid()).ToString());
        return cliente.SendAsync(peticion, Ct);
    }

    [Fact]
    public async Task Aceptar_en_coordinacion_avisa_a_secretaria_con_el_texto_del_modal()
    {
        await SembrarAsync();
        await CompletarDocumentacionAsync();
        using var host = CrearHost();
        using var coordinador = ClienteDe(host, Coordinador, "coordinador_carrera");

        var respuesta = await AccionAsync(coordinador, EnCoordinacion, "aceptar", "Conforme");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Secretaria, aviso.Destinatario);
        Assert.Equal("secretaria", aviso.RolDestino);
        Assert.Equal("PEDIDO_PARA_REVISAR", aviso.TipoEvento);
        Assert.Equal("PEDIDO_DESIGNACION", aviso.TipoEntidad);
        Assert.Equal(EnCoordinacion, aviso.EntidadId);
        Assert.Equal("Pedido para revisar", aviso.Titulo);
        Assert.StartsWith("Pasó a Secretaría el pedido 2026-9002 (el alta del docente de Prof. Suárez, Valeria · ", aviso.Mensaje);
        Assert.EndsWith("). Esperando tu revisión.", aviso.Mensaje);
        Assert.Equal("en_revision_secretaria", ContextoDe(aviso).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Aceptar_en_secretaria_avisa_a_decanato_y_nunca_al_actor_aunque_tenga_ese_rol()
    {
        await SembrarAsync();
        await EjecutarAsync($"INSERT INTO identity.user_roles(user_id, role_id) VALUES ('{Secretaria}', '{DecanatoRolId}')");
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");

        Assert.Equal(HttpStatusCode.OK, (await AccionAsync(secretaria, EnSecretaria, "aceptar")).StatusCode);

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Decanato, aviso.Destinatario);
        Assert.Equal("decanato", aviso.RolDestino);
        Assert.StartsWith("Pasó a Decanato el pedido 2026-9003", aviso.Mensaje);
    }

    [Fact]
    public async Task Repetir_la_misma_clave_de_idempotencia_no_vuelve_a_avisar()
    {
        await SembrarAsync();
        await CompletarDocumentacionAsync();
        using var host = CrearHost();
        using var coordinador = ClienteDe(host, Coordinador, "coordinador_carrera");
        var clave = Guid.NewGuid();

        await AccionAsync(coordinador, EnCoordinacion, "aceptar", clave: clave);
        var repetida = await AccionAsync(coordinador, EnCoordinacion, "aceptar", clave: clave);

        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Single(await NotificacionesAsync());
    }

    [Fact]
    public async Task Rechazar_avisa_a_todos_los_jefes_de_la_materia_cada_uno_con_su_fila()
    {
        await SembrarAsync();
        var otroJefe = Guid.NewGuid();
        var jefeDeOtraMateria = Guid.NewGuid();
        await EjecutarAsync($"""
            INSERT INTO identity.users(id, upn, display_name) VALUES
              ('{otroJefe}', 'jefe2@example.test', 'Segundo Jefe'),
              ('{jefeDeOtraMateria}', 'jefe3@example.test', 'Jefe de otra materia');
            INSERT INTO identity.user_roles(user_id, role_id, materia_id) VALUES
              ('{otroJefe}', 'a1000000-0000-4000-8000-000000000002', '{MateriaDelDocente}'),
              ('{jefeDeOtraMateria}', 'a1000000-0000-4000-8000-000000000002', '70000000-0000-4000-8000-000000000104');
            """);
        using var host = CrearHost();
        using var coordinador = ClienteDe(host, Coordinador, "coordinador_carrera");

        Assert.Equal(HttpStatusCode.OK, (await AccionAsync(coordinador, EnCoordinacion, "rechazar", "Documentación incompleta")).StatusCode);

        var avisos = await NotificacionesAsync();
        Assert.Equal(new[] { Jefe, otroJefe }.OrderBy(x => x), avisos.Select(a => a.Destinatario).OrderBy(x => x));
        Assert.All(avisos, a =>
        {
            Assert.Equal("jefe_catedra", a.RolDestino);
            Assert.Equal("Pedido rechazado", a.Titulo);
            Assert.EndsWith("). Justificativo: Documentación incompleta", a.Mensaje);
            Assert.StartsWith("Se rechazó el pedido 2026-9002 (", a.Mensaje);
        });
    }

    [Fact]
    public async Task Devolver_desde_coordinacion_avisa_a_los_jefes_de_la_materia()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var coordinador = ClienteDe(host, Coordinador, "coordinador_carrera");

        await AccionAsync(coordinador, EnCoordinacion, "devolver", "Falta el CV");

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Jefe, aviso.Destinatario);
        Assert.Equal("jefe_catedra", aviso.RolDestino);
        Assert.Equal("Pedido devuelto", aviso.Titulo);
        Assert.EndsWith("para que lo corrija Jefe de Cátedra. Comentario: Falta el CV", aviso.Mensaje);
    }

    [Fact]
    public async Task Devolver_desde_secretaria_avisa_a_los_coordinadores_de_la_carrera()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");

        await AccionAsync(secretaria, EnSecretaria, "devolver", "Revisar la dedicación");

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Coordinador, aviso.Destinatario);
        Assert.Equal("coordinador_carrera", aviso.RolDestino);
        Assert.Contains("para que lo corrija Coordinador. Comentario: Revisar la dedicación", aviso.Mensaje);
    }

    [Fact]
    public async Task Devolver_desde_decanato_avisa_a_secretaria_en_todo_el_departamento()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var decanato = ClienteDe(host, Decanato, "decanato");

        await AccionAsync(decanato, EnDecanato, "devolver", "Falta aval");

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Secretaria, aviso.Destinatario);
        Assert.Equal("secretaria", aviso.RolDestino);
        Assert.Contains("para que lo corrija Secretaría. Comentario: Falta aval", aviso.Mensaje);
    }

    [Fact]
    public async Task Enviar_y_las_acciones_que_no_dejan_a_nadie_pendiente_no_avisan()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var jefe = ClienteDe(host, Jefe, "jefe_catedra");
        var borrador = Guid.Parse("d5000000-0000-4000-8000-000000000001");

        // Whatever the outcome of the send (it may fail on completeness), it must not notify.
        await AccionAsync(jefe, borrador, "enviar");

        Assert.Empty(await NotificacionesAsync());
    }

    [Fact]
    public async Task Si_la_emision_falla_la_transicion_queda_confirmada()
    {
        await SembrarAsync();
        await CompletarDocumentacionAsync();
        await EjecutarAsync("DROP TABLE notificaciones.notificaciones");
        using var host = CrearHost();
        using var coordinador = ClienteDe(host, Coordinador, "coordinador_carrera");

        var respuesta = await AccionAsync(coordinador, EnCoordinacion, "aceptar");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(1, await EscalarAsync(
            "SELECT count(*) FROM designaciones.pedidos WHERE id = @id AND estado = 'en_revision_secretaria'", ("id", EnCoordinacion)));
    }

    [Fact]
    public async Task Un_notificador_que_lanza_no_deshace_la_transicion()
    {
        await SembrarAsync();
        using var host = CrearHost(servicios => servicios.AddScoped<INotificador, NotificadorQueLanza>());
        using var coordinador = ClienteDe(host, Coordinador, "coordinador_carrera");

        var respuesta = await AccionAsync(coordinador, EnCoordinacion, "devolver", "Falta algo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(1, await EscalarAsync(
            "SELECT count(*) FROM designaciones.pedidos WHERE id = @id AND estado = 'devuelto'", ("id", EnCoordinacion)));
        Assert.Empty(await NotificacionesAsync());
    }

    private sealed class NotificadorQueLanza : INotificador
    {
        public Task NotificarAsync(AvisoNuevo aviso, IReadOnlyCollection<DestinoNotificacion> destinos, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
