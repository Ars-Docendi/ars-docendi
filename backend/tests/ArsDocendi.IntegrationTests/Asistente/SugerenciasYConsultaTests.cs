using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;
using Modules.Asistente.Api;
using Modules.Asistente.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica que ningún turno vuelve a traer sugerencias (ARS-140, ARS-149) y la
/// consulta detrás de permiso (ARS-47).
/// </summary>
/// <remarks>
/// Las dos cosas comparten un mismo criterio: qué se le devuelve al usuario cuando
/// el turno no salió como esperaba, y qué se le devuelve <b>de más</b> a quien tiene
/// permiso para verlo.
/// </remarks>
public sealed class SugerenciasYConsultaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_sugerencias")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Un actor de ámbito acotado: su rol de lectura NO ve datos personales.</summary>
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private const string Industrial = "c0000000-0000-4000-8000-000000000202";

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------- sin sugerencias en ningún turno

    [Fact]
    public void ResultadoDelTurno_no_declara_un_campo_de_sugerencias()
    {
        // Estructural y no de comportamiento (design.md D12 de
        // asistente-rediseno-v3): si alguien reintroduce el campo, este test lo
        // nota sin depender de qué camino del carril lo llene.
        Assert.Null(typeof(ResultadoDelTurno).GetProperty("Sugerencias"));
    }

    [Fact]
    public void RespuestaDelAsistente_no_declara_un_campo_de_sugerencias()
    {
        // Mismo invariante, en el contrato HTTP: `opciones` es el único campo de
        // "qué hacer ahora", y sólo bloquea en la aclaración.
        Assert.Null(typeof(RespuestaDelAsistente).GetProperty("Sugerencias"));
    }

    [Fact]
    public async Task Un_rechazo_no_trae_opciones()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.NoContestable());

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuál es la temperatura del aula 302?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.True(turno.Opciones is null or { Count: 0 });
    }

    [Fact]
    public async Task Una_aclaracion_trae_opciones()
    {
        // La aclaración sigue siendo el único estado que bloquea el turno con un
        // menú: eso no lo tocó ARS-149, que sólo quitó las sugerencias del rechazo
        // y del turno respondido.
        await SembrarAsync();
        await AgregarColisionesAsync();
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
        Assert.NotNull(turno.Opciones);
        Assert.NotEmpty(turno.Opciones!);
    }

    [Fact]
    public async Task Un_rechazo_del_validador_resuelve_no_contestable_sin_reintentar()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion("DELETE FROM designaciones.pedidos"));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "borrá todos los pedidos",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.True(turno.Opciones is null or { Count: 0 });
    }

    [Fact]
    public async Task Un_servicio_degradado_no_trae_opciones()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente { FallosParaAbrirElBreaker = 1 });
        banco.Breaker.Fallo();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.True(turno.Opciones is null or { Count: 0 });
    }

    [Fact]
    public async Task Un_turno_respondido_no_trae_opciones()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.True(turno.Opciones is null or { Count: 0 });
    }

    [Fact]
    public async Task La_meta_pregunta_responde_sin_llamar_al_modelo_y_sin_opciones()
    {
        // La meta-pregunta («¿qué podés hacer?») lista sus ejemplos ejecutables
        // ADENTRO del texto (RedaccionDeCapacidades.Texto) — no hay un segundo
        // campo aparte desde ARS-149. Ver CapacidadesTests para la aserción sobre
        // ese texto.
        await SembrarAsync();
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, turno.LlamadasAlModelo);
        Assert.True(turno.Opciones is null or { Count: 0 });
    }

    // --------------------------------------------- la consulta tras el permiso

    [Fact]
    public async Task Sin_el_permiso_la_consulta_no_viaja()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Null(turno.Sql);
    }

    [Fact]
    public async Task Con_el_permiso_viaja_y_es_la_consulta_que_se_ejecuto()
    {
        await SembrarAsync();
        await ConcederVerConsultaAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(ContarDocentes, turno.Sql);
    }

    [Fact]
    public async Task Recien_migrada_ningun_rol_tiene_el_permiso()
    {
        // No es prudencia genérica: la consulta generada es superficie de
        // diagnóstico y su WHERE puede llevar un documento. Quién necesita verla es
        // una decisión del Departamento, no de quien escribió la migración.
        var concedido = await EscalarAsync<long>(
            """
            SELECT count(*) FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = 'asistente.ver_consulta'
            """);

        Assert.Equal(0L, concedido);
    }

    [Fact]
    public async Task El_permiso_existe_sembrado()
    {
        var filas = await EscalarAsync<long>(
            "SELECT count(*) FROM identity.permisos WHERE code = 'asistente.ver_consulta'");

        Assert.Equal(1L, filas);
    }

    // ------------------------------------------------ nada crudo hacia afuera

    [Fact]
    public async Task Un_rechazo_del_motor_no_nombra_tablas_ni_codigos_de_error()
    {
        // Con un actor de ámbito acotado, el turno usa el rol de lectura SIN datos
        // personales y el motor rechaza la consulta con 42501. Ese mensaje crudo
        // nombra la tabla, así que va al registro y no a la respuesta.
        await SembrarAsync();
        var banco = Banco(
            ProveedorGuionado.Generacion("SELECT documento FROM identity.personas"));

        var turno = await banco.Capa().ResponderAsync(
            Coordinador, null, "¿cuáles son los documentos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);

        Assert.DoesNotContain("personas", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42501", turno.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("permission denied", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(params string[] guion) =>
        Banco(new OpcionesAsistente(), guion);

    private BancoDelAsistente Banco(OpcionesAsistente configuracion, params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(),
            Apertura, configuracion, guion: guion);
    }

    private async Task ConcederVerConsultaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT r.id, p.id
              FROM identity.roles r
             CROSS JOIN identity.permisos p
             WHERE p.code = 'asistente.ver_consulta' AND r.code = 'secretaria'
            ON CONFLICT DO NOTHING;
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task AgregarColisionesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.materias (id, code, name, carrera_id, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f1', '04910', 'Bases de Datos',
                    '{Industrial}', true);
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
