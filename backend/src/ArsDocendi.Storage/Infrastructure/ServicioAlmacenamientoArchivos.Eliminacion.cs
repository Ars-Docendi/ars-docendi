using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArsDocendi.Storage.Infrastructure;

public sealed partial class ServicioAlmacenamientoArchivos
{
    private readonly IProteccionArchivos exclusion = proteccion ?? new ProteccionArchivos(db, []);
    private readonly IConsultasReferenciasArchivos[] referencias = consultasReferencias?.ToArray() ?? [];

    public async Task EliminarAsync(Guid archivoId, Guid propietarioId, CancellationToken ct) =>
        _ = await EliminarProtegidoAsync(archivoId, propietarioId, true, false, DateTimeOffset.UtcNow, ct);

    public Task<bool> EliminarSiSinReferenciasAsync(Guid archivoId, Guid propietarioId, CancellationToken ct) =>
        EliminarProtegidoAsync(archivoId, propietarioId, false, false, DateTimeOffset.UtcNow, ct);

    private async Task<bool> EliminarProtegidoAsync(Guid id, Guid propietario, bool conflictoReferenciado, bool limpieza, DateTimeOffset ahora, CancellationToken ct)
    {
        var reservado = await exclusion.EjecutarAsync([id], async token =>
        {
            var archivo = await RequerirPropioAsync(id, propietario, token);
            if (archivo.Estado == EstadosArchivo.Eliminado) return (ArchivoEntidad?)null;
            if (limpieza && archivo.ReservaHasta > DateTimeOffset.UtcNow) return null;
            if (await TieneReferenciasAsync(id, token))
            {
                if (conflictoReferenciado)
                    throw Error(TipoErrorAplicacion.Conflicto, "archivo-referenced", "El archivo conserva referencias y no puede eliminarse.");
                return null;
            }
            // La indisponibilidad es definitiva antes del I/O remoto. CAS de revisión nunca coincide con esta fase.
            await db.Archivos.Where(x => x.Id == id && x.Estado != EstadosArchivo.Eliminado)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadosArchivo.Cuarentena)
                    .SetProperty(x => x.FaseCarga, "eliminando").SetProperty(x => x.ReservaId, (Guid?)null)
                    .SetProperty(x => x.ReservaHasta, (DateTimeOffset?)null).SetProperty(x => x.EliminadoEn, ahora), token);
            return archivo;
        }, ct);
        if (reservado is null) return false;
        try
        {
            await objetos.EliminarAsync(reservado.Bucket, reservado.ClaveObjeto, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Borrado remoto pendiente de {ArchivoId}: {TipoError}", id, ex.GetType().Name);
            throw Error(TipoErrorAplicacion.Conflicto, "archivo-delete-pending", "La eliminación remota está pendiente; puede reintentarse.");
        }
        await db.Archivos.Where(x => x.Id == id && x.FaseCarga == "eliminando")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadosArchivo.Eliminado).SetProperty(x => x.FaseCarga, (string?)null), ct);
        return true;
    }

    private async Task<bool> TieneReferenciasAsync(Guid id, CancellationToken ct)
    {
        if (referencias.GroupBy(x => x.Consumidor, StringComparer.Ordinal).Any(g => g.Count() != 1)
            || !referencias.Any(x => x.Consumidor == "Portal") || !referencias.Any(x => x.Consumidor == "Designaciones"))
            throw Error(TipoErrorAplicacion.Conflicto, "archivo-references-unavailable", "No están disponibles todos los consumidores de archivos.");
        var tiene = false;
        try
        {
            // Consultar todos: que uno sea positivo no oculta otro consumidor fallido.
            foreach (var consulta in referencias) tiene |= await consulta.TieneReferenciasAsync(id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Error(TipoErrorAplicacion.Conflicto, "archivo-references-unavailable", "No pudo verificarse la retención de archivos.");
        }
        return tiene;
    }

    public async Task<int> LimpiarAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var candidatas = await db.Archivos.AsNoTracking().Where(x => x.Ambiente == config.Ambiente && x.Bucket == config.Bucket
            && (x.Estado == EstadosArchivo.Pendiente && x.ExpiraEn < ahora
                || x.Estado == EstadosArchivo.Cuarentena && x.ExpiraEn < ahora
                || x.Estado == EstadosArchivo.Rechazado && x.RevisadoEn < ahora
                || x.FaseCarga == "eliminando"))
            .Select(x => new { x.Id, x.PropietarioId }).ToListAsync(ct);
        var eliminados = 0;
        foreach (var archivo in candidatas)
            if (await EliminarProtegidoAsync(archivo.Id, archivo.PropietarioId, false, true, ahora, ct)) eliminados++;

        // No borrar objetos arbitrarios del bucket: solo claves de intentos generadas por este protocolo
        // cuyo archivo y ambiente conocemos. Una clave anterior nunca se vuelve a reservar.
        foreach (var objeto in await objetos.ListarAsync(config.Bucket, "archivos/", ct))
        {
            var partes = objeto.Clave.Split('/');
            if (objeto.UltimaModificacion >= ahora || partes.Length != 4 || partes[0] != "archivos" || partes[2] != "intentos"
                || !Guid.TryParseExact(partes[1], "N", out var id) || !Guid.TryParseExact(partes[3], "N", out _)) continue;
            var borrar = await exclusion.EjecutarAsync([id], async token =>
            {
                var archivo = await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
                    && x.Bucket == config.Bucket && x.Ambiente == config.Ambiente, token);
                if (archivo is null || archivo.ReservaHasta > DateTimeOffset.UtcNow) return false;
                return archivo.ClaveObjeto != objeto.Clave || archivo.Estado == EstadosArchivo.Eliminado;
            }, ct);
            if (!borrar) continue;
            await objetos.EliminarAsync(config.Bucket, objeto.Clave, ct);
            eliminados++;
        }
        return eliminados;
    }

    public async Task<IReadOnlyDictionary<Guid, ArchivoDto>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> archivoIds, CancellationToken ct)
    {
        var ids = archivoIds.Distinct().ToArray();
        var archivos = await db.Archivos.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return archivos.ToDictionary(x => x.Id, Mapear);
    }
}
