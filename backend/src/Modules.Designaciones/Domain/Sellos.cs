namespace Modules.Designaciones.Domain;

/// <summary>
/// Eslabón de la cadena de integridad de un período [BR-designaciones-019]. Sólo inserción:
/// la base rechaza modificarlo y exige que encadene con la cabeza vigente.
/// </summary>
public sealed class PedidoSello
{
    public Guid PeriodoId { get; set; }
    public long Secuencia { get; set; }
    public Guid HistorialId { get; set; }
    public Guid PedidoId { get; set; }
    public required byte[] HashAnterior { get; set; }
    /// <summary>SHA-256 del estado canónico del pedido tras el evento.</summary>
    public required byte[] HashEstado { get; set; }
    public required byte[] Hash { get; set; }
    /// <summary>HMAC-SHA256 de <see cref="Hash"/> con la clave del host.</summary>
    public required byte[] Firma { get; set; }
    public short VersionClave { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

/// <summary>Cabeza de la cadena publicada fuera de la base (lote exportado) o sellado retroactivo.</summary>
public sealed class PeriodoAncla
{
    public Guid Id { get; set; }
    public Guid PeriodoId { get; set; }
    public long Secuencia { get; set; }
    public required byte[] Hash { get; set; }
    public required string Motivo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public static class MotivosAncla
{
    public const string ExportacionLote = "exportacion_lote";
    public const string Retroactivo = "retroactivo";
}
