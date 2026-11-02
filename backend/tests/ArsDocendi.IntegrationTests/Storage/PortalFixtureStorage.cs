using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Storage.Infrastructure;
using Modules.Designaciones.Infrastructure;
using Modules.Portal.Application;
using Modules.Portal.Infrastructure;
using Modules.Portal.Repositories;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    private async Task SembrarPersonaPortalAsync(CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.personas(id,documento,nombre,apellido)
            VALUES (@id,'50000001','Docente','Prueba') ON CONFLICT DO NOTHING
            """, conexion);
        comando.Parameters.AddWithValue("id", Propietario);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private (ServicioAlmacenamientoArchivos Almacenamiento, ServicioPortal Portal) CrearPortalProtegido(
        AlmacenamientoDbContext db, PortalDbContext portalDb, DesignacionesDbContext designacionesDb)
    {
        var portalRefs = new ReferenciasArchivosPortal(portalDb);
        var designacionesRefs = new ReferenciasArchivosDesignaciones(designacionesDb);
        var proteccion = new ProteccionArchivos(db, [portalRefs, designacionesRefs]);
        var almacenamiento = CrearServicio(db, proteccion: proteccion, referencias: [portalRefs, designacionesRefs]);
        var portal = new ServicioPortal(new RepositorioPortal(portalDb), new IdentityDePrueba(),
            new UsuarioActual(Propietario), almacenamiento, proteccionArchivos: proteccion);
        return (almacenamiento, portal);
    }
}
