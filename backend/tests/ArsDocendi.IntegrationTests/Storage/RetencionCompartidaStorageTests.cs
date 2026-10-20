using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Modules.Portal.Application;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Repositories;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    [Fact]
    public async Task Cv_compartido_con_pedido_sobrevive_reemplazo_y_delete_directo_es_conflicto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var portalDb = PostgresFixture.CrearPortal(cadena);
        await using var designacionesDb = PostgresFixture.CrearDesignaciones(cadena);
        await SembrarPersonaPortalAsync(ct);
        var (almacenamiento, portal) = CrearPortalProtegido(db, portalDb, designacionesDb);
        var primero = await SubirPdfAsync(almacenamiento, "cv-compartido.pdf");
        var cv = await portal.GuardarCvAsync(new GuardarCvDto(primero), ct);
        var pedido = Guid.NewGuid();
        var carrera = Guid.NewGuid();
        var materia = Guid.NewGuid();
        var periodo = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES ({Propietario},'50000001','Sintetica','Prueba') ON CONFLICT DO NOTHING;
            INSERT INTO identity.carreras(id,code,name) VALUES ({carrera},'STORAGE-TEST','Sintetica');
            INSERT INTO identity.materias(id,code,name) VALUES ({materia},'98765','Sintetica');
            INSERT INTO designaciones.periodos(id,nombre,carga_desde,carga_hasta,impacto_desde,impacto_hasta)
                VALUES ({periodo},'Sintetico',DATE '2026-01-01',DATE '2026-12-31',DATE '2027-01-01',DATE '2027-12-31');
            INSERT INTO designaciones.pedidos(id,numero,periodo_id,persona_id,novedad,materia_id,carrera_id,cargo_solicitado_id,dedicacion_solicitada_id,horas,horas_investigacion,horas_externas)
                VALUES ({pedido},'STORAGE-TEST',{periodo},{Propietario},'Alta',{materia},{carrera},
                    (SELECT id FROM designaciones.cargos LIMIT 1),(SELECT id FROM designaciones.dedicaciones LIMIT 1),10,0,0);
            INSERT INTO designaciones.pedido_adjuntos(pedido_id,tipo,nombre,archivo_id) VALUES ({pedido},'cv','cv-compartido.pdf',{primero});
            """, ct);
        var segundo = await SubirPdfAsync(almacenamiento, "cv-nuevo.pdf");
        cv = await portal.GuardarCvAsync(new GuardarCvDto(segundo) { Version = cv.Version }, ct);
        Assert.Equal(segundo, cv.ArchivoId);
        Assert.Equal(primero, await db.Database.SqlQuery<Guid>($"SELECT archivo_id AS \"Value\" FROM designaciones.pedido_adjuntos WHERE pedido_id={pedido}").SingleAsync(ct));
        Assert.Equal(EstadosArchivo.Disponible, (await almacenamiento.ObtenerAsync(primero, ct))!.Estado);
        var descarga = await almacenamiento.AbrirDescargaAsync(primero, ct);
        Assert.NotNull(descarga);
        using var lector = new StreamReader(descarga.Contenido);
        Assert.Equal("%PDF-1.7\ncv-compartido.pdf\n", await lector.ReadToEndAsync(ct));
        var conflicto = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => almacenamiento.EliminarAsync(primero, Propietario, ct));
        Assert.Equal("archivo-referenced", conflicto.Codigo);
        await portal.EliminarCvAsync(ct, cv.Version);
        Assert.Equal(EstadosArchivo.Eliminado, (await almacenamiento.ObtenerAsync(segundo, ct))!.Estado);
        Assert.Equal(EstadosArchivo.Disponible, (await almacenamiento.ObtenerAsync(primero, ct))!.Estado);
    }
}
