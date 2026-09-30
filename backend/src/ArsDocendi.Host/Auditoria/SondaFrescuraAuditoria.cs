namespace ArsDocendi.Host.Auditoria;

public sealed record ResultadoSondaAuditoria(bool Saludable,
    bool ContenidoIndependientementeVerificado, IReadOnlyList<string> Alertas);

/// <summary>Evalúa evidencia recibida por un operador externo; no lee ni certifica PostgreSQL.</summary>
public static class SondaFrescuraAuditoria
{
    public static ResultadoSondaAuditoria Evaluar(EstadoTestigosRemotos? testigos,
        DateTimeOffset? reporteLocalRecibidoEn, DateTimeOffset ahora, TimeSpan maximoAtraso, bool reporteLocalValido)
    {
        if (maximoAtraso <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximoAtraso));
        var alertas = new List<string>();
        bool Fresco(DateTimeOffset? fecha) => fecha is not null && fecha <= ahora && ahora - fecha <= maximoAtraso;
        if (testigos is null) alertas.Add("testigos-no-verificados");
        else
        {
            if (!Fresco(testigos.PrimarioEn)) alertas.Add("primario-vencido");
            if (!Fresco(testigos.SecundarioEn)) alertas.Add("secundario-vencido");
            if (testigos.RequiereReconciliacion) alertas.Add("restauracion-requiere-reconciliacion");
        }
        if (!reporteLocalValido) alertas.Add("verificacion-local-fallida");
        if (!Fresco(reporteLocalRecibidoEn)) alertas.Add("verificador-silencioso-o-vencido");
        return new(alertas.Count == 0, false, alertas);
    }
}
