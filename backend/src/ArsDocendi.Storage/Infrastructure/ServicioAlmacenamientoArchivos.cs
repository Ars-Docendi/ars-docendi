using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Storage.Infrastructure;

public sealed partial class ServicioAlmacenamientoArchivos(
    AlmacenamientoDbContext db,
    IProveedorObjetos objetos,
    IAntivirusArchivos antivirus,
    IOptions<AlmacenamientoOptions> opciones,
    ILogger<ServicioAlmacenamientoArchivos> logger,
    IEnumerable<IConsultasReferenciasArchivos>? consultasReferencias = null,
    IProteccionArchivos? proteccion = null) : IAlmacenamientoArchivos, IRetencionArchivos, IConsultasArchivos
{
    private readonly AlmacenamientoOptions config = opciones.Value;

    public async Task<SesionCargaArchivoDto> IniciarCargaAsync(IniciarCargaArchivoDto datos, Guid propietarioId, CancellationToken ct)
    {
        ReglasArchivos.ValidarInicio(datos, config);
        var id = Guid.NewGuid();
        var expira = DateTimeOffset.UtcNow.AddSeconds(config.ExpiracionCargaSegundos);
        db.Archivos.Add(new ArchivoEntidad
        {
            Id = id, Proposito = datos.Proposito, Ambiente = config.Ambiente, Bucket = config.Bucket,
            ClaveObjeto = $"archivos/{id:N}", NombreOriginal = ReglasArchivos.SanitizarNombre(datos.NombreOriginal),
            MimeDeclarado = datos.MimeDeclarado.Trim().ToLowerInvariant(), TamanoBytes = datos.TamanoBytes,
            Estado = EstadosArchivo.Pendiente, PropietarioId = propietarioId, ExpiraEn = expira,
            CreadoEn = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Sesión de carga {ArchivoId} iniciada", id);
        return new(id, $"/api/archivos/cargas/{id:N}/objeto", expira);
    }

    public async Task SubirAsync(Guid archivoId, Guid propietarioId, Stream contenido, string? mimeDeclarado, long? tamanoDeclarado, CancellationToken ct)
    {
        var archivo = await RequerirPropioAsync(archivoId, propietarioId, ct);
        var ahora = DateTimeOffset.UtcNow;
        var intento = Guid.NewGuid();
        var clave = $"archivos/{archivoId:N}/intentos/{intento:N}";
        var hasta = VencimientoReserva(archivo, ahora);
        var reservado = await db.Archivos.Where(x => x.Id == archivoId && x.Estado == EstadosArchivo.Pendiente
            && x.ExpiraEn > ahora && (x.FaseCarga == null || (x.FaseCarga == "subiendo" && x.ReservaHasta < ahora)))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IntentoId, intento).SetProperty(x => x.ReservaId, intento)
                .SetProperty(x => x.ClaveObjeto, clave).SetProperty(x => x.FaseCarga, "subiendo").SetProperty(x => x.ReservaHasta, hasta), ct);
        if (reservado != 1) throw Conflicto();
        try
        {
            using var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = await contenido.ReadAsync(buffer, ct)) > 0)
            {
                if (memoria.Length + leidos > archivo.TamanoBytes) throw TamanoInvalido();
                await memoria.WriteAsync(buffer.AsMemory(0, leidos), ct);
            }
            if (memoria.Length != archivo.TamanoBytes || (tamanoDeclarado is { } declarado && declarado != memoria.Length))
                throw TamanoInvalido();
            memoria.Position = 0;
            await objetos.SubirAsync(archivo.Bucket, clave, memoria, archivo.MimeDeclarado, memoria.Length, ct);
            var completado = await db.Archivos.Where(x => x.Id == archivoId && x.Estado == EstadosArchivo.Pendiente
                && x.ReservaId == intento && x.FaseCarga == "subiendo" && x.ReservaHasta > DateTimeOffset.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FaseCarga, "cargado").SetProperty(x => x.ReservaHasta, (DateTimeOffset?)null), ct);
            if (completado != 1) throw Conflicto();
        }
        catch
        {
            // El intento nunca publicado es propio: no se reutiliza esta clave aunque la subida remota terminara.
            await db.Archivos.Where(x => x.Id == archivoId && x.ReservaId == intento && x.FaseCarga == "subiendo")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FaseCarga, (string?)null).SetProperty(x => x.ReservaId, (Guid?)null)
                    .SetProperty(x => x.ReservaHasta, (DateTimeOffset?)null), CancellationToken.None);
            // Si el proveedor falla al compensar, queda un temporal reconocible para limpieza.
            try { await objetos.EliminarAsync(archivo.Bucket, clave, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning("Temporal de {ArchivoId} retenido: {TipoError}", archivoId, ex.GetType().Name); }
            throw;
        }
    }

    public async Task<ArchivoDto?> ObtenerAsync(Guid archivoId, CancellationToken ct) =>
        await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == archivoId, ct) is { } archivo ? Mapear(archivo) : null;

    public Task<bool> EsPropietarioAsync(Guid archivoId, Guid propietarioId, CancellationToken ct) =>
        db.Archivos.AsNoTracking().AnyAsync(x => x.Id == archivoId && x.PropietarioId == propietarioId, ct);

    public async Task<ArchivoDto> RequerirDisponibleDePropietarioAsync(Guid archivoId, string proposito, Guid propietarioId, CancellationToken ct)
    {
        var archivo = await RequerirPropioAsync(archivoId, propietarioId, ct);
        if (archivo.Estado != EstadosArchivo.Disponible || archivo.Proposito != proposito)
            throw Error(TipoErrorAplicacion.Validacion, "archivo-not-available", "El archivo no está disponible para ese propósito.");
        return Mapear(archivo);
    }

    public async Task<DescargaArchivo?> AbrirDescargaAsync(Guid archivoId, CancellationToken ct)
    {
        var archivo = await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == archivoId, ct);
        if (archivo is null || archivo.Estado != EstadosArchivo.Disponible) return null;
        var objeto = await objetos.ObtenerAsync(archivo.Bucket, archivo.ClaveObjeto, ct);
        return objeto is null ? null : new(objeto.Contenido, archivo.NombreOriginal, archivo.MimeDetectado ?? archivo.MimeDeclarado, objeto.TamanoBytes);
    }

    private async Task<ArchivoEntidad> RequerirPropioAsync(Guid id, Guid propietario, CancellationToken ct)
    {
        var archivo = await db.Archivos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error(TipoErrorAplicacion.NoEncontrado, "archivo-not-found", "No se encontró el archivo.");
        if (archivo.PropietarioId != propietario)
            throw Error(TipoErrorAplicacion.Prohibido, "archivo-forbidden", "El archivo no pertenece al actor.");
        return archivo;
    }

    private static ArchivoDto Mapear(ArchivoEntidad x) => new(x.Id, x.Proposito, x.NombreOriginal, x.MimeDeclarado,
        x.MimeDetectado, x.TamanoBytes, x.Sha256, x.Estado, x.CreadoEn, x.ConfirmadoEn);
    private DateTimeOffset VencimientoReserva(ArchivoEntidad archivo, DateTimeOffset ahora) =>
        ahora.AddSeconds(Math.Min(Math.Max(1, config.DuracionReservaSegundos), Math.Max(0, (archivo.ExpiraEn - ahora).TotalSeconds / 2)));
    private static ExcepcionAplicacion Error(TipoErrorAplicacion tipo, string codigo, string mensaje) => new(tipo, codigo, mensaje);
    private static ExcepcionAplicacion Conflicto() => Error(TipoErrorAplicacion.Conflicto, "archivo-upload-expired", "La sesión expiró o tiene una operación incompatible en curso.");
    private static ExcepcionAplicacion TamanoInvalido() => Error(TipoErrorAplicacion.Validacion, "archivo-size-mismatch", "El tamaño no coincide con el declarado.");
}
