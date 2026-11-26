using ArsDocendi.Shared.Auth;
using Modules.Designaciones.Domain;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class MaquinaEstadosPedidoTests
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid Carrera = Guid.Parse("c0000000-0000-4000-8000-000000000201");

    [Theory]
    [InlineData("rechazar")]
    [InlineData("devolver")]
    [InlineData("priorizar")]
    public void Comentarios_obligatorios_se_validan_sin_mutar_el_pedido(string accion)
    {
        var pedido = new Pedido
        {
            Id = Guid.NewGuid(),
            Numero = "TEST",
            PeriodoId = Periodo,
            PersonaId = Guid.NewGuid(),
            MateriaId = Materia,
            CarreraId = Carrera,
            Novedad = Novedades.Alta,
            Estado = accion == "priorizar" ? EstadosPedido.Borrador : EstadosPedido.EnRevisionCoordinador,
        };
        var actor = accion == "priorizar"
            ? Actor(RolesCircuito.JefeCatedra)
            : Actor(RolesCircuito.CoordinadorCarrera);
        AccionPedido comando = accion switch
        {
            "rechazar" => new AccionPedido.Rechazar(""),
            "devolver" => new AccionPedido.Devolver(""),
            _ => new AccionPedido.Priorizar(""),
        };

        Assert.Throws<ErrorDominioPedido>(() =>
            MaquinaEstadosPedido.AplicarAccion(pedido, new AlcancePedido(Materia, Guid.Empty), comando, actor));
        Assert.False(pedido.Prioritario);
        Assert.Equal(accion == "priorizar" ? EstadosPedido.Borrador : EstadosPedido.EnRevisionCoordinador,
            pedido.Estado);
    }

    [Theory]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.Secretaria)]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Decanato)]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.CoordinadorCarrera)]
    public void Rol_incorrecto_no_puede_aceptar_la_etapa(string estado, string rol)
    {
        var pedido = new Pedido
        {
            Numero = "TEST",
            PeriodoId = Periodo,
            PersonaId = Guid.NewGuid(),
            MateriaId = Materia,
            CarreraId = Carrera,
            Novedad = Novedades.Alta,
            Estado = estado,
        };
        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(Materia, Guid.NewGuid()), new AccionPedido.Aceptar(), Actor(rol)));
    }

    [Theory]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.CoordinadorCarrera, "aceptar")]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.CoordinadorCarrera, "rechazar")]
    [InlineData(EstadosPedido.EnRevisionCoordinador, RolesCircuito.CoordinadorCarrera, "devolver")]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Secretaria, "aceptar")]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Secretaria, "rechazar")]
    [InlineData(EstadosPedido.EnRevisionSecretaria, RolesCircuito.Secretaria, "devolver")]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.Decanato, "aceptar")]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.Decanato, "rechazar")]
    [InlineData(EstadosPedido.EnRevisionDecanato, RolesCircuito.Decanato, "devolver")]
    public void Cada_revisor_puede_actuar_en_su_etapa(string estado, string rol, string accion)
    {
        var pedido = PedidoEn(estado);
        AccionPedido comando = accion switch
        {
            "aceptar" => new AccionPedido.Aceptar(),
            "rechazar" => new AccionPedido.Rechazar("Justificación"),
            _ => new AccionPedido.Devolver("Corrección requerida"),
        };

        var transicion = MaquinaEstadosPedido.AplicarAccion(pedido, new AlcancePedido(Materia, Guid.Empty), comando, Actor(rol));

        Assert.Equal(rol, transicion.CodigoRolActuante);
        // Aceptar avanza una etapa; devolver retrocede exactamente un nivel y recuerda
        // la etapa que devolvió para retomarla al reenviar [BR-designaciones-014].
        var (estadoEsperado, propietarioEsperado) = (accion, estado) switch
        {
            ("aceptar", EstadosPedido.EnRevisionCoordinador) => (EstadosPedido.EnRevisionSecretaria, (string?)null),
            ("aceptar", EstadosPedido.EnRevisionSecretaria) => (EstadosPedido.EnRevisionDecanato, null),
            ("aceptar", _) => (EstadosPedido.EnLote, null),
            ("rechazar", _) => (EstadosPedido.Rechazado, null),
            (_, EstadosPedido.EnRevisionCoordinador) => (EstadosPedido.Devuelto, RolesCircuito.JefeCatedra),
            (_, EstadosPedido.EnRevisionSecretaria) => (EstadosPedido.Devuelto, RolesCircuito.CoordinadorCarrera),
            _ => (EstadosPedido.Devuelto, RolesCircuito.Secretaria),
        };
        Assert.Equal(estadoEsperado, transicion.EstadoResultante);
        Assert.Equal(propietarioEsperado, transicion.PropietarioActual);
        Assert.Equal(accion == "devolver" ? estado : null, transicion.EtapaRetorno);
    }

    [Theory]
    [InlineData(EstadosPedido.Rechazado)]
    [InlineData(EstadosPedido.Cancelado)]
    [InlineData(EstadosPedido.Aprobado)]
    [InlineData(EstadosPedido.EnLote)]
    [InlineData(EstadosPedido.NoAprobado)]
    public void Fuera_del_circuito_no_se_admite_ninguna_accion_de_revision(string estado)
    {
        // [BR-designaciones-011] El rechazo (y el resto de los terminales) no se reabre.
        // En lote y rechazado en la conciliación ya salieron del circuito de revisión.
        AccionPedido[] acciones =
        [
            new AccionPedido.Enviar(), new AccionPedido.Cancelar(), new AccionPedido.Aceptar(),
            new AccionPedido.Rechazar("Otra vez"), new AccionPedido.Devolver("Corregir"),
            new AccionPedido.Reenviar(), new AccionPedido.Priorizar("Urgente"), new AccionPedido.Despriorizar(),
        ];
        string[] roles =
        [
            RolesCircuito.JefeCatedra, RolesCircuito.CoordinadorCarrera, RolesCircuito.Secretaria,
            RolesCircuito.Decanato, RolesCircuito.Administrativo,
        ];

        foreach (var rol in roles)
        foreach (var accion in acciones)
        {
            var pedido = PedidoEn(estado);
            Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
                pedido, new AlcancePedido(Materia, Guid.Empty), accion, Actor(rol)));
            Assert.False(MaquinaEstadosPedido.PuedeRevisar(pedido, new AlcancePedido(Materia, Guid.Empty), Actor(rol)));
        }
    }

    [Theory]
    [InlineData(RolesCircuito.JefeCatedra)]
    [InlineData(RolesCircuito.CoordinadorCarrera)]
    [InlineData(RolesCircuito.Secretaria)]
    [InlineData(RolesCircuito.Decanato)]
    [InlineData(RolesCircuito.Administrativo)]
    public void Cada_rol_del_circuito_puede_priorizar_y_despriorizar_en_su_ambito(string rol)
    {
        var pedido = PedidoEn(EstadosPedido.Borrador);
        var actor = Actor(rol);

        var prioridad = MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(Materia, Guid.Empty), new AccionPedido.Priorizar("Urgente"), actor);
        var sinPrioridad = MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(Materia, Guid.Empty), new AccionPedido.Despriorizar(), actor);

        Assert.True(prioridad.Prioritario);
        Assert.False(sinPrioridad.Prioritario);
        Assert.Equal(rol, prioridad.CodigoRolActuante);
    }

    [Theory]
    [InlineData(EstadosPedido.EnRevisionCoordinador)]
    [InlineData(EstadosPedido.EnRevisionSecretaria)]
    [InlineData(EstadosPedido.EnRevisionDecanato)]
    public void Administrativo_revisa_pero_no_acepta_en_cada_etapa(string estado)
    {
        var pedido = PedidoEn(estado);
        var actor = Actor(RolesCircuito.Administrativo);

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(Materia, Guid.Empty), new AccionPedido.Aceptar(), actor));
        Assert.Equal(EstadosPedido.Rechazado, MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(Materia, Guid.Empty), new AccionPedido.Rechazar("No corresponde"), actor).EstadoResultante);
        Assert.Equal(EstadosPedido.Devuelto, MaquinaEstadosPedido.AplicarAccion(
            pedido, new AlcancePedido(Materia, Guid.Empty), new AccionPedido.Devolver("Corregir"), actor).EstadoResultante);
    }

    [Fact]
    public void Cancelar_y_reenviar_exigen_ambito()
    {
        var fueraDeAmbito = new ActorContexto(
            Guid.NewGuid(),
            new HashSet<string> { RolesCircuito.JefeCatedra },
            new HashSet<Guid>(),
            new HashSet<Guid>());
        var borrador = PedidoEn(EstadosPedido.Borrador);
        var devuelto = PedidoEn(EstadosPedido.Devuelto);
        devuelto.PropietarioActual = RolesCircuito.JefeCatedra;
        devuelto.EtapaRetorno = EstadosPedido.EnRevisionCoordinador;

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            borrador, new AlcancePedido(Materia, Guid.NewGuid()), new AccionPedido.Cancelar(), fueraDeAmbito));
        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            devuelto, new AlcancePedido(Materia, Guid.NewGuid()), new AccionPedido.Reenviar(), fueraDeAmbito));
    }

    [Fact]
    public void Sincronizar_un_pedido_en_lote_lo_aprueba_con_el_rol_de_mayor_alcance()
    {
        var actor = ConPermisos(RolesCircuito.Secretaria, Permisos.DesignacionesConciliar);

        var transicion = MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(EstadosPedido.EnLote), new AlcancePedido(Materia, Carrera), new AccionPedido.Sincronizar(), actor);

        Assert.Equal(EstadosPedido.Aprobado, transicion.EstadoResultante);
        Assert.Equal(AccionesHistorial.Sincronizar, transicion.AccionHistorial);
        Assert.Equal(RolesCircuito.Secretaria, transicion.CodigoRolActuante);
    }

    [Fact]
    public void Conciliar_exige_el_permiso_aunque_el_rol_sea_departamental()
    {
        // La conciliación se decide por permiso, nunca por código de rol.
        var sinPermiso = ConPermisos(RolesCircuito.Secretaria);

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(EstadosPedido.EnLote), new AlcancePedido(Materia, Carrera), new AccionPedido.Sincronizar(), sinPermiso));
        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(EstadosPedido.EnLote), new AlcancePedido(Materia, Carrera), new AccionPedido.NoAprobar("No salió"), sinPermiso));
    }

    [Fact]
    public void Conciliar_exige_el_ambito_del_pedido()
    {
        // Un Coordinador con el permiso sólo concilia su carrera [BR-designaciones-009].
        var coordinador = new ActorContexto(
            Guid.NewGuid(),
            new HashSet<string> { RolesCircuito.CoordinadorCarrera },
            new HashSet<Guid>(),
            new HashSet<Guid> { Guid.NewGuid() })
        {
            Permisos = new HashSet<string> { Permisos.DesignacionesConciliar },
        };

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(EstadosPedido.EnLote), new AlcancePedido(Materia, Carrera), new AccionPedido.Sincronizar(), coordinador));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rechazar_en_la_conciliacion_exige_motivo(string motivo)
    {
        var actor = ConPermisos(RolesCircuito.Administrativo, Permisos.DesignacionesConciliar);

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(EstadosPedido.EnLote), new AlcancePedido(Materia, Carrera), new AccionPedido.NoAprobar(motivo), actor));
    }

    [Fact]
    public void Rechazar_en_la_conciliacion_con_motivo_pasa_a_no_aprobado()
    {
        var actor = ConPermisos(RolesCircuito.Administrativo, Permisos.DesignacionesConciliar);

        var transicion = MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(EstadosPedido.EnLote), new AlcancePedido(Materia, Carrera),
            new AccionPedido.NoAprobar("La rectificación no lo aprobó"), actor);

        Assert.Equal(EstadosPedido.NoAprobado, transicion.EstadoResultante);
        Assert.Equal(AccionesHistorial.NoAprobar, transicion.AccionHistorial);
        Assert.Equal("La rectificación no lo aprobó", transicion.Comentario);
    }

    [Fact]
    public void Un_pedido_heredado_ya_materializado_no_se_rechaza_ni_se_edita_en_la_conciliacion()
    {
        // Se escribió sobre las designaciones al aceptar Decanato, antes de la conciliación.
        var heredado = PedidoEn(EstadosPedido.EnLote);
        heredado.MaterializadoEn = DateTimeOffset.UtcNow;
        var actor = ConPermisos(
            RolesCircuito.Secretaria, Permisos.DesignacionesConciliar, Permisos.DesignacionesEditar);

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            heredado, new AlcancePedido(Materia, Carrera), new AccionPedido.NoAprobar("No salió"), actor));
        Assert.False(MaquinaEstadosPedido.PuedeEditar(heredado, actor));
    }

    [Theory]
    [InlineData(EstadosPedido.NoAprobado)]
    [InlineData(EstadosPedido.Aprobado)]
    public void Solo_se_concilia_un_pedido_en_lote(string estado)
    {
        var actor = ConPermisos(RolesCircuito.Secretaria, Permisos.DesignacionesConciliar);

        Assert.Throws<ErrorDominioPedido>(() => MaquinaEstadosPedido.AplicarAccion(
            PedidoEn(estado), new AlcancePedido(Materia, Carrera), new AccionPedido.Sincronizar(), actor));
    }

    [Theory]
    [InlineData(EstadosPedido.EnLote, true)]
    [InlineData(EstadosPedido.NoAprobado, true)]
    [InlineData(EstadosPedido.Aprobado, false)]
    [InlineData(EstadosPedido.Rechazado, false)]
    public void Con_designaciones_editar_se_edita_en_lote_o_rechazado_en_la_conciliacion(string estado, bool editable)
    {
        var conPermiso = ConPermisos(RolesCircuito.Administrativo, Permisos.DesignacionesEditar);
        var sinPermiso = ConPermisos(RolesCircuito.Administrativo);

        Assert.Equal(editable, MaquinaEstadosPedido.PuedeEditar(PedidoEn(estado), conPermiso));
        Assert.False(MaquinaEstadosPedido.PuedeEditar(PedidoEn(estado), sinPermiso));
    }

    private static ActorContexto ConPermisos(string rol, params string[] permisos) =>
        Actor(rol) with { Permisos = permisos.ToHashSet() };

    private static Pedido PedidoEn(string estado) => new()
    {
        Id = Guid.NewGuid(),
        Numero = "TEST",
        PeriodoId = Periodo,
        PersonaId = Guid.NewGuid(),
        MateriaId = Materia,
        CarreraId = Carrera,
        Novedad = Novedades.Alta,
        Estado = estado,
    };

    private static ActorContexto Actor(string rol) => new(
        Guid.NewGuid(),
        new HashSet<string> { rol },
        new HashSet<Guid> { Materia },
        new HashSet<Guid> { Guid.Empty });
}
