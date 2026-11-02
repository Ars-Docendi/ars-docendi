using System.Security.Cryptography;
using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    [Fact]
    public async Task Control_inicial_obsoleto_no_envia_PUT_tras_confirmacion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        var a = CrearServicio(dbA);
        var bytesA = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var bytesB = Encoding.UTF8.GetBytes("%PDF-1.7\nversion B\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytesA.Length), Propietario, ct);

        var barrera = new BarreraLecturaInicial();
        var opcionesB = new DbContextOptionsBuilder<AlmacenamientoDbContext>()
            .UseNpgsql(cadena, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AlmacenamientoDbContext.Schema))
            .AddInterceptors(barrera)
            .Options;
        await using var dbB = new AlmacenamientoDbContext(opcionesB);
        var proveedorB = new ProveedorConRegistro(new ProveedorSeaweedFs(cliente!));
        var b = CrearServicio(dbB, proveedorAlternativo: proveedorB);
        using var cuerpoB = new MemoryStream(bytesB);
        var cargaB = b.SubirAsync(sesion.ArchivoId, Propietario, cuerpoB, null, bytesB.Length, ct);

        Exception? errorB;
        try
        {
            await barrera.LecturaEjecutada.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            await SubirContenidoAsync(a, sesion, bytesA);
            var publicado = await a.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
            barrera.Continuar.TrySetResult();
            errorB = await Record.ExceptionAsync(async () => await cargaB.WaitAsync(TimeSpan.FromSeconds(15), ct));

            var conflicto = Assert.IsType<ExcepcionAplicacion>(errorB);
            Assert.Equal(TipoErrorAplicacion.Conflicto, conflicto.Tipo);
            Assert.False(proveedorB.PuestaCompleta.Task.IsCompleted);
            var descarga = await a.AbrirDescargaAsync(sesion.ArchivoId, ct);
            Assert.NotNull(descarga);
            using var memoria = new MemoryStream();
            await descarga.Contenido.CopyToAsync(memoria, ct);
            await descarga.Contenido.DisposeAsync();
            Assert.Equal(bytesA, memoria.ToArray());
            Assert.Equal("application/pdf", descarga.Mime);
            Assert.Equal(bytesA.Length, descarga.TamanoBytes);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytesA)).ToLowerInvariant(), publicado.Sha256);
        }
        finally
        {
            barrera.Continuar.TrySetResult();
            if (!cargaB.IsCompleted)
                _ = await Record.ExceptionAsync(async () => await cargaB.WaitAsync(TimeSpan.FromSeconds(15), ct));
        }
    }

    [Fact]
    public async Task Put_de_intento_vencido_no_sustituye_bytes_publicados_y_analizados()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var a = CrearServicio(dbA);
        var bytesA = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var bytesB = Encoding.UTF8.GetBytes("%PDF-1.7\nversion B\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytesA.Length), Propietario, ct);

        var proveedorB = new ProveedorConRegistro(new ProveedorSeaweedFs(cliente!));
        var b = CrearServicio(dbB, proveedorAlternativo: proveedorB);
        using var cuerpoB = new CuerpoDetenido(bytesB);
        var cargaB = b.SubirAsync(sesion.ArchivoId, Propietario, cuerpoB, null, bytesB.Length, ct);
        await cuerpoB.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        var claveB = await dbB.Archivos.AsNoTracking().Select(x => x.ClaveObjeto).SingleAsync(ct);

        // Simula que vence la reserva de B mientras su solicitud sigue leyendo el cuerpo.
        await dbA.Archivos.Where(x => x.Id == sesion.ArchivoId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservaHasta, DateTimeOffset.UtcNow.AddMinutes(-1)), ct);
        await SubirContenidoAsync(a, sesion, bytesA);
        var publicado = await a.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);

        Exception? errorB;
        try
        {
            cuerpoB.Continuar.TrySetResult();
            var puesta = await proveedorB.PuestaCompleta.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            Assert.Equal(claveB, puesta.Clave);
            errorB = await Record.ExceptionAsync(async () => await cargaB.WaitAsync(TimeSpan.FromSeconds(30), ct));
        }
        finally
        {
            cuerpoB.Continuar.TrySetResult();
            if (!cargaB.IsCompleted)
                _ = await Record.ExceptionAsync(async () => await cargaB.WaitAsync(TimeSpan.FromSeconds(30), ct));
        }

        var conflicto = Assert.IsType<ExcepcionAplicacion>(errorB);
        Assert.Equal(TipoErrorAplicacion.Conflicto, conflicto.Tipo);
        var descarga = await a.AbrirDescargaAsync(sesion.ArchivoId, ct);
        Assert.NotNull(descarga);
        using var memoria = new MemoryStream();
        await descarga.Contenido.CopyToAsync(memoria, ct);
        await descarga.Contenido.DisposeAsync();
        Assert.Equal(bytesA, memoria.ToArray());
        Assert.Equal("application/pdf", descarga.Mime);
        Assert.Equal(bytesA.Length, descarga.TamanoBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytesA)).ToLowerInvariant(), publicado.Sha256);
        var persistido = await dbA.Archivos.AsNoTracking().SingleAsync(ct);
        Assert.Equal(EstadosArchivo.Disponible, persistido.Estado);
        Assert.NotEqual(claveB, persistido.ClaveObjeto);
        Assert.Equal(publicado.Sha256, persistido.Sha256);
        Assert.Null(await new ProveedorSeaweedFs(cliente!).ObtenerAsync(Bucket, claveB, ct));
    }

    [Fact]
    public async Task Put_repetido_tras_subida_se_rechaza_y_preserva_archivo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var a = CrearServicio(dbA);
        var b = CrearServicio(dbB);
        var bytesA = Encoding.UTF8.GetBytes("%PDF-1.7\nversion A\n");
        var bytesB = Encoding.UTF8.GetBytes("%PDF-1.7\nversion B\n");
        var sesion = await a.IniciarCargaAsync(new(PropositosArchivo.Cv, "cv.pdf", "application/pdf", bytesA.Length), Propietario, ct);
        await SubirContenidoAsync(a, sesion, bytesA);
        using var cuerpoB = new CuerpoDetenido(bytesB);
        var cargaB = b.SubirAsync(sesion.ArchivoId, Propietario, cuerpoB, null, bytesB.Length, ct);
        await Task.WhenAny(cuerpoB.Iniciado.Task, cargaB).WaitAsync(TimeSpan.FromSeconds(15), ct);
        var publicado = await a.ConfirmarCargaAsync(new(sesion.ArchivoId), Propietario, ct);
        cuerpoB.Continuar.TrySetResult();
        try { await cargaB; }
        catch (ExcepcionAplicacion ex) { Assert.Equal(TipoErrorAplicacion.Conflicto, ex.Tipo); }
        var descarga = await a.AbrirDescargaAsync(sesion.ArchivoId, ct);
        Assert.NotNull(descarga);
        using var memoria = new MemoryStream();
        await descarga.Contenido.CopyToAsync(memoria, ct);
        await descarga.Contenido.DisposeAsync();
        Assert.Equal(bytesA, memoria.ToArray());
        Assert.Equal("application/pdf", descarga.Mime);
        Assert.Equal(bytesA.Length, descarga.TamanoBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(memoria.ToArray())).ToLowerInvariant(), publicado.Sha256);
    }

    [Fact]
    public async Task Borrado_falla_cerrado_sin_consumidores_requeridos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db, registrarConsumidores: false);
        var id = await SubirPdfAsync(servicio, "conservado.pdf");
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EliminarAsync(id, Propietario, ct));
        Assert.Equal("archivo-references-unavailable", error.Codigo);
        Assert.NotNull(await servicio.AbrirDescargaAsync(id, ct));
    }

    private sealed class BarreraLecturaInicial : DbCommandInterceptor
    {
        private int primeraLectura;

        public TaskCompletionSource LecturaEjecutada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
            System.Data.Common.DbCommand command,
            CommandExecutedEventData eventData,
            System.Data.Common.DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref primeraLectura, 1) == 0)
            {
                LecturaEjecutada.TrySetResult();
                await Continuar.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class ProveedorConRegistro(IProveedorObjetos real) : IProveedorObjetos
    {
        public TaskCompletionSource<(string Bucket, string Clave)> PuestaCompleta { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SubirAsync(string bucket, string clave, Stream contenido, string mime, long tamanoBytes, CancellationToken ct)
        {
            try
            {
                await real.SubirAsync(bucket, clave, contenido, mime, tamanoBytes, ct);
                PuestaCompleta.TrySetResult((bucket, clave));
            }
            catch (Exception ex)
            {
                PuestaCompleta.TrySetException(ex);
                throw;
            }
        }

        public Task<ObjetoRemoto?> ObtenerAsync(string bucket, string clave, CancellationToken ct) => real.ObtenerAsync(bucket, clave, ct);
        public Task<IReadOnlyList<ObjetoListado>> ListarAsync(string bucket, string prefijo, CancellationToken ct) => real.ListarAsync(bucket, prefijo, ct);
        public Task EliminarAsync(string bucket, string clave, CancellationToken ct) => real.EliminarAsync(bucket, clave, ct);
    }

    private sealed class CuerpoDetenido(byte[] bytes) : MemoryStream(bytes)
    {
        public TaskCompletionSource Iniciado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            Iniciado.TrySetResult();
            await Continuar.Task.WaitAsync(ct);
            return await base.ReadAsync(buffer, ct);
        }
    }
}
