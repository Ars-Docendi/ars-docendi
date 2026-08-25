using System.Text;
using Microsoft.Extensions.Logging;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Captura todo lo que un componente registra, para poder afirmar sobre ello.
/// </summary>
/// <remarks>
/// Existe por un requisito concreto: las filas que devuelve el asistente
/// <b>nunca</b> se persisten. Hoy no hay ningún registro que las guarde, y ésta es
/// la forma de que siga siendo cierto — si alguien agrega un <c>LogInformation</c>
/// con las filas para depurar y se lo olvida puesto, el test lo agarra.
/// </remarks>
public sealed class RegistroDeCapturas : ILoggerProvider
{
    private readonly List<string> _lineas = [];
    private readonly Lock _candado = new();

    /// <summary>Un logger tipado que escribe acá.</summary>
    public ILogger<T> Logger<T>() => new LoggerCapturador<T>(this);

    /// <summary>Todo lo registrado, concatenado.</summary>
    public string Todo()
    {
        lock (_candado)
        {
            return string.Join("\n", _lineas);
        }
    }

    /// <summary>Las líneas registradas, en orden.</summary>
    public IReadOnlyList<string> Lineas
    {
        get
        {
            lock (_candado)
            {
                return [.. _lineas];
            }
        }
    }

    private void Agregar(string linea)
    {
        lock (_candado)
        {
            _lineas.Add(linea);
        }
    }

    public ILogger CreateLogger(string categoryName) => new LoggerCapturador<object>(this);

    public void Dispose()
    {
        // Nada que liberar: la captura vive en memoria.
    }

    private sealed class LoggerCapturador<T>(RegistroDeCapturas registro) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var linea = new StringBuilder(formatter(state, exception));

            // También el estado crudo: un valor que viajara solo como parámetro
            // estructurado no aparecería en el mensaje formateado, y es exactamente
            // por donde se filtraría sin que nadie lo note.
            linea.Append(" | ").Append(state);

            if (exception is not null)
            {
                linea.Append(" | ").Append(exception);
            }

            registro.Agregar(linea.ToString());
        }
    }
}
