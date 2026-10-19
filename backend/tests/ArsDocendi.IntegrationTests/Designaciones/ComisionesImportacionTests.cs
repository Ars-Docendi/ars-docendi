using System.Net;
using ArsDocendi.IntegrationTests.Designaciones.Guarani;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Api;
using Modules.Designaciones.Infrastructure.Guarani;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class ComisionesImportacionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "comisiones_sga")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid CarlaLopez = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid GustavoRuiz = Guid.Parse("d0000000-0000-4000-8000-000000000002");
    private static readonly Guid Informatica = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid IngenieriaSoftware = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Importacion_guarda_el_snapshot_y_vincula_por_clave_natural()
    {
        await SembrarAsync();

        var resultado = await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);

        Assert.Equal(3, resultado.Cantidad);
        Assert.Equal(2026, resultado.AnioAcademico);
        Assert.Equal("2do Cuatrimestre", resultado.PeriodoLectivoNombre);
        Assert.Equal(1, resultado.DocentesSinVincular);
        Assert.Equal(1, resultado.CarrerasSinVincular);

        var comisiones = await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct);
        var c9001 = comisiones.Single(c => c.Id == "9001");
        Assert.Equal(IngenieriaSoftware, c9001.Materia.Id);
        Assert.Equal(Informatica, c9001.Carreras.Single(k => k.Codigo == "INF").Id);
        Assert.Null(c9001.Carreras.Single(k => k.Codigo == "ZZZ").Id);
        Assert.Equal(CarlaLopez, c9001.Docentes.Single(d => d.Nombre == "LOPEZ CARLA").PersonaId);
        Assert.Null(c9001.Docentes.Single(d => d.Nombre == "DOCENTE FICTICIO SIN ALTA").PersonaId);
        Assert.Equal(["08:00-12:00 Aula 101", "08:00-10:00 "],
            c9001.Horarios.Select(h => $"{h.Desde}-{h.Hasta} {h.Aula}"));
        Assert.Equal("Presencial", c9001.Modalidad);

        var c9002 = comisiones.Single(c => c.Id == "9002");
        Assert.Null(c9002.Materia.Id);
        Assert.Empty(c9002.Docentes);
        Assert.Equal("501", Assert.Single(c9002.Horarios).AsignacionId);

        var c9003 = comisiones.Single(c => c.Id == "9003");
        Assert.Empty(c9003.Horarios);
        Assert.Equal(GustavoRuiz, Assert.Single(c9003.Docentes).PersonaId);
    }

    [Fact]
    public async Task Documento_no_se_persiste_en_ninguna_tabla_del_snapshot()
    {
        await SembrarAsync();
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);

        await using var conexion = await AbrirConexionAsync();
        foreach (var tabla in new[] { "sga_comisiones", "sga_comision_carreras", "sga_comision_docentes", "sga_comision_horarios", "sga_importaciones" })
        {
            await using var comando = new NpgsqlCommand(
                $"SELECT count(*) FROM comisiones.{tabla} t WHERE t::text ~ '28\\.?341\\.?567|99000001|22156789'", conexion);
            Assert.Equal(0L, await comando.ExecuteScalarAsync(Ct));
        }
    }

    [Fact]
    public async Task Reimportar_reemplaza_solo_el_periodo_y_conserva_vinculos()
    {
        await SembrarAsync();
        await Servicio(Guarani151()).ImportarAsync(new("151"), Ct);
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);
        var antes = await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct);

        var reducido = GuaraniDePrueba.ConFixtures().Responder(GuaraniDePrueba.Comisiones("152"), SoloComision("9001"));
        var resultado = await Servicio(reducido).ImportarAsync(new("152"), Ct);

        var despues = await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct);
        Assert.Equal(1, resultado.Cantidad);
        Assert.Equal(["9001"], despues.Select(c => c.Id));
        Assert.Equal(
            antes.Single(c => c.Id == "9001").Docentes.Select(d => d.PersonaId),
            despues.Single().Docentes.Select(d => d.PersonaId));
        Assert.Equal(["8001", "8002", "8003"],
            (await Servicio(new GuaraniDePrueba()).ListarAsync("151", Ct)).Select(c => c.Id).Order());
    }

    [Fact]
    public async Task Persona_dada_de_alta_despues_queda_vinculada_al_reimportar()
    {
        await SembrarAsync();
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);
        await EjecutarAsync("""
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES ('e0000000-0000-4000-8000-000000000099', '99000001', 'Docente', 'Ficticio')
            """);

        var resultado = await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);

        Assert.Equal(0, resultado.DocentesSinVincular);
        var docente = (await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct))
            .Single(c => c.Id == "9001").Docentes.Single(d => d.Nombre == "DOCENTE FICTICIO SIN ALTA");
        Assert.Equal(Guid.Parse("e0000000-0000-4000-8000-000000000099"), docente.PersonaId);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "guarani-no-disponible", TipoErrorAplicacion.ServicioExternoFallido)]
    [InlineData(HttpStatusCode.Unauthorized, "guarani-autenticacion", TipoErrorAplicacion.ServicioExternoFallido)]
    public async Task Falla_de_guarani_conserva_las_comisiones_previas(
        HttpStatusCode status, string codigo, TipoErrorAplicacion tipo)
    {
        await SembrarAsync();
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);
        var fallido = GuaraniDePrueba.ConFixtures().Responder(GuaraniDePrueba.Comisiones("152"), "[]", status);

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => Servicio(fallido).ImportarAsync(new("152"), Ct));

        Assert.Equal(codigo, error.Codigo);
        Assert.Equal(tipo, error.Tipo);
        Assert.Equal(3, (await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct)).Count);
        var periodo = (await Servicio(new GuaraniDePrueba()).ListarPeriodosAsync(Ct)).Single(p => p.Id == "152");
        Assert.Equal("fallida", periodo.UltimoIntento!.Estado);
        Assert.Equal("exitosa", periodo.UltimaExitosa!.Estado);
        Assert.Equal("Lucía Fernández", periodo.UltimaExitosa.Usuario);
    }

    [Fact]
    public async Task Timeout_de_guarani_es_504_y_conserva_las_comisiones_previas()
    {
        await SembrarAsync();
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);
        var lento = GuaraniDePrueba.ConFixtures().Fallar(GuaraniDePrueba.Comisiones("152"),
            () => throw new TaskCanceledException("timeout", new TimeoutException()));

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => Servicio(lento).ImportarAsync(new("152"), Ct));

        Assert.Equal(TipoErrorAplicacion.ServicioExternoSinRespuesta, error.Tipo);
        Assert.Equal(3, (await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct)).Count);
        Assert.Equal("timeout", await EscalarTextoAsync(
            "SELECT causa FROM comisiones.sga_importaciones WHERE estado='fallida'"));
    }

    [Fact]
    public async Task Lista_vacia_no_reemplaza_y_queda_registrada_como_fallida()
    {
        await SembrarAsync();
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);
        var vacio = GuaraniDePrueba.ConFixtures().Responder(GuaraniDePrueba.Comisiones("152"), "[]");

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => Servicio(vacio).ImportarAsync(new("152"), Ct));

        Assert.Equal("guarani-sin-comisiones", error.Codigo);
        Assert.Equal(3, (await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct)).Count);
        Assert.Equal("sin_comisiones", await EscalarTextoAsync(
            "SELECT causa FROM comisiones.sga_importaciones WHERE estado='fallida'"));
    }

    [Fact]
    public async Task Comision_ya_importada_en_otro_periodo_rechaza_el_lote_sin_reemplazar()
    {
        await SembrarAsync();
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);
        var movida = Guarani151().Responder(GuaraniDePrueba.Comisiones("151"), SoloComision("9001"));

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => Servicio(movida).ImportarAsync(new("151"), Ct));

        Assert.Equal("guarani-respuesta-invalida", error.Codigo);
        Assert.Empty(await Servicio(new GuaraniDePrueba()).ListarAsync("151", Ct));
        Assert.Equal(3, (await Servicio(new GuaraniDePrueba()).ListarAsync("152", Ct)).Count);
    }

    [Fact]
    public async Task Importacion_en_curso_del_mismo_periodo_devuelve_conflicto()
    {
        await SembrarAsync();
        await EjecutarAsync("""
            INSERT INTO comisiones.sga_importaciones (anio_academico, periodo_lectivo_guarani_id, periodo_lectivo_nombre)
            VALUES (2026, '152', '2do Cuatrimestre')
            """);

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct));

        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        Assert.Equal("comisiones-importacion-en-curso", error.Codigo);
    }

    [Fact]
    public async Task Reserva_abandonada_caduca_y_no_bloquea_el_periodo()
    {
        await SembrarAsync();
        await EjecutarAsync("""
            INSERT INTO comisiones.sga_importaciones (anio_academico, periodo_lectivo_guarani_id, periodo_lectivo_nombre, iniciada_at)
            VALUES (2026, '152', '2do Cuatrimestre', now() - interval '1 hour')
            """);

        var resultado = await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);

        Assert.Equal(3, resultado.Cantidad);
        Assert.Equal("interrumpida", await EscalarTextoAsync(
            "SELECT causa FROM comisiones.sga_importaciones WHERE estado='fallida'"));
    }

    [Fact]
    public async Task Sin_configuracion_responde_no_configurado_sin_llamar_a_guarani()
    {
        var guarani = GuaraniDePrueba.ConFixtures();
        var opciones = GuaraniDePrueba.Opciones();
        opciones.Contrasena = string.Empty;
        var servicio = Servicio(guarani, opciones);

        var periodos = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.ListarPeriodosGuaraniAsync(Ct));
        var importar = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.ImportarAsync(new("152"), Ct));

        Assert.All([periodos, importar], e =>
        {
            Assert.Equal(TipoErrorAplicacion.ServicioExternoNoConfigurado, e.Tipo);
            Assert.Equal("guarani-no-configurado", e.Codigo);
        });
        Assert.Empty(guarani.Solicitudes);
    }

    [Fact]
    public async Task Periodo_que_guarani_no_informa_se_rechaza()
    {
        await SembrarAsync();

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("999"), Ct));

        Assert.Equal(TipoErrorAplicacion.Validacion, error.Tipo);
    }

    [Fact]
    public async Task Periodos_importados_se_ordenan_del_mas_reciente_al_mas_antiguo()
    {
        await SembrarAsync();
        await Servicio(Guarani151()).ImportarAsync(new("151"), Ct);
        await Servicio(GuaraniDePrueba.ConFixtures()).ImportarAsync(new("152"), Ct);

        var periodos = await Servicio(new GuaraniDePrueba()).ListarPeriodosAsync(Ct);

        Assert.Equal(["152", "151"], periodos.Select(p => p.Id).Where(id => id is "152" or "151"));
        Assert.True(periodos.ToList().FindIndex(p => p.Id == "152") < periodos.ToList().FindIndex(p => p.Id == "151"));
    }

    [Fact]
    public async Task Busqueda_por_documento_normaliza_y_omite_inexistentes()
    {
        await SembrarAsync();
        await using var identityDb = PostgresFixture.CrearIdentity(Cadena);
        var identity = new ConsultasIdentity(identityDb);

        var encontrados = await identity.BuscarPersonasPorDocumentosAsync(["28341567", "99000001"], Ct);

        Assert.Equal(CarlaLopez, Assert.Single(encontrados).Value);
        Assert.Empty(await identity.BuscarPersonasPorDocumentosAsync([], Ct));
    }

    [Fact]
    public async Task Permisos_de_comisiones_se_asignan_segun_el_design()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT p.code, string_agg(r.code, ',' ORDER BY r.code)
            FROM identity.rol_permisos rp
            JOIN identity.roles r ON r.id = rp.rol_id
            JOIN identity.permisos p ON p.id = rp.permiso_id
            WHERE p.code LIKE 'comisiones.%'
            GROUP BY p.code
            """, conexion);
        var asignados = new Dictionary<string, string>();
        await using (var lector = await comando.ExecuteReaderAsync(Ct))
            while (await lector.ReadAsync(Ct)) asignados[lector.GetString(0)] = lector.GetString(1);

        Assert.Equal("administrativo,coordinador_carrera,decanato,jefe_catedra,secretaria,sys_admin", asignados["comisiones.ver"]);
        Assert.Equal("secretaria,sys_admin", asignados["comisiones.importar"]);
    }

    private ServicioComisiones Servicio(GuaraniDePrueba guarani, OpcionesGuarani? opciones = null)
    {
        var valores = opciones ?? GuaraniDePrueba.Opciones();
        var db = PostgresFixture.CrearDesignaciones(Cadena);
        var identity = new ConsultasIdentity(PostgresFixture.CrearIdentity(Cadena));
        return new ServicioComisiones(
            new RepositorioComisiones(db),
            guarani.Cliente(valores),
            identity,
            new UsuarioActualFalso(Secretaria),
            Options.Create(valores),
            NullLogger<ServicioComisiones>.Instance);
    }

    private static GuaraniDePrueba Guarani151() => new GuaraniDePrueba()
        .Responder(GuaraniDePrueba.RutaPeriodos, GuaraniDePrueba.PeriodosJson)
        .Responder(GuaraniDePrueba.Comisiones("151"), GuaraniDePrueba.ComisionesJson
            .Replace("\"comision\": 9001", "\"comision\": 8001", StringComparison.Ordinal)
            .Replace("\"comision\": \"9002\"", "\"comision\": \"8002\"", StringComparison.Ordinal)
            .Replace("\"comision\": 9003", "\"comision\": 8003", StringComparison.Ordinal))
        .Responder(GuaraniDePrueba.Comisiones("152"), GuaraniDePrueba.ComisionesJson);

    private static string SoloComision(string id)
    {
        using var json = System.Text.Json.JsonDocument.Parse(GuaraniDePrueba.ComisionesJson);
        var filas = json.RootElement.EnumerateArray()
            .Where(f => f.GetProperty("comision").ToString() == id)
            .Select(f => f.GetRawText());
        return "[" + string.Join(",", filas) + "]";
    }

    private async Task SembrarAsync()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Join(raiz.FullName, "AGENTS.md"))) raiz = raiz.Parent;
        await EjecutarAsync(await File.ReadAllTextAsync(
            Path.Join(raiz!.FullName, "infra", "scripts", "seed-data", "sintetico.sql"), Ct));
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(Ct);
    }

    private async Task<string?> EscalarTextoAsync(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (string?)await comando.ExecuteScalarAsync(Ct);
    }

    private sealed class UsuarioActualFalso(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "test@unlam.edu.ar";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
