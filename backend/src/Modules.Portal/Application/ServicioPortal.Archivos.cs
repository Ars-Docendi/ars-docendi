using ArsDocendi.Storage.Contracts;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Domain;
using Modules.Portal.Repositories;

namespace Modules.Portal.Application;

public sealed partial class ServicioPortal
{
    public async Task<CvDto> GuardarCvAsync(GuardarCvDto datos, CancellationToken ct)
    {
        var perfil = await PerfilActualAsync(ct, "cv");
        if (perfil.Cv is not null) repositorio.EsperarVersion(perfil.Cv, datos.Version);
        else if (datos.Version is not null) throw RepositorioPortal.Conflicto();
        var anterior = perfil.Cv?.ArchivoId;
        var nuevo = datos.ArchivoId == Guid.Empty ? (Guid?)null : datos.ArchivoId;
        var resultado = await ProtegerArchivosAsync(anterior, nuevo, async token =>
        {
            var archivo = nuevo is { } id ? await RequerirArchivoAsync(id, anterior, PropositosArchivo.Cv, token) : null;
            if (archivo is null && string.IsNullOrWhiteSpace(datos.LegacyNombre))
                await RequerirArchivoPropioAsync(datos.ArchivoId, PropositosArchivo.Cv, token);
            if (perfil.Cv is null)
            {
                perfil.Cv = new Cv { Id = Guid.NewGuid(), PerfilId = perfil.Id, Nombre = archivo?.NombreOriginal ?? datos.LegacyNombre!.Trim() };
                repositorio.Agregar(perfil.Cv);
            }
            perfil.Cv.Nombre = archivo?.NombreOriginal ?? datos.LegacyNombre!.Trim();
            perfil.Cv.Uri = archivo is null ? datos.LegacyUri : null;
            perfil.Cv.ArchivoId = archivo?.Id;
            perfil.Cv.FechaCarga = DateTimeOffset.UtcNow;
            await repositorio.GuardarAsync(token);
            return MapearCv(perfil.Cv, archivo);
        }, ct);
        if (anterior is { } anteriorId && anterior != nuevo) await RetenerAsync(anteriorId, ct);
        return resultado;
    }

    public async Task<DescargaArchivo?> DescargarCvAsync(CancellationToken ct)
    {
        var perfil = await repositorio.ObtenerSeccionAsync((await PersonaActualAsync(ct)).Id, "cv", ct);
        return perfil?.Cv?.ArchivoId is { } archivoId ? await almacenamiento.AbrirDescargaAsync(archivoId, ct) : null;
    }

    public async Task EliminarCvAsync(CancellationToken ct, uint? version = null)
    {
        var perfil = await repositorio.ObtenerSeccionAsync((await PersonaActualAsync(ct)).Id, "cv", ct);
        if (perfil?.Cv is null) return; // Eliminar una asociación inexistente es idempotente.
        repositorio.EsperarVersion(perfil.Cv, version);
        var archivo = perfil.Cv.ArchivoId;
        await ProtegerArchivosAsync(archivo, null, async token =>
        {
            repositorio.Eliminar(perfil.Cv); await repositorio.GuardarAsync(token); return true;
        }, ct);
        if (archivo is { } id) await RetenerAsync(id, ct);
    }

    private Task<T> ProtegerArchivosAsync<T>(Guid? anterior, Guid? nuevo, Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        var ids = new[] { anterior, nuevo }.OfType<Guid>().Distinct().Order().ToArray();
        if (ids.Length == 0) return operacion(ct);
        return (proteccionArchivos ?? almacenamiento as IProteccionArchivos
            ?? throw new InvalidOperationException("Falta la protección transaccional de archivos."))
            .EjecutarAsync(ids, operacion, ct);
    }

    private Task<bool> RetenerAsync(Guid archivo, CancellationToken ct) =>
        (retencionArchivos ?? almacenamiento as IRetencionArchivos
            ?? throw new InvalidOperationException("Falta la política de retención de archivos."))
            .EliminarSiSinReferenciasAsync(archivo, UsuarioIdActual(), ct);
}
