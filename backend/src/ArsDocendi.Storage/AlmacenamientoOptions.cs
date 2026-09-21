namespace ArsDocendi.Storage;

public sealed class AlmacenamientoOptions
{
    public const string Seccion = "Almacenamiento";
    public string Endpoint { get; set; } = "localhost:8333";
    public bool UseSsl { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "arsdocendi-dev";
    public string Ambiente { get; set; } = "development";
    public int ExpiracionCargaSegundos { get; set; } = 600;
    public int ExpiracionDescargaSegundos { get; set; } = 300;
    public long TamanoMaximoPdf { get; set; } = 10 * 1024 * 1024;
    public long TamanoMaximoImagen { get; set; } = 5 * 1024 * 1024;
    public bool RechazarSiAntivirusNoDisponible { get; set; }
    public string ClamAvHost { get; set; } = "clamav";
    public int ClamAvPort { get; set; } = 3310;
}
