namespace Modules.Asistente.Application;

/// <summary>
/// El motivo por el que la generación declaró una pregunta no contestable
/// (design.md D2 de asistente-rechazos-dinamicos).
/// </summary>
/// <remarks>
/// Conjunto cerrado de cuatro valores. Cualquier otro texto que el modelo
/// devuelva —o la ausencia del campo— resuelve <see cref="NoCubierto"/>, nunca
/// una excepción: un motivo mal escrito no puede tirar abajo la interpretación
/// de toda la generación.
///
/// <b>Público, y no interno como el resto de esta carpeta</b>: es el tipo de un
/// parámetro de tres records públicos —<see cref="GeneracionDeSql"/>,
/// <see cref="ResultadoDelTurno"/>, <see cref="TurnoParaRegistrar"/>—, y un
/// enum menos accesible que el record que lo expone no compila. Mismo criterio
/// que ya usan <see cref="EstadoDelTurno"/> y <see cref="CarrilDelTurno"/>.
/// </remarks>
public enum MotivoDeRechazo
{
    /// <summary>La pregunta no tiene que ver con el dominio del sistema.</summary>
    FueraDeTema,

    /// <summary>La pregunta corresponde a un sistema externo (Guaraní, planillas).</summary>
    OtroSistema,

    /// <summary>La pregunta es demasiado amplia o no tiene un dato puntual.</summary>
    MuyGeneral,

    /// <summary>Cualquier otro caso: el esquema no cubre lo preguntado.</summary>
    NoCubierto,
}

/// <summary>
/// Interpreta el valor de cable de <see cref="MotivoDeRechazo"/> (design.md D2).
/// </summary>
internal static class MotivosDeRechazo
{
    /// <summary>
    /// El mapa único entre el valor de cable —lo que escribe el modelo— y el
    /// valor del enum. Es el único lugar que conoce esos cuatro literales.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, MotivoDeRechazo> PorValorDeCable =
        new Dictionary<string, MotivoDeRechazo>(StringComparer.Ordinal)
        {
            ["fuera_de_tema"] = MotivoDeRechazo.FueraDeTema,
            ["otro_sistema"] = MotivoDeRechazo.OtroSistema,
            ["muy_general"] = MotivoDeRechazo.MuyGeneral,
            ["no_cubierto"] = MotivoDeRechazo.NoCubierto,
        };

    /// <summary>
    /// Interpreta el valor de cable. Cualquier valor fuera del conjunto
    /// cerrado —incluida una comparación que difiera por mayúsculas, espacios
    /// o acentos— resuelve <see cref="MotivoDeRechazo.NoCubierto"/>: el
    /// conjunto es cerrado y la comparación es literal, a propósito.
    /// </summary>
    public static MotivoDeRechazo Interpretar(string? valorDeCable) =>
        valorDeCable is not null && PorValorDeCable.TryGetValue(valorDeCable, out var motivo)
            ? motivo
            : MotivoDeRechazo.NoCubierto;

    /// <summary>El valor de cable de un motivo, para el registro operativo.</summary>
    public static string ValorDeCable(MotivoDeRechazo motivo) => motivo switch
    {
        MotivoDeRechazo.FueraDeTema => "fuera_de_tema",
        MotivoDeRechazo.OtroSistema => "otro_sistema",
        MotivoDeRechazo.MuyGeneral => "muy_general",
        MotivoDeRechazo.NoCubierto => "no_cubierto",
        _ => "no_cubierto",
    };
}
