using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArsDocendi.Shared.Persistencia;

namespace ArsDocendi.Migraciones;

/// <summary>Salida determinística del preview; no requiere filesystem compartido con Docker.</summary>
public static class PreviewMigraciones
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<IReadOnlyDictionary<string, string>> GenerarAsync(
        IReadOnlyList<IMigradorModulo> migradores,
        IReadOnlyList<EstadoMigracionesModulo> estados, string sha, CancellationToken ct)
    {
        var archivos = new Dictionary<string, string>(StringComparer.Ordinal);
        var scripts = new List<object>();
        for (var i = 0; i < migradores.Count; i++)
        {
            var sql = await migradores[i].GenerarScriptAsync(ct);
            var posterior = await migradores[i].ConsultarAsync(ct);
            if (!posterior.Aplicadas.SequenceEqual(estados[i].Aplicadas, StringComparer.Ordinal))
                throw new InvalidOperationException("El historial cambió durante el preview; debe generarse otra vez.");
            if (sql.Length == 0) continue;
            var nombre = $"{i + 1:D2}_{migradores[i].Contexto}.sql";
            archivos.Add(nombre, sql);
            scripts.Add(new { contexto = migradores[i].Contexto, archivo = nombre, sha256 = Hash(sql) });
        }
        archivos.Add("manifiesto.json", JsonSerializer.Serialize(new
        {
            formato = "arsdocendi-migraciones/v1", sha,
            estadoSha256 = Hash(JsonSerializer.Serialize(estados, Json)),
            contextos = estados, scripts, noOp = scripts.Count == 0,
        }, Json));
        return archivos;
    }

    public static async Task ExportarAsync(IReadOnlyDictionary<string, string> archivos, string directorio, CancellationToken ct)
    {
        foreach (var nombre in archivos.Keys)
            if (nombre.Length == 0 || nombre is "." or ".." || nombre.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(nombre))
                throw new ArgumentException("El preview sólo admite nombres de archivo, no rutas.", nameof(archivos));
        if (directorio == "-")
        {
            await using var writer = new TarWriter(Console.OpenStandardOutput(), TarEntryFormat.Pax, leaveOpen: true);
            foreach (var (nombre, texto) in archivos)
            {
                await using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(texto));
                await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, nombre) { DataStream = bytes }, ct);
            }
            return;
        }
        var destino = Path.GetFullPath(directorio);
        for (var padre = new DirectoryInfo(destino); padre is not null; padre = padre.Parent)
            if (padre.LinkTarget is not null)
                throw new ArgumentException("La ruta de preview no admite enlaces simbólicos.");
        if (Directory.Exists(destino) && Directory.EnumerateFileSystemEntries(destino).Any())
            throw new ArgumentException("El directorio de preview debe estar vacío; no se sobrescriben archivos.");
        Directory.CreateDirectory(destino);
        foreach (var (nombre, texto) in archivos)
            await File.WriteAllTextAsync(Path.Join(destino, nombre), texto, new UTF8Encoding(false), ct);
    }

    private static string Hash(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
}
