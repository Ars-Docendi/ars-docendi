using System.Security.Cryptography;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Storage.Infrastructure;

public sealed class ServicioAlmacenamientoArchivos(
    AlmacenamientoDbContext db,
    IProveedorObjetos objetos,
    IAntivirusArchivos antivirus,
    IOptions<AlmacenamientoOptions> opciones,
    ILogger<ServicioAlmacenamientoArchivos> logger) : IAlmacenamientoArchivos
{
    private readonly AlmacenamientoOptions config = opciones.Value;

    public async Task<SesionCargaArchivoDto> IniciarCargaAsync(IniciarCargaArchivoDto datos, Guid propietarioId, CancellationToken ct)
    {
        ReglasArchivos.ValidarInicio(datos, config);
        var id = Guid.NewGuid();
        var expira = DateTimeOffset.UtcNow.AddSeconds(config.ExpiracionCargaSegundos);
        var entidad = new ArchivoEntidad
        {
            Id = id,
            Proposito = datos.Proposito,
            Ambiente = config.Ambiente,
            Bucket = config.Bucket,
            ClaveObjeto = $"archivos/{id:N}",
            NombreOriginal = ReglasArchivos.SanitizarNombre(datos.NombreOriginal),
            MimeDeclarado = datos.MimeDeclarado.Trim().ToLowerInvariant(),
            TamanoBytes = datos.TamanoBytes,
            Estado = EstadosArchivo.Pendiente,
            PropietarioId = propietarioId,
            ExpiraEn = expira,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        db.Archivos.Add(entidad);
        await db.SaveChangesAsync(ct);
        var url = await objetos.CrearUrlSubidaAsync(entidad.Bucket, entidad.ClaveObjeto, config.ExpiracionCargaSegundos, ct);
        logger.LogInformation("Sesión de carga {ArchivoId} iniciada para propósito {Proposito}", id, datos.Proposito);
        return new SesionCargaArchivoDto(id, url, expira);
    }

    public async Task<ArchivoDto> ConfirmarCargaAsync(ConfirmarCargaArchivoDto datos, Guid propietarioId, CancellationToken ct)
    {
        var entidad = await db.Archivos.SingleOrDefaultAsync(x => x.Id == datos.ArchivoId, ct)
            ?? throw Error(TipoErrorAplicacion.NoEncontrado, "archivo-not-found", "No se encontró el archivo.");
        if (entidad.PropietarioId != propietarioId)
            throw Error(TipoErrorAplicacion.Prohibido, "archivo-forbidden", "El archivo no pertenece al actor.");
        if (entidad.Estado == EstadosArchivo.Disponible) return Mapear(entidad);
        if (entidad.Estado != EstadosArchivo.Pendiente || entidad.ExpiraEn <= DateTimeOffset.UtcNow)
            throw Error(TipoErrorAplicacion.Conflicto, "archivo-upload-expired", "La sesión de carga expiró o ya no es utilizable.");

        var objeto = await objetos.ObtenerAsync(entidad.Bucket, entidad.ClaveObjeto, ct)
            ?? throw Error(TipoErrorAplicacion.Validacion, "archivo-object-missing", "No se encontró el objeto subido.");
        using var contenidoObjeto = objeto.Contenido;
        if (contenidoObjeto.CanSeek) contenidoObjeto.Position = 0;
        using var memoria = new MemoryStream();
        await contenidoObjeto.CopyToAsync(memoria, ct);
        var bytes = memoria.ToArray();
        var mime = ReglasArchivos.DetectarMime(bytes);
        var hash = ReglasArchivos.CalcularSha256(bytes);
        try
        {
            ReglasArchivos.ValidarContenido(entidad.Proposito, mime, objeto.TamanoBytes, config);
            if (datos.TamanoBytes is { } tamano && tamano != objeto.TamanoBytes)
                throw Error(TipoErrorAplicacion.Validacion, "archivo-size-mismatch", "El tamaño confirmado no coincide con el objeto.");
            if (datos.Sha256 is { Length: > 0 } esperado && !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(hash), System.Text.Encoding.ASCII.GetBytes(esperado.Trim().ToLowerInvariant())))
                throw Error(TipoErrorAplicacion.Validacion, "archivo-hash-mismatch", "El hash confirmado no coincide con el objeto.");
        }
        catch (ExcepcionAplicacion ex)
        {
            entidad.MimeDetectado = mime;
            entidad.TamanoBytes = objeto.TamanoBytes;
            entidad.Sha256 = hash;
            entidad.Estado = EstadosArchivo.Rechazado;
            entidad.RevisadoEn = DateTimeOffset.UtcNow;
            entidad.MotivoRevision = ex.Codigo;
            await db.SaveChangesAsync(ct);
            throw;
        }

        memoria.Position = 0;
        var escaneo = await antivirus.AnalizarAsync(memoria, ct);
        entidad.MimeDetectado = mime;
        entidad.TamanoBytes = objeto.TamanoBytes;
        entidad.Sha256 = hash;
        entidad.ConfirmadoEn = DateTimeOffset.UtcNow;
        entidad.RevisadoEn = DateTimeOffset.UtcNow;
        entidad.MotivoRevision = escaneo.Motivo;
        entidad.Estado = escaneo.Disponible ? EstadosArchivo.Disponible : EstadosArchivo.Rechazado;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Archivo {ArchivoId} confirmado con estado {Estado}", entidad.Id, entidad.Estado);
        return Mapear(entidad);
    }

    public async Task<ArchivoDto?> ObtenerAsync(Guid archivoId, CancellationToken ct) =>
        await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == archivoId, ct) is { } entidad ? Mapear(entidad) : null;

    public async Task<bool> EsPropietarioAsync(Guid archivoId, Guid propietarioId, CancellationToken ct) =>
        await db.Archivos.AsNoTracking().AnyAsync(x => x.Id == archivoId && x.PropietarioId == propietarioId, ct);

    public async Task<ArchivoDto> RequerirDisponibleDePropietarioAsync(Guid archivoId, string proposito, Guid propietarioId, CancellationToken ct)
    {
        var archivo = await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == archivoId, ct)
            ?? throw Error(TipoErrorAplicacion.NoEncontrado, "archivo-not-found", "No se encontró el archivo.");
        if (archivo.PropietarioId != propietarioId)
            throw Error(TipoErrorAplicacion.Prohibido, "archivo-forbidden", "El archivo no pertenece al actor.");
        if (archivo.Estado != EstadosArchivo.Disponible || !string.Equals(archivo.Proposito, proposito, StringComparison.Ordinal))
            throw Error(TipoErrorAplicacion.Validacion, "archivo-not-available", "El archivo no está disponible para ese propósito.");
        return Mapear(archivo);
    }

    public async Task<DescargaArchivo?> AbrirDescargaAsync(Guid archivoId, CancellationToken ct)
    {
        var archivo = await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == archivoId, ct);
        if (archivo is null || archivo.Estado != EstadosArchivo.Disponible) return null;
        var objeto = await objetos.ObtenerAsync(archivo.Bucket, archivo.ClaveObjeto, ct);
        return objeto is null ? null : new DescargaArchivo(objeto.Contenido, archivo.NombreOriginal, archivo.MimeDetectado ?? archivo.MimeDeclarado, objeto.TamanoBytes);
    }

    public async Task EliminarAsync(Guid archivoId, Guid propietarioId, CancellationToken ct)
    {
        var archivo = await db.Archivos.SingleOrDefaultAsync(x => x.Id == archivoId, ct)
            ?? throw Error(TipoErrorAplicacion.NoEncontrado, "archivo-not-found", "No se encontró el archivo.");
        if (archivo.PropietarioId != propietarioId)
            throw Error(TipoErrorAplicacion.Prohibido, "archivo-forbidden", "El archivo no pertenece al actor.");
        if (archivo.Estado == EstadosArchivo.Eliminado) return;
        await objetos.EliminarAsync(archivo.Bucket, archivo.ClaveObjeto, ct);
        archivo.Estado = EstadosArchivo.Eliminado;
        archivo.EliminadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> LimpiarAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var candidatas = await db.Archivos.Where(x =>
                (x.Estado == EstadosArchivo.Pendiente && x.ExpiraEn < ahora)
                || (x.Estado == EstadosArchivo.Rechazado && x.RevisadoEn != null && x.RevisadoEn < ahora)
                || (x.Estado == EstadosArchivo.Disponible && x.EliminadoEn != null && x.EliminadoEn < ahora))
            .ToListAsync(ct);
        foreach (var archivo in candidatas)
        {
            await objetos.EliminarAsync(archivo.Bucket, archivo.ClaveObjeto, ct);
            archivo.Estado = EstadosArchivo.Eliminado;
            archivo.EliminadoEn ??= ahora;
        }

        var clavesConservadas = await db.Archivos
            .Where(x => x.Ambiente == config.Ambiente && x.Bucket == config.Bucket && x.Estado != EstadosArchivo.Eliminado)
            .Select(x => x.ClaveObjeto)
            .ToHashSetAsync(ct);
        var huérfanos = (await objetos.ListarAsync(config.Bucket, "archivos/", ct))
            .Where(x => x.UltimaModificacion < ahora && !clavesConservadas.Contains(x.Clave))
            .ToArray();
        foreach (var huérfano in huérfanos)
            await objetos.EliminarAsync(config.Bucket, huérfano.Clave, ct);

        if (candidatas.Count > 0) await db.SaveChangesAsync(ct);
        return candidatas.Count + huérfanos.Length;
    }

    private static ArchivoDto Mapear(ArchivoEntidad x) => new(
        x.Id, x.Proposito, x.NombreOriginal, x.MimeDeclarado, x.MimeDetectado,
        x.TamanoBytes, x.Sha256, x.Estado, x.CreadoEn, x.ConfirmadoEn);

    private static ExcepcionAplicacion Error(TipoErrorAplicacion tipo, string codigo, string mensaje) =>
        new(tipo, codigo, mensaje);
}
