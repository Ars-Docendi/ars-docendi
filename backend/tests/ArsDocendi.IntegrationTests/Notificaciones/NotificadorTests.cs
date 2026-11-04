using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notificaciones.Contracts;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class NotificadorTests(PostgresFixture postgres)
    : BaseNotificaciones(postgres, "notificador")
{
    private static readonly Guid PersonaSinUsuario = Guid.Parse("d0000000-0000-4000-8000-000000000010");
    private static readonly Guid PersonaDelDocente = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia101 = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Materia102 = Guid.Parse("70000000-0000-4000-8000-000000000102");

    private static AvisoNuevo Aviso(string? rolDestino = null) => new(
        TiposEventoNotificacion.TareaAsignada, TiposEntidadNotificacion.Tarea, Guid.NewGuid(), rolDestino,
        "Titulo", "Mensaje", new Dictionary<string, object?> { ["numero"] = "T-0001", ["extra"] = null });

    private static async Task NotificarAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host,
        AvisoNuevo aviso, params DestinoNotificacion[] destinos)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INotificador>().NotificarAsync(aviso, destinos, Ct);
    }

    [Fact]
    public async Task Resuelve_usuario_persona_y_rol_sin_duplicar_y_guarda_el_snapshot()
    {
        await SembrarAsync();
        using var host = CrearHost();
        var aviso = Aviso("jefe_catedra");

        await NotificarAsync(host, aviso,
            new DestinoNotificacion.Usuario(Docente),
            new DestinoNotificacion.Persona(PersonaDelDocente), // same usuario through its persona
            new DestinoNotificacion.RolEnAmbito("jefe_catedra", MateriaId: Materia101));

        var filas = await NotificacionesAsync();
        Assert.Equal(new[] { Docente, Jefe }.OrderBy(x => x), filas.Select(f => f.Destinatario).OrderBy(x => x));
        Assert.All(filas, f =>
        {
            Assert.Equal("jefe_catedra", f.RolDestino);
            Assert.Equal(aviso.EntidadId, f.EntidadId);
            Assert.Equal("Titulo", f.Titulo);
            Assert.Null(f.LeidaEn);
            Assert.Contains("T-0001", f.Contexto);
        });
    }

    [Fact]
    public async Task Varios_jefes_de_una_materia_reciben_cada_uno_su_fila_con_lectura_independiente()
    {
        await SembrarAsync();
        var otroJefe = Guid.NewGuid();
        await EjecutarAsync($"""
            INSERT INTO identity.users(id, upn, display_name) VALUES ('{otroJefe}', 'otro.jefe@example.test', 'Otro Jefe');
            INSERT INTO identity.user_roles(user_id, role_id, materia_id)
            VALUES ('{otroJefe}', 'a1000000-0000-4000-8000-000000000002', '{Materia101}');
            """);
        using var host = CrearHost();

        await NotificarAsync(host, Aviso("jefe_catedra"), new DestinoNotificacion.RolEnAmbito("jefe_catedra", Materia101));

        var filas = await NotificacionesAsync();
        Assert.Equal(2, filas.Count);
        Assert.Equal(new[] { Jefe, otroJefe }.OrderBy(x => x), filas.Select(f => f.Destinatario).OrderBy(x => x));

        using var cliente = ClienteDe(host, Jefe, "jefe_catedra");
        var propia = filas.Single(f => f.Destinatario == Jefe);
        Assert.True((await cliente.PatchAsync($"/api/notificaciones/{propia.Id}/leida", null, Ct)).IsSuccessStatusCode);
        var luego = await NotificacionesAsync();
        Assert.NotNull(luego.Single(f => f.Destinatario == Jefe).LeidaEn);
        Assert.Null(luego.Single(f => f.Destinatario == otroJefe).LeidaEn);
    }

    [Fact]
    public async Task El_alcance_del_rol_se_respeta_y_los_jefes_de_otra_materia_no_reciben()
    {
        await SembrarAsync();
        using var host = CrearHost();

        // The seeded jefe holds 101, 102 and 103; nobody holds a materia that does not exist.
        await NotificarAsync(host, Aviso(), new DestinoNotificacion.RolEnAmbito("jefe_catedra", Guid.NewGuid()));
        Assert.Empty(await NotificacionesAsync());

        await NotificarAsync(host, Aviso(), new DestinoNotificacion.RolEnAmbito("jefe_catedra", Materia102));
        Assert.Equal(Jefe, Assert.Single(await NotificacionesAsync()).Destinatario);
    }

    [Fact]
    public async Task Una_persona_sin_usuario_se_omite_sin_error_y_no_frena_a_los_demas()
    {
        await SembrarAsync();
        using var host = CrearHost();

        await NotificarAsync(host, Aviso(),
            new DestinoNotificacion.Persona(PersonaSinUsuario), new DestinoNotificacion.Usuario(Docente));

        Assert.Equal(Docente, Assert.Single(await NotificacionesAsync()).Destinatario);
    }

    [Fact]
    public async Task Un_usuario_inactivo_nombrado_directamente_o_por_persona_o_por_rol_no_recibe()
    {
        await SembrarAsync();
        await EjecutarAsync($"UPDATE identity.users SET is_active = false WHERE id IN ('{Docente}', '{Secretaria}', '{Jefe}')");
        using var host = CrearHost();

        await NotificarAsync(host, Aviso(),
            new DestinoNotificacion.Usuario(Docente),
            new DestinoNotificacion.Persona(PersonaDelDocente),
            new DestinoNotificacion.RolEnAmbito("secretaria"),
            new DestinoNotificacion.RolEnAmbito("jefe_catedra", Materia101),
            new DestinoNotificacion.Usuario(Coordinador));

        Assert.Equal(Coordinador, Assert.Single(await NotificacionesAsync()).Destinatario);
    }

    [Fact]
    public async Task Si_no_hay_nadie_a_quien_avisar_no_escribe_nada()
    {
        await SembrarAsync();
        using var host = CrearHost();
        await NotificarAsync(host, Aviso());
        await NotificarAsync(host, Aviso(), new DestinoNotificacion.RolEnAmbito("secretaria", Guid.NewGuid()));
        Assert.Empty(await NotificacionesAsync());
    }

    [Fact]
    public async Task Un_fallo_al_escribir_no_llega_a_quien_llama()
    {
        await SembrarAsync();
        await EjecutarAsync("DROP TABLE notificaciones.notificaciones");
        using var host = CrearHost();

        // No exception: the contract is best effort.
        await NotificarAsync(host, Aviso(), new DestinoNotificacion.Usuario(Docente));
        // Nor with a tipo_entidad the CHECK rejects.
        await EjecutarAsync("""
            CREATE TABLE notificaciones.notificaciones (id uuid PRIMARY KEY, destinatario_usuario_id uuid, rol_destino text,
              tipo_evento text, tipo_entidad text CHECK (tipo_entidad = 'X'), entidad_id uuid, titulo text, mensaje text,
              contexto jsonb, leida_en timestamptz, creada_en timestamptz)
            """);
        await NotificarAsync(host, Aviso(), new DestinoNotificacion.Usuario(Docente));
    }

    [Fact]
    public async Task La_tabla_rechaza_filas_incoherentes()
    {
        await SembrarAsync();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => EjecutarAsync($"""
            INSERT INTO notificaciones.notificaciones(destinatario_usuario_id, tipo_evento, tipo_entidad, entidad_id, titulo, mensaje)
            VALUES ('{Docente}', 'X', 'OTRA', gen_random_uuid(), 'T', 'M')
            """));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => EjecutarAsync($"""
            INSERT INTO notificaciones.notificaciones(destinatario_usuario_id, tipo_evento, tipo_entidad, entidad_id, titulo, mensaje)
            VALUES ('{Docente}', 'X', 'TAREA', gen_random_uuid(), '  ', 'M')
            """));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => EjecutarAsync($"""
            INSERT INTO notificaciones.notificaciones(destinatario_usuario_id, tipo_evento, tipo_entidad, entidad_id, titulo, mensaje)
            VALUES ('{Guid.NewGuid()}', 'X', 'TAREA', gen_random_uuid(), 'T', 'M')
            """));
    }
}
