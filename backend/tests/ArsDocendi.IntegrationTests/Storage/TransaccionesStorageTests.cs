using System.Diagnostics;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    [Fact]
    public async Task Rollback_multicontexto_revierte_storage_y_portal_sobre_una_conexion_real()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var portal = PostgresFixture.CrearPortal(cadena);
        var proteccion = new ProteccionArchivos(db, [new ParticipantePortal(portal)]);
        var servicio = CrearServicio(db, proteccion: proteccion);
        var id = await SubirPdfAsync(servicio, "atomico.pdf");
        var perfil = PerfilConCv(id);
        await Assert.ThrowsAsync<IOException>(() => proteccion.EjecutarAsync<bool>([id], async token =>
        {
            Assert.Same(db.Database.GetDbConnection(), portal.Database.GetDbConnection());
            Assert.NotNull(portal.Database.CurrentTransaction);
            await servicio.RequerirDisponibleDePropietarioAsync(id, PropositosArchivo.Cv, Propietario, token);
            portal.Perfiles.Add(perfil);
            await portal.SaveChangesAsync(token);
            await db.Archivos.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.MotivoRevision, "mutacion-que-se-revierte"), token);
            throw new IOException("Fallo después de ambas escrituras reales");
        }, ct));
        await using var comprobar = PostgresFixture.CrearPortal(cadena);
        Assert.False(await comprobar.Perfiles.AnyAsync(x => x.Id == perfil.Id, ct));
        Assert.False(await comprobar.Cvs.AnyAsync(x => x.ArchivoId == id, ct));
        Assert.Null((await db.Archivos.AsNoTracking().SingleAsync(x => x.Id == id, ct)).MotivoRevision);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Null(portal.Database.CurrentTransaction);
        await servicio.EliminarAsync(id, Propietario, ct);
    }

    [Fact]
    public async Task Asociacion_confirmada_serializa_delete_y_conserva_bytes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var portal = PostgresFixture.CrearPortal(cadena);
        var proteccion = new ProteccionArchivos(dbA, [new ParticipantePortal(portal)]);
        var a = CrearServicio(dbA, proteccion: proteccion);
        var b = CrearServicio(dbB);
        var id = await SubirPdfAsync(a, "asociado.pdf");
        var guardado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asociar = proteccion.EjecutarAsync([id], async token =>
        {
            await a.RequerirDisponibleDePropietarioAsync(id, PropositosArchivo.Cv, Propietario, token);
            portal.Perfiles.Add(PerfilConCv(id));
            await portal.SaveChangesAsync(token);
            guardado.TrySetResult();
            await continuar.Task.WaitAsync(token);
            return true;
        }, ct);
        await guardado.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        await dbB.Database.OpenConnectionAsync(ct);
        var pid = ((NpgsqlConnection)dbB.Database.GetDbConnection()).ProcessID;
        var eliminar = b.EliminarAsync(id, Propietario, ct);
        try
        {
            await EsperarBloqueoAsync(pid, ct);
            Assert.False(eliminar.IsCompleted);
        }
        finally { continuar.TrySetResult(); }
        Assert.True(await asociar);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => eliminar);
        Assert.Equal("archivo-referenced", error.Codigo);
        var descarga = await b.AbrirDescargaAsync(id, ct);
        Assert.NotNull(descarga);
        await descarga.Contenido.DisposeAsync();
    }

    [Fact]
    public async Task Delete_reservado_impide_asociacion_nueva_aun_si_falla_proveedor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var portal = PostgresFixture.CrearPortal(cadena);
        var proteccion = new ProteccionArchivos(db, [new ParticipantePortal(portal)]);
        var proveedor = new ProveedorConFallo(new ProveedorSeaweedFs(cliente!));
        var servicio = CrearServicio(db, proveedorAlternativo: proveedor, proteccion: proteccion);
        var id = await SubirPdfAsync(servicio, "indisponible.pdf");
        proveedor.FallarBorrado = true;
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EliminarAsync(id, Propietario, ct));
        await Assert.ThrowsAsync<ExcepcionAplicacion>(() => proteccion.EjecutarAsync([id], async token =>
        {
            await servicio.RequerirDisponibleDePropietarioAsync(id, PropositosArchivo.Cv, Propietario, token);
            portal.Perfiles.Add(PerfilConCv(id));
            await portal.SaveChangesAsync(token);
            return true;
        }, ct));
        Assert.False(await portal.Cvs.AnyAsync(x => x.ArchivoId == id, ct));
    }

    [Fact]
    public async Task Multiples_ids_en_orden_inverso_se_protegen_sin_deadlock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbA = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var dbB = PostgresFixture.CrearAlmacenamiento(cadena);
        var a = new ProteccionArchivos(dbA, []);
        var b = new ProteccionArchivos(dbB, []);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var dentro = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primero = a.EjecutarAsync(ids, async token =>
        {
            dentro.TrySetResult();
            await continuar.Task.WaitAsync(token);
            return 1;
        }, ct);
        await dentro.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        await dbB.Database.OpenConnectionAsync(ct);
        var pid = ((NpgsqlConnection)dbB.Database.GetDbConnection()).ProcessID;
        var segundo = b.EjecutarAsync(ids.Reverse().ToArray(), _ => Task.FromResult(2), ct);
        try { await EsperarBloqueoAsync(pid, ct); }
        finally { continuar.TrySetResult(); }
        Assert.Equal(1, await primero);
        Assert.Equal(2, await segundo);
    }

    private async Task EsperarBloqueoAsync(int pid, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        var limite = Stopwatch.StartNew();
        while (limite.Elapsed < TimeSpan.FromSeconds(5))
        {
            await using var comando = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid=@pid AND wait_event_type='Lock')", conexion);
            comando.Parameters.AddWithValue("pid", pid);
            if ((bool)(await comando.ExecuteScalarAsync(ct))!) return;
            await Task.Delay(10, ct);
        }
        Assert.Fail("La segunda sesión no llegó al bloqueo PostgreSQL esperado.");
    }
}
