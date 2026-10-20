using System.Data.Common;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Modules.Portal.Infrastructure;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed partial class AlmacenamientoSeaweedFsTests
{
    private sealed class ReferenciasSql(AlmacenamientoDbContext db, string consumidor, bool provocarError = false) : IConsultasReferenciasArchivos
    {
        public string Consumidor => consumidor;
        public Task<bool> TieneReferenciasAsync(Guid archivoId, CancellationToken ct) => provocarError
            ? db.Database.SqlQuery<bool>($"SELECT inexistente AS \"Value\" FROM portal.cvs").SingleAsync(ct)
            : consumidor == "Portal"
                ? db.Database.SqlQuery<bool>($"SELECT (EXISTS(SELECT 1 FROM portal.cvs WHERE archivo_id={archivoId}) OR EXISTS(SELECT 1 FROM portal.proyecto_documentos WHERE archivo_id={archivoId})) AS \"Value\"").SingleAsync(ct)
                : db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM designaciones.pedido_adjuntos WHERE archivo_id={archivoId}) AS \"Value\"").SingleAsync(ct);
    }

    private sealed class ParticipantePortal(PortalDbContext db) : IParticipanteTransaccionArchivos
    {
        public async Task EnlistarAsync(DbConnection conexion, DbTransaction transaccion, CancellationToken ct)
        {
            db.Database.SetDbConnection(conexion);
            await db.Database.UseTransactionAsync(transaccion, ct);
        }
        public async Task DesenlistarAsync(CancellationToken ct)
        {
            await db.Database.UseTransactionAsync(null, ct);
            db.ChangeTracker.Clear();
        }
    }

    private sealed class AntivirusDetenido : IAntivirusArchivos
    {
        public TaskCompletionSource Iniciado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Invocaciones { get; private set; }
        public async Task<ResultadoAntivirus> AnalizarAsync(Stream contenido, CancellationToken ct)
        {
            Invocaciones++;
            Iniciado.TrySetResult();
            await Continuar.Task.WaitAsync(ct);
            return new(true, true, null);
        }
    }

    private sealed class ProveedorConFallo(IProveedorObjetos real) : IProveedorObjetos
    {
        public bool FallarBorrado { get; set; }
        public bool FallarTrasSubir { get; set; }
        public Task<ObjetoRemoto?> ObtenerAsync(string bucket, string clave, CancellationToken ct) => real.ObtenerAsync(bucket, clave, ct);
        public Task<IReadOnlyList<ObjetoListado>> ListarAsync(string bucket, string prefijo, CancellationToken ct) => real.ListarAsync(bucket, prefijo, ct);
        public async Task SubirAsync(string bucket, string clave, Stream contenido, string mime, long tamano, CancellationToken ct)
        {
            await real.SubirAsync(bucket, clave, contenido, mime, tamano, ct);
            if (FallarTrasSubir) throw new IOException("Corte inyectado después del PUT real");
        }
        public Task EliminarAsync(string bucket, string clave, CancellationToken ct) => FallarBorrado
            ? throw new IOException("Corte inyectado antes del DELETE real") : real.EliminarAsync(bucket, clave, ct);
    }
}
