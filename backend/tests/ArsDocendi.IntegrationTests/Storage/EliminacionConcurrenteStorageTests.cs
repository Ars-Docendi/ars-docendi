using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Modules.Portal.Domain;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    [Fact]
    public async Task Antivirus_tardio_no_resucita_archivo_eliminado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var antivirus = new AntivirusDetenido();
        var a = CrearServicio(dbA, antivirus);
        var b = CrearServicio(dbB);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nrevision\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytes.Length), Propietario, ct);
        await SubirContenidoAsync(a, sesion, bytes);
        var confirmar = a.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        await antivirus.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        try { await b.EliminarAsync(sesion.ArchivoId, Propietario, ct); }
        finally { antivirus.Continuar.TrySetResult(); }
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => confirmar);
        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        Assert.Equal(EstadosArchivo.Eliminado, (await b.ObtenerAsync(sesion.ArchivoId, ct))!.Estado);
        Assert.Null(await b.AbrirDescargaAsync(sesion.ArchivoId, ct));
    }

    [Fact]
    public async Task Doble_confirmacion_analiza_una_vez_y_reintento_es_idempotente()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var antivirus = new AntivirusDetenido();
        var a = CrearServicio(dbA, antivirus);
        var b = CrearServicio(dbB);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nrevision\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytes.Length), Propietario, ct);
        await SubirContenidoAsync(a, sesion, bytes);
        var confirmar = a.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        await antivirus.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        try
        {
            var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => b.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct));
            Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        }
        finally { antivirus.Continuar.TrySetResult(); }
        var publicado = await confirmar;
        var repetido = await b.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        Assert.Equal(publicado, repetido);
        Assert.Equal(1, antivirus.Invocaciones);
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => b.ConfirmarCargaAsync(new(sesion.ArchivoId, "00"), Propietario, ct));
    }

    [Fact]
    public async Task Borrado_remoto_fallido_reserva_indisponibilidad_y_reintenta_sin_afectar_ajenos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var proveedor = new ProveedorConFallo(new ProveedorSeaweedFs(cliente!));
        var servicio = CrearServicio(db, proveedorAlternativo: proveedor);
        var id = await SubirPdfAsync(servicio, "propio.pdf");
        var ajeno = await SubirPdfAsync(servicio, "ajeno.pdf");
        proveedor.FallarBorrado = true;
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EliminarAsync(id, Propietario, ct));
        Assert.Equal("archivo-delete-pending", error.Codigo);
        Assert.Null(await servicio.AbrirDescargaAsync(id, ct));
        Assert.Equal(EstadosArchivo.Cuarentena, (await servicio.ObtenerAsync(id, ct))!.Estado);
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.ConfirmarCargaAsync(new(id), Propietario, ct));
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.RequerirDisponibleDePropietarioAsync(id, PropositosArchivo.Cv, Propietario, ct));
        proveedor.FallarBorrado = false;
        Assert.Equal(1, await servicio.LimpiarAsync(DateTimeOffset.UtcNow, ct));
        await servicio.EliminarAsync(id, Propietario, ct);
        Assert.Equal(EstadosArchivo.Eliminado, (await servicio.ObtenerAsync(id, ct))!.Estado);
        var descarga = await servicio.AbrirDescargaAsync(ajeno, ct);
        Assert.NotNull(descarga);
        await descarga.Contenido.DisposeAsync();
    }

    [Fact]
    public async Task Consulta_de_consumidor_fallida_no_se_interpreta_como_cero_referencias()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db, referencias: [new ReferenciasSql(db, "Portal", true), new ReferenciasSql(db, "Designaciones")]);
        var id = await SubirPdfAsync(servicio, "conservado.pdf");
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EliminarAsync(id, Propietario, ct));
        Assert.Equal("archivo-references-unavailable", error.Codigo);
        Assert.Equal(EstadosArchivo.Disponible, (await servicio.ObtenerAsync(id, ct))!.Estado);
        var descarga = await servicio.AbrirDescargaAsync(id, ct);
        Assert.NotNull(descarga);
        await descarga.Contenido.DisposeAsync();
    }

    [Fact]
    public async Task Consumidor_ausente_no_se_interpreta_como_cero_referencias()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db, registrarConsumidores: false);
        var id = await SubirPdfAsync(servicio, "conservado-sin-consumidores.pdf");

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EliminarAsync(id, Propietario, ct));

        Assert.Equal("archivo-references-unavailable", error.Codigo);
        Assert.Equal(EstadosArchivo.Disponible, (await servicio.ObtenerAsync(id, ct))!.Estado);
        var descarga = await servicio.AbrirDescargaAsync(id, ct);
        Assert.NotNull(descarga);
        await descarga.Contenido.DisposeAsync();
    }

    [Fact]
    public async Task Archivo_referenciado_rechaza_delete_y_retencion_conserva_descarga()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var portal = PostgresFixture.CrearPortal(cadena);
        var proteccion = new ProteccionArchivos(db, [new ParticipantePortal(portal)]);
        var servicio = CrearServicio(db, proteccion: proteccion);
        var id = await SubirPdfAsync(servicio, "referenciado.pdf");
        await proteccion.EjecutarAsync([id], async token =>
        {
            await servicio.RequerirDisponibleDePropietarioAsync(id, PropositosArchivo.Cv, Propietario, token);
            portal.Perfiles.Add(PerfilConCv(id));
            await portal.SaveChangesAsync(token);
            return true;
        }, ct);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EliminarAsync(id, Propietario, ct));
        Assert.Equal("archivo-referenced", error.Codigo);
        Assert.False(await servicio.EliminarSiSinReferenciasAsync(id, Propietario, ct));
        var descarga = await servicio.AbrirDescargaAsync(id, ct);
        Assert.NotNull(descarga);
        await descarga.Contenido.DisposeAsync();
    }

    private static Perfil PerfilConCv(Guid archivoId)
    {
        var perfil = new Perfil { Id = Guid.NewGuid(), PersonaId = Guid.NewGuid(), CreadoEn = DateTimeOffset.UtcNow };
        perfil.Cv = new Cv { Id = Guid.NewGuid(), PerfilId = perfil.Id, ArchivoId = archivoId, Nombre = "sintetico.pdf", FechaCarga = DateTimeOffset.UtcNow, CreadoEn = DateTimeOffset.UtcNow };
        return perfil;
    }
}
