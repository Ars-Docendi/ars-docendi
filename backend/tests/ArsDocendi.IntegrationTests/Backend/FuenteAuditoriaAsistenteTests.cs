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
}
