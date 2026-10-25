using ArsDocendi.Host.Administracion;
using Modules.Asistente.Contracts;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// <see cref="FuenteAuditoriaAsistente.Mapear"/> — mapeo puro, sin base
/// (sistema-seccion-unificada, design.md D5, tarea 2.4).
/// </summary>
public sealed class FuenteAuditoriaAsistenteTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid UsuarioAfectado = Guid.NewGuid();

    [Fact]
    public void Presupuesto_de_usuario_muestra_el_nombre_y_los_valores()
    {
        var evento = new EventoDeAdministracion(
            1, Actor, DateTimeOffset.UnixEpoch, "presupuesto.usuario", null, UsuarioAfectado,
            [new CampoDeAdministracion("cupo", "30", "60")]);
        var nombres = new Dictionary<Guid, NombreResuelto>
        {
            [Actor] = new(null, "Ernesto", "Vidal"),
            [UsuarioAfectado] = new(null, "Lucía", "Fernández"),
        };

        var dto = FuenteAuditoriaAsistente.Mapear(evento, nombres);

        Assert.Equal("Cupo diario de Lucía Fernández: 30 → 60", dto.Resumen);
        Assert.Equal("Cambio", dto.AccionEtiqueta);
        Assert.Equal("Ernesto Vidal", dto.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorPersona, dto.TipoActor);
        Assert.Equal("Asistente", dto.Modulo);
        Assert.Equal(UsuarioAfectado.ToString(), dto.RowPk);
    }

    [Fact]
    public void Presupuesto_de_usuario_con_cupo_previo_nulo_es_alta_sin_flecha()
    {
        var evento = new EventoDeAdministracion(
            2, Actor, DateTimeOffset.UnixEpoch, "presupuesto.usuario", null, UsuarioAfectado,
            [new CampoDeAdministracion("cupo", null, "7")]);
        var nombres = new Dictionary<Guid, NombreResuelto> { [UsuarioAfectado] = new(null, "Lucía", "Fernández") };

        var dto = FuenteAuditoriaAsistente.Mapear(evento, nombres);

        Assert.Equal("Alta", dto.AccionEtiqueta);
        Assert.Equal("INSERT", dto.Accion);
        Assert.Equal("Cupo diario de Lucía Fernández: 7", dto.Resumen);
    }

    [Fact]
    public void Presupuesto_de_rol_usa_el_codigo_de_rol_como_clave()
    {
        var evento = new EventoDeAdministracion(
            3, Actor, DateTimeOffset.UnixEpoch, "presupuesto.rol", "secretaria", null,
            [new CampoDeAdministracion("cupo", "10", "15")]);

        var dto = FuenteAuditoriaAsistente.Mapear(evento, new Dictionary<Guid, NombreResuelto>());

        Assert.Equal("secretaria", dto.RowPk);
        Assert.Equal("Cupo diario del rol secretaria: 10 → 15", dto.Resumen);
        Assert.Equal("Actor no identificado", dto.Actor);
        Assert.Equal(MapeadorEventoAuditoria.TipoActorNoIdentificado, dto.TipoActor);
    }

    [Fact]
    public void Activacion_de_mantenimiento_muestra_activo_y_enmascara_razon()
    {
        var evento = new EventoDeAdministracion(
            4, Actor, DateTimeOffset.UnixEpoch, "mantenimiento.activar", null, null,
            [
                new CampoDeAdministracion("activo", "false", "true"),
                new CampoDeAdministracion("razon", null, "mantenimiento programado"),
            ]);

        var dto = FuenteAuditoriaAsistente.Mapear(evento, new Dictionary<Guid, NombreResuelto>());

        Assert.Equal("Mantenimiento del asistente activado", dto.Resumen);
        var activo = Assert.Single(dto.Cambios, c => c.Campo == "activo");
        Assert.False(activo.Oculto);
        // «Sí»/«No», no «true»/«false» (sistema-seccion-unificada, design.md
        // D5, «Humanized identity events», tarea 2.9).
        Assert.Equal("No", activo.ValorAnterior);
        Assert.Equal("Sí", activo.ValorNuevo);
        var razon = Assert.Single(dto.Cambios, c => c.Campo == "razon");
        Assert.True(razon.Oculto);
        Assert.Null(razon.ValorNuevo);
        Assert.DoesNotContain("mantenimiento programado", dto.Resumen, StringComparison.Ordinal);
    }

    [Fact]
    public void Tipo_desconocido_mapea_a_cambio_generico_sin_campos()
    {
        var evento = new EventoDeAdministracion(
            5, Actor, DateTimeOffset.UnixEpoch, "algo.nuevo", null, null, []);

        var dto = FuenteAuditoriaAsistente.Mapear(evento, new Dictionary<Guid, NombreResuelto>());

        Assert.Equal("Cambio", dto.AccionEtiqueta);
        Assert.Empty(dto.Cambios);
        Assert.Equal("Cambio de algo.nuevo", dto.Resumen);
    }

    [Fact]
    public void Tope_organizacional_muestra_valores_en_usd()
    {
        var evento = new EventoDeAdministracion(
            6, Actor, DateTimeOffset.UnixEpoch, "tope_organizacional", null, null,
            [new CampoDeAdministracion("tope_mensual_usd", "0", "500")]);

        var dto = FuenteAuditoriaAsistente.Mapear(evento, new Dictionary<Guid, NombreResuelto>());

        Assert.Equal("Tope mensual organizacional: 0 → 500 USD", dto.Resumen);
        Assert.Equal("—", dto.RowPk);
    }

    private static EventoAuditoriaDto MapearSolo(string tipo, string? clave, params CampoDeAdministracion[] campos) =>
        FuenteAuditoriaAsistente.Mapear(
            new EventoDeAdministracion(7, Actor, DateTimeOffset.UnixEpoch, tipo, clave, null, campos),
            new Dictionary<Guid, NombreResuelto>());

    [Fact]
    public void Seleccion_de_modelo_desde_el_servidor_muestra_empresa_modelo_y_pista()
    {
        var dto = MapearSolo(
            "modelo.seleccionar", "openai",
            new CampoDeAdministracion("empresa", null, "openai"),
            new CampoDeAdministracion("modelo", null, "gpt-5"),
            new CampoDeAdministracion("origen", "servidor", null),
            new CampoDeAdministracion("pista", null, "…ab12"));

        Assert.Equal("Modelo del asistente: configuración del servidor → openai / gpt-5 (clave …ab12)", dto.Resumen);
        Assert.Equal("Modelo del asistente", dto.Objeto);
        Assert.Equal("modelo_del_asistente", dto.Tabla);
        Assert.Equal("openai", dto.RowPk);
        Assert.Equal("Cambio", dto.AccionEtiqueta);
        Assert.All(dto.Cambios, c => Assert.False(c.Oculto));
        Assert.Contains(dto.Cambios, c => c.Campo == "pista" && c.EtiquetaCampo == "Pista de la clave");
    }

    [Fact]
    public void Seleccion_de_modelo_local_entre_modelos_no_muestra_pista()
    {
        var dto = MapearSolo(
            "modelo.seleccionar", "ollama",
            new CampoDeAdministracion("empresa", "openai", "ollama"),
            new CampoDeAdministracion("modelo", "gpt-5", "qwen"),
            new CampoDeAdministracion("origen", null, null),
            new CampoDeAdministracion("pista", null, null));

        Assert.Equal("Modelo del asistente: openai / gpt-5 → ollama / qwen", dto.Resumen);
    }

    [Fact]
    public void Restablecer_el_modelo_vuelve_a_la_configuracion_del_servidor()
    {
        var dto = MapearSolo(
            "modelo.restablecer", "openai",
            new CampoDeAdministracion("empresa", "openai", null),
            new CampoDeAdministracion("modelo", "gpt-5", null),
            new CampoDeAdministracion("origen", null, "servidor"),
            new CampoDeAdministracion("pista", null, null));

        Assert.Equal("Modelo del asistente: openai / gpt-5 → configuración del servidor", dto.Resumen);
        Assert.Equal("modelo_del_asistente", dto.Tabla);
    }

    [Fact]
    public void Credencial_nueva_es_alta_y_muestra_solo_la_pista()
    {
        var dto = MapearSolo(
            "credencial.crear", "openai",
            new CampoDeAdministracion("empresa", null, "openai"),
            new CampoDeAdministracion("pista", null, "…ab12"));

        Assert.Equal("Clave de openai guardada (pista …ab12)", dto.Resumen);
        Assert.Equal("Alta", dto.AccionEtiqueta);
        Assert.Equal("INSERT", dto.Accion);
        Assert.Equal("modelo_del_asistente", dto.Tabla);
    }

    [Fact]
    public void Credencial_reemplazada_muestra_las_dos_pistas()
    {
        var dto = MapearSolo(
            "credencial.reemplazar", "openai",
            new CampoDeAdministracion("empresa", "openai", "openai"),
            new CampoDeAdministracion("pista", "…ab12", "…cd34"));

        Assert.Equal("Clave de openai reemplazada: pista …ab12 → …cd34", dto.Resumen);
        Assert.Equal("Cambio", dto.AccionEtiqueta);
    }
}
