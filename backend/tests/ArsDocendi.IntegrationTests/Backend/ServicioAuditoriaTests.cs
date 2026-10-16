using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Contracts;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class ServicioAuditoriaTests
{
    // --------------------------------------------------- D6: actor y evidencia

    [Fact]
    public async Task Actor_que_resuelve_persona_se_muestra_en_orden_natural()
    {
        // sistema-seccion-unificada, design.md D6: «Nombre Apellido», no
        // «Apellido, Nombre» — actor y usuario afectado por igual.
        var registro = Registro(1, cambiadoPor: Guid.NewGuid(), requestId: "req-1");
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Ernesto Vidal", "Ernesto", "Vidal"));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Ernesto Vidal", evento.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorPersona, evento.TipoActor);
    }

    [Fact]
    public async Task Actor_que_resuelve_solo_por_cuenta_sigue_siendo_persona()
    {
        var registro = Registro(2, cambiadoPor: Guid.NewGuid(), requestId: "req-2");
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Cuenta visible", evento.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorPersona, evento.TipoActor);
    }

    [Fact]
    public async Task Actor_nulo_sin_contexto_de_solicitud_es_proceso_automatico()
    {
        // D6, fila 4: changed_by NULL Y request_id NULL — nadie hizo el pedido
        // por HTTP: migración, seed o proceso de fondo.
        var registro = Registro(3, cambiadoPor: null, requestId: null);
        var servicio = ServicioCon(new RegistroAuditoria(registro, null, null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Proceso automático", evento.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorProceso, evento.TipoActor);
    }

    [Fact]
    public async Task Actor_nulo_con_contexto_de_solicitud_es_no_identificado()
    {
        // D6, fila 3: changed_by NULL pero request_id presente — hubo un
        // request HTTP con un usuario anónimo o un claim no-UUID.
        var registro = Registro(4, cambiadoPor: null, requestId: "req-anonimo");
        var servicio = ServicioCon(new RegistroAuditoria(registro, null, null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Actor no identificado", evento.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorNoIdentificado, evento.TipoActor);
    }

    [Fact]
    public async Task Actor_con_id_estampado_que_no_resuelve_es_no_identificado()
    {
        // D6, fila 2: un changed_by estampado sin cuenta que lo resuelva.
        var registro = Registro(5, cambiadoPor: Guid.NewGuid(), requestId: "req-5");
        var servicio = ServicioCon(new RegistroAuditoria(registro, null, null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Actor no identificado", evento.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorNoIdentificado, evento.TipoActor);
    }

    // ------------------------------------------------------------ D6: acciones

    [Theory]
    [InlineData("INSERT", "Alta")]
    [InlineData("UPDATE", "Cambio")]
    [InlineData("DELETE", "Eliminación")]
    public async Task Las_tres_acciones_mapean_a_Alta_Cambio_o_Eliminacion(string accion, string etiqueta)
    {
        var registro = Registro(6, cambiadoPor: Guid.NewGuid(), requestId: "req-6", accion: accion);
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal(etiqueta, evento.AccionEtiqueta);
        // Una UPDATE nunca puede leer «Eliminación»: sólo un DELETE físico la produce.
        if (accion == "UPDATE")
        {
            Assert.NotEqual("Eliminación", evento.AccionEtiqueta);
        }
    }

    // ------------------------------------------------------------- D5: resumen

    [Fact]
    public async Task Un_solo_campo_seguro_muestra_los_valores_con_la_clave_de_fila()
    {
        var registro = Registro(
            7, cambiadoPor: Guid.NewGuid(), requestId: "req-7",
            schema: "designaciones", tabla: "pedidos", claveFila: "1042", accion: "UPDATE",
            filaAnterior: """{"estado":"pendiente"}""",
            filaNueva: """{"estado":"aprobado"}""",
            columnas: ["estado"]);
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Solicitud #1042: Estado pendiente → aprobado", evento.Resumen);
    }

    [Fact]
    public async Task Una_fila_no_numerica_no_agrega_numeral_al_resumen()
    {
        var registro = Registro(
            8, cambiadoPor: Guid.NewGuid(), requestId: "req-8",
            schema: "identity", tabla: "roles", claveFila: "un-uuid-cualquiera", accion: "UPDATE",
            filaAnterior: """{"scope":"local"}""",
            filaNueva: """{"scope":"global"}""",
            columnas: ["scope"]);
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Rol: Ámbito local → global", evento.Resumen);
    }

    [Fact]
    public async Task Mas_de_un_campo_seguro_se_queda_generico_sin_valores()
    {
        var registro = Registro(
            9, cambiadoPor: Guid.NewGuid(), requestId: "req-9",
            schema: "identity", tabla: "personas", claveFila: "persona-1", accion: "UPDATE",
            filaAnterior: """{"documento":"30111222","estado":"activo"}""",
            filaNueva: """{"documento":"30111222","estado":"inactivo"}""",
            columnas: ["documento", "estado"]);
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Cambio de persona · Documento, Estado", evento.Resumen);
        Assert.DoesNotContain("30111222", evento.Resumen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_campo_enmascarado_unico_se_queda_generico_sin_valor()
    {
        var registro = Registro(
            10, cambiadoPor: Guid.NewGuid(), requestId: "req-10",
            schema: "identity", tabla: "personas", claveFila: "persona-2", accion: "UPDATE",
            filaAnterior: """{"documento":"30111222"}""",
            filaNueva: """{"documento":"30111333"}""",
            columnas: ["documento"]);
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.Equal("Cambio de persona · Documento", evento.Resumen);
        Assert.DoesNotContain("30111", evento.Resumen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_columnas_cambiadas_el_resumen_es_generico_y_sin_cambios()
    {
        var registro = Registro(
            11, cambiadoPor: Guid.NewGuid(), requestId: "req-11",
            schema: "identity", tabla: "roles", claveFila: "1", accion: "UPDATE",
            filaAnterior: null, filaNueva: null, columnas: null);
        var servicio = ServicioCon(new RegistroAuditoria(registro, null, null, null));

        var pagina = await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None);
        var evento = Assert.Single(pagina.Elementos);

        Assert.Empty(evento.ColumnasCambiadas);
        Assert.Empty(evento.Cambios);
        Assert.Equal("Cambio de rol", evento.Resumen);
    }

    // ---------------------------------------- panel de detalle: fechas y nulos

    [Fact]
    public async Task Alta_de_asignacion_de_rol_omite_ausentes_formatea_fecha_en_hora_ar_y_nunca_el_texto_null()
    {
        // Reproduce el bug reportado sobre el panel de detalle: un INSERT en
        // identity.user_roles con deleted_at/granted_by/materia_id NULL en el
        // snapshot no debe mostrar ese ruido, y created_at (UTC en el snapshot)
        // se muestra en hora de Buenos Aires, no en ISO crudo.
        var filaNueva = $$"""
            {"id":"{{Guid.NewGuid()}}","user_id":"{{Guid.NewGuid()}}","role_id":"{{Guid.NewGuid()}}",
             "materia_id":null,"carrera_id":"{{Guid.NewGuid()}}",
             "granted_at":"2026-09-26T22:52:30.81236-03:00","granted_by":null,
             "created_at":"2026-09-27T01:52:30.81236+00:00","deleted_at":null}
            """;
        // Un INSERT real nunca llega con ColumnasCambiadas: el trigger de audit
        // sólo la completa en UPDATE (Registro() por defecto simula esto con
        // ["estado"], así que acá se arma el registro a mano con null real).
        var registro = new RegistroCambio
        {
            Id = 12,
            NombreSchema = "identity",
            NombreTabla = "user_roles",
            ClaveFila = "asignacion-1",
            Accion = "INSERT",
            FilaAnterior = null,
            FilaNueva = filaNueva,
            ColumnasCambiadas = null,
            CambiadoEn = DateTimeOffset.UnixEpoch,
            CambiadoPor = Guid.NewGuid(),
            RequestId = "req-12",
        };
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        Assert.DoesNotContain(evento.Cambios, c => c.Campo == "deleted_at");
        Assert.DoesNotContain(evento.Cambios, c => c.Campo == "materia_id");
        Assert.DoesNotContain(evento.Cambios, c => c.Campo == "granted_by");

        var creadoEn = Assert.Single(evento.Cambios, c => c.Campo == "created_at");
        Assert.False(creadoEn.Oculto);
        Assert.Equal("Fecha de creación", creadoEn.EtiquetaCampo);
        Assert.Equal("26/9/2026 22:52:30", creadoEn.ValorNuevo);

        var carrera = Assert.Single(evento.Cambios, c => c.Campo == "carrera_id");
        Assert.True(carrera.Oculto);
        Assert.Equal("Carrera", carrera.EtiquetaCampo);

        var otorgadoEl = Assert.Single(evento.Cambios, c => c.Campo == "granted_at");
        Assert.True(otorgadoEl.Oculto);
        Assert.Equal("Otorgado el", otorgadoEl.EtiquetaCampo);

        var id = Assert.Single(evento.Cambios, c => c.Campo == "id");
        Assert.True(id.Oculto);
        Assert.Equal("Identificador", id.EtiquetaCampo);
    }

    [Fact]
    public async Task Un_valor_que_pasa_a_null_no_muestra_el_texto_null_y_la_fecha_formatea_sin_hora()
    {
        var registro = Registro(
            13, cambiadoPor: Guid.NewGuid(), requestId: "req-13",
            schema: "designaciones", tabla: "designaciones", claveFila: "desig-1", accion: "UPDATE",
            filaAnterior: """{"vigente_hasta":"2026-01-15"}""",
            filaNueva: """{"vigente_hasta":null}""",
            columnas: ["vigente_hasta"]);
        var servicio = ServicioCon(new RegistroAuditoria(registro, "Cuenta visible", null, null));

        var evento = await UnicoEvento(servicio);

        var cambio = Assert.Single(evento.Cambios);
        Assert.Equal("15/1/2026", cambio.ValorAnterior);
        Assert.Null(cambio.ValorNuevo);
    }

    // ------------------------------------------------------------------ apoyo

    private static ServicioAuditoria ServicioCon(params RegistroAuditoria[] registros)
    {
        var repositorio = new RepositorioAuditoriaEnMemoria(new ResultadoPaginaAuditoria(registros, registros.Length));
        var fuenteAsistente = new FuenteAuditoriaAsistente(new ContratoDeAdministracionVacio(), repositorio);
        return new ServicioAuditoria(repositorio, fuenteAsistente, NullLogger<ServicioAuditoria>.Instance);
    }

    /// <summary>Sin eventos: estos tests prueban sólo el lado de <c>change_log</c>.</summary>
    private sealed class ContratoDeAdministracionVacio : IConsultasDeAuditoriaDeAdministracion
    {
        public Task<LoteDeAuditoriaDeAdministracion> ListarAsync(
            DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct) =>
            Task.FromResult(new LoteDeAuditoriaDeAdministracion([], false));
    }

    private static async Task<EventoAuditoriaDto> UnicoEvento(ServicioAuditoria servicio) =>
        Assert.Single((await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None)).Elementos);

    private static RegistroCambio Registro(
        long id,
        Guid? cambiadoPor,
        string? requestId,
        string schema = "identity",
        string tabla = "personas",
        string claveFila = "fila-1",
        string accion = "INSERT",
        string? filaAnterior = null,
        string? filaNueva = "{\"estado\":\"activo\"}",
        string[]? columnas = null) => new()
        {
            Id = id,
            NombreSchema = schema,
            NombreTabla = tabla,
            ClaveFila = claveFila,
            Accion = accion,
            FilaAnterior = filaAnterior,
            FilaNueva = filaNueva,
            ColumnasCambiadas = columnas ?? (accion == "UPDATE" ? null : ["estado"]),
            CambiadoEn = DateTimeOffset.UnixEpoch,
            CambiadoPor = cambiadoPor,
            RequestId = requestId,
        };

    private sealed class RepositorioAuditoriaEnMemoria(ResultadoPaginaAuditoria pagina) : IRepositorioAuditoria
    {
        public Task<ResultadoPaginaAuditoria> ListarAsync(
            ConsultaAuditoriaDto filtros, int salto, int cantidad, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginaAuditoria(
                pagina.Registros.Skip(salto).Take(cantidad).ToArray(), pagina.Total));

        public Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, NombreResuelto>>(new Dictionary<Guid, NombreResuelto>());

        public Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresDeSujetosAsync(
            IReadOnlyCollection<Guid> idsDeUsuarios, IReadOnlyCollection<Guid> idsDePersonas, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, NombreResuelto>>(new Dictionary<Guid, NombreResuelto>());

        public Task<IReadOnlyDictionary<Guid, string>> ResolverNombresDeRolesAsync(
            IReadOnlyCollection<Guid> idsDeRoles, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
