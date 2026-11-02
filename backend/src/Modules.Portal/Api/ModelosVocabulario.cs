namespace Modules.Portal.Api;

/// <param name="Canonico">Término al que se fusionó; <c>null</c> si no está fusionado.</param>
/// <param name="Docentes">Perfiles que lo usan como habilidad o interés.</param>
public sealed record TerminoVocabularioDto(
    Guid Id,
    string Termino,
    bool Sugerido,
    string? Canonico,
    int Docentes);

public sealed record RenombrarTerminoDto(string Termino);

public sealed record FusionarTerminoDto(Guid DestinoId);
