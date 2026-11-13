using Modules.Asistente.Infrastructure;

namespace Modules.Asistente.Api;

/// <summary>
/// Si el modelo del asistente está cargado (<c>GET /api/asistente/modelo/disponibilidad</c>,
/// asistente-modelo-en-reposo, D5). Ni el nombre del modelo, ni URLs, ni lo que Ollama tiene
/// cargado para otros.
/// </summary>
/// <param name="Estado"><c>no_aplica</c>, <c>listo</c>, <c>en_reposo</c>, <c>activando</c> o <c>desconocido</c>.</param>
/// <param name="UltimaActivacionFallida">Si el último intento de cargarlo no llegó a buen término.</param>
public sealed record DisponibilidadDelModeloDto(string Estado, bool UltimaActivacionFallida)
{
    internal static DisponibilidadDelModeloDto De(LecturaDelModelo lectura) =>
        new(lectura.Codigo, lectura.UltimaActivacionFallida);
}
