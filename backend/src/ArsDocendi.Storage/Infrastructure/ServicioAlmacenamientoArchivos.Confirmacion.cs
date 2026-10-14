using System.Security.Cryptography;
using System.Text;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Storage.Infrastructure;

public sealed partial class ServicioAlmacenamientoArchivos
{
    public async Task<ArchivoDto> ConfirmarCargaAsync(ConfirmarCargaArchivoDto datos, Guid propietarioId, CancellationToken ct)
    {
        var archivo = await RequerirPropioAsync(datos.ArchivoId, propietarioId, ct);
        if (archivo.Estado == EstadosArchivo.Disponible)
        {
            ValidarConfirmacion(datos, archivo.Sha256!, archivo.TamanoBytes);
            return Mapear(archivo);
        }
        var ahora = DateTimeOffset.UtcNow;
        var reserva = Guid.NewGuid();
        var tomado = await db.Archivos.Where(x => x.Id == archivo.Id && x.ExpiraEn > ahora && x.IntentoId != null
            && x.IntentoId == archivo.IntentoId && x.ClaveObjeto == archivo.ClaveObjeto
            && ((x.Estado == EstadosArchivo.Pendiente && x.FaseCarga == "cargado")
                || (x.Estado == EstadosArchivo.Cuarentena && x.FaseCarga == "revisando" && x.ReservaHasta < ahora)))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadosArchivo.Cuarentena).SetProperty(x => x.FaseCarga, "revisando")
                .SetProperty(x => x.ReservaId, reserva).SetProperty(x => x.ReservaHasta, VencimientoReserva(archivo, ahora)), ct);
        if (tomado != 1)
        {
            var actual = await RequerirPropioAsync(archivo.Id, propietarioId, ct);
            if (actual.Estado == EstadosArchivo.Disponible)
            {
                ValidarConfirmacion(datos, actual.Sha256!, actual.TamanoBytes);
                return Mapear(actual);
            }
            if (actual.Estado == EstadosArchivo.Pendiente && actual.FaseCarga == null && actual.ExpiraEn > ahora)
                throw Error(TipoErrorAplicacion.Validacion, "archivo-object-missing", "No se encontró una carga completa.");
            throw Conflicto();
        }
        var mime = (string?)null;
        var hash = (string?)null;
        try
        {
            var objeto = await objetos.ObtenerAsync(archivo.Bucket, archivo.ClaveObjeto, ct)
                ?? throw Error(TipoErrorAplicacion.Validacion, "archivo-object-missing", "No se encontró el objeto subido.");
            using var contenido = objeto.Contenido;
            using var memoria = new MemoryStream();
            await contenido.CopyToAsync(memoria, ct);
            var bytes = memoria.ToArray();
            if (bytes.LongLength != archivo.TamanoBytes || objeto.TamanoBytes != bytes.LongLength) throw TamanoInvalido();
            mime = ReglasArchivos.DetectarMime(bytes);
            hash = ReglasArchivos.CalcularSha256(bytes);
            ReglasArchivos.ValidarContenido(archivo.Proposito, mime, bytes.LongLength, config);
            ValidarConfirmacion(datos, hash, bytes.LongLength);
            memoria.Position = 0;
            var escaneo = await antivirus.AnalizarAsync(memoria, ct);
            if (!escaneo.Disponible)
                throw Error(TipoErrorAplicacion.Validacion,
                    escaneo.Motivo?.Contains("no está disponible", StringComparison.OrdinalIgnoreCase) == true
                        ? "archivo-antivirus-unavailable" : "archivo-antivirus-rejected", "El análisis antivirus no habilitó el archivo.");
            var actualizado = await db.Archivos.Where(x => x.Id == archivo.Id && x.Estado == EstadosArchivo.Cuarentena
                && x.FaseCarga == "revisando" && x.ReservaId == reserva && x.ReservaHasta > DateTimeOffset.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadosArchivo.Disponible).SetProperty(x => x.MimeDetectado, mime)
                    .SetProperty(x => x.Sha256, hash).SetProperty(x => x.ConfirmadoEn, DateTimeOffset.UtcNow)
                    .SetProperty(x => x.RevisadoEn, DateTimeOffset.UtcNow).SetProperty(x => x.FaseCarga, (string?)null)
                    .SetProperty(x => x.ReservaHasta, (DateTimeOffset?)null), ct);
            if (actualizado != 1) throw Conflicto();
            return Mapear(await RequerirPropioAsync(archivo.Id, propietarioId, ct));
        }
        catch (ExcepcionAplicacion ex) when (ex.Tipo == TipoErrorAplicacion.Validacion)
        {
            await db.Archivos.Where(x => x.Id == archivo.Id && x.ReservaId == reserva && x.FaseCarga == "revisando")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadosArchivo.Rechazado)
                    .SetProperty(x => x.MimeDetectado, mime).SetProperty(x => x.Sha256, hash)
                    .SetProperty(x => x.RevisadoEn, DateTimeOffset.UtcNow).SetProperty(x => x.MotivoRevision, ex.Codigo)
                    .SetProperty(x => x.ReservaHasta, (DateTimeOffset?)null).SetProperty(x => x.FaseCarga, (string?)null), ct);
            throw;
        }
        catch
        {
            // Un fallo de proceso real se recupera por TTL; un fallo observado permite reintentar inmediatamente la misma versión.
            await db.Archivos.Where(x => x.Id == archivo.Id && x.ReservaId == reserva && x.FaseCarga == "revisando")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadosArchivo.Pendiente).SetProperty(x => x.FaseCarga, "cargado")
                    .SetProperty(x => x.ReservaHasta, (DateTimeOffset?)null), CancellationToken.None);
            throw;
        }
    }

    private static void ValidarConfirmacion(ConfirmarCargaArchivoDto datos, string hash, long tamano)
    {
        if (datos.TamanoBytes is { } esperado && esperado != tamano) throw TamanoInvalido();
        if (datos.Sha256 is { Length: > 0 } sha && !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(sha.Trim().ToLowerInvariant())))
            throw Error(TipoErrorAplicacion.Validacion, "archivo-hash-mismatch", "El hash confirmado no coincide con el objeto.");
    }
}
