using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;

namespace ArsDocendi.Storage.Infrastructure;

public static class ReglasArchivos
{
    public static string SanitizarNombre(string nombre)
    {
        var baseNombre = Path.GetFileName(nombre.Trim());
        var limpio = new string(baseNombre.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return limpio.Length is > 0 and <= 180 ? limpio : throw Error("nombre", "El nombre del archivo no es válido.");
    }

    public static void ValidarInicio(IniciarCargaArchivoDto datos, AlmacenamientoOptions opciones)
    {
        if (!PropositosArchivo.Todos.Contains(datos.Proposito)) throw Error("proposito", "El propósito de archivo no está admitido.");
        var nombre = SanitizarNombre(datos.NombreOriginal);
        if (datos.TamanoBytes <= 0) throw Error("tamanoBytes", "El tamaño debe ser positivo.");
        var esPdf = PropositosArchivo.EsPdf(datos.Proposito);
        var limite = esPdf ? opciones.TamanoMaximoPdf : opciones.TamanoMaximoImagen;
        if (datos.TamanoBytes > limite) throw Error("tamanoBytes", "El archivo supera el límite permitido.");
        var mime = datos.MimeDeclarado.Trim().ToLowerInvariant();
        var mimeValido = esPdf ? mime == "application/pdf" : mime is "image/jpeg" or "image/png";
        if (!mimeValido) throw Error("mimeDeclarado", "El MIME no es compatible con el propósito.");
        if (esPdf && !nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw Error("nombre", "El archivo debe tener extensión PDF.");
    }

    public static string CalcularSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    public static string DetectarMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        return "application/octet-stream";
    }

    public static void ValidarContenido(string proposito, string mimeDetectado, long tamano, AlmacenamientoOptions opciones)
    {
        var limite = PropositosArchivo.EsPdf(proposito) ? opciones.TamanoMaximoPdf : opciones.TamanoMaximoImagen;
        if (tamano <= 0 || tamano > limite) throw Error("tamanoBytes", "El objeto no respeta el tamaño permitido.");
        var valido = PropositosArchivo.EsPdf(proposito)
            ? mimeDetectado == "application/pdf"
            : mimeDetectado is "image/jpeg" or "image/png";
        if (!valido) throw Error("mime", "El contenido real no coincide con el propósito.");
    }

    private static ExcepcionAplicacion Error(string campo, string mensaje) => new(
        TipoErrorAplicacion.Validacion, "archivo-validation", mensaje,
        new Dictionary<string, string[]> { [campo] = [mensaje] });
}
