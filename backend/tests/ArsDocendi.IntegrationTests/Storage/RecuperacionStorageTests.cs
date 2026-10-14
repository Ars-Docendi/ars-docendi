using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    [Fact]
    public async Task Reserva_de_intento_vence_antes_de_sesion_para_permitir_recuperacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var lector = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var sesion = await servicio.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytes.Length), Propietario, ct);
        using var cuerpo = new CuerpoDetenido(bytes);
        var subir = servicio.SubirAsync(sesion.ArchivoId, Propietario, cuerpo, null, bytes.Length, ct);
        await cuerpo.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        try
        {
            var archivo = await lector.Archivos.AsNoTracking().SingleAsync(ct);
            Assert.True(archivo.ReservaHasta < archivo.ExpiraEn, "El lease debe expirar antes que la sesión para poder recuperar un proceso caído.");
        }
        finally { cuerpo.Continuar.TrySetResult(); await subir; }
    }

    [Fact]
    public async Task Upload_activo_no_se_limpia_y_upload_vencido_se_recupera_sin_sobrescritura()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var a = CrearServicio(dbA);
        var b = CrearServicio(dbB);
        var bytesA = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var bytesB = Encoding.UTF8.GetBytes("%PDF-1.7\nversion B\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytesA.Length), Propietario, ct);
        using var cuerpo = new CuerpoDetenido(bytesA);
        var subir = a.SubirAsync(sesion.ArchivoId, Propietario, cuerpo, null, bytesA.Length, ct);
        await cuerpo.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        try
        {
            // La fecha solicitada para limpieza no invalida un lease activo del reloj real.
            Assert.Equal(0, await b.LimpiarAsync(DateTimeOffset.UtcNow.AddHours(1), ct));
            await using (var transaccion = await dbB.Database.BeginTransactionAsync(ct))
            {
                await dbB.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='1s'", ct);
                await dbB.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({sesion.ArchivoId.ToString()}, 8361))", ct);
                await transaccion.RollbackAsync(ct);
            }
            await dbB.Archivos.Where(x => x.Id == sesion.ArchivoId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservaHasta, DateTimeOffset.UtcNow.AddMinutes(-1)), ct);
            await SubirContenidoAsync(b, sesion, bytesB);
            await b.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        }
        finally { cuerpo.Continuar.TrySetResult(); }
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => subir);
        var descarga = await b.AbrirDescargaAsync(sesion.ArchivoId, ct);
        Assert.NotNull(descarga);
        using var memoria = new MemoryStream();
        await descarga.Contenido.CopyToAsync(memoria, ct);
        await descarga.Contenido.DisposeAsync();
        Assert.Equal(bytesB, memoria.ToArray());
    }

    [Fact]
    public async Task Fallo_tras_put_real_reintenta_con_otra_clave_y_limpia_temporal_sin_borrar_ajenos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var real = new ProveedorSeaweedFs(cliente!);
        var proveedor = new ProveedorConFallo(real) { FallarTrasSubir = true, FallarBorrado = true };
        var servicio = CrearServicio(db, proveedorAlternativo: proveedor);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var sesion = await servicio.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytes.Length), Propietario, ct);
        await Assert.ThrowsAsync<IOException>(() => SubirContenidoAsync(servicio, sesion, bytes));
        var claveFallida = (await db.Archivos.AsNoTracking().SingleAsync(ct)).ClaveObjeto;
        var temporal = await real.ObtenerAsync(Bucket, claveFallida, ct);
        Assert.NotNull(temporal);
        await temporal.Contenido.DisposeAsync();
        proveedor.FallarTrasSubir = false;
        proveedor.FallarBorrado = false;
        await SubirContenidoAsync(servicio, sesion, bytes);
        await servicio.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        var actual = await db.Archivos.AsNoTracking().SingleAsync(ct);
        Assert.NotEqual(claveFallida, actual.ClaveObjeto);
        using var ajeno = new MemoryStream(bytes);
        await real.SubirAsync(Bucket, "archivos/objeto-ajeno", ajeno, "application/pdf", bytes.Length, ct);
        Assert.Equal(1, await servicio.LimpiarAsync(DateTimeOffset.UtcNow.AddHours(1), ct));
        Assert.Null(await real.ObtenerAsync(Bucket, claveFallida, ct));
        var remotoAjeno = await real.ObtenerAsync(Bucket, "archivos/objeto-ajeno", ct);
        Assert.NotNull(remotoAjeno);
        await remotoAjeno.Contenido.DisposeAsync();
        Assert.NotNull(await servicio.AbrirDescargaAsync(sesion.ArchivoId, ct));
    }

    [Fact]
    public async Task Review_vencido_se_reintenta_sin_lock_db_y_resultado_anterior_no_publica()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var detenido = new AntivirusDetenido();
        var a = CrearServicio(dbA, detenido);
        var b = CrearServicio(dbB);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytes.Length), Propietario, ct);
        await SubirContenidoAsync(a, sesion, bytes);
        var revision = a.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        await detenido.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        ArchivoDto publicado;
        try
        {
            Assert.Null(await b.AbrirDescargaAsync(sesion.ArchivoId, ct));
            Assert.Equal(0, await b.LimpiarAsync(DateTimeOffset.UtcNow.AddHours(1), ct));
            await using (var transaccion = await dbB.Database.BeginTransactionAsync(ct))
            {
                await dbB.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='1s'", ct);
                await dbB.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({sesion.ArchivoId.ToString()}, 8361))", ct);
                await transaccion.RollbackAsync(ct);
            }
            await dbB.Archivos.Where(x => x.Id == sesion.ArchivoId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservaHasta, DateTimeOffset.UtcNow.AddMinutes(-1)), ct);
            publicado = await b.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        }
        finally { detenido.Continuar.TrySetResult(); }
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => revision);
        Assert.Equal(publicado, await b.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct));
    }
}
