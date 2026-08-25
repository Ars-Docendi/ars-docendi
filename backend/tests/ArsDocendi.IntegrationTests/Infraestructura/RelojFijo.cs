namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Un <see cref="TimeProvider"/> con reloj movible, para los tests que dependen del
/// paso del tiempo.
/// </summary>
/// <remarks>
/// Existe para poder verificar la expiración del hilo sin esperar dos horas. Un
/// test que dependiera del reloj real sería lento o mentiroso, y probablemente las
/// dos cosas.
/// </remarks>
public sealed class RelojFijo(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _ahora = inicio;

    public override DateTimeOffset GetUtcNow() => _ahora;

    /// <summary>Adelanta el reloj.</summary>
    public void Avanzar(TimeSpan cuanto) => _ahora += cuanto;
}
