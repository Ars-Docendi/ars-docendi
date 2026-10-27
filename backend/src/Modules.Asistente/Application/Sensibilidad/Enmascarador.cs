using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>What gets hidden from the result before redaction.</summary>
internal enum ModoDeEnmascarado
{
    /// <summary>Markers for <c>sensible-valor</c>, and <c>sensible-texto</c> is suppressed.</summary>
    Todo,

    /// <summary>
    /// Only <c>sensible-texto</c> is suppressed; <c>sensible-valor</c> values pass through.
    /// </summary>
    SoloTextoLibre,
}

/// <summary>Frontera de salida: qué del resultado puede viajar al proveedor del modelo.</summary>
internal static class Enmascarador
{
    /// <summary>Devuelve el resultado que puede viajar al modelo.</summary>
    public static ResultadoDeConsulta Enmascarar(
        ResultadoDeConsulta resultado, ModoDeEnmascarado modo = ModoDeEnmascarado.Todo)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        var sensibilidad = resultado.Sensibilidad;
        var sobrevivientes = Enumerable
            .Range(0, resultado.Columnas.Count)
            .Where(indice =>
                Sensibilidad(sensibilidad, indice).Clasificacion
                    != ClasificacionDeSensibilidad.SensibleTexto)
            .ToArray();

        if (sobrevivientes.Length == resultado.Columnas.Count
            && (modo == ModoDeEnmascarado.SoloTextoLibre || !resultado.TieneColumnasTapadas))
        {
            // Nada que enmascarar: se devuelve la misma instancia para no pagar
            // una copia en el caso corriente, que es la mayoría de los turnos.
            return resultado;
        }

        var marcadores = new Marcadores();

        var filas = resultado.Filas
            .Select(fila => (IReadOnlyList<object?>)sobrevivientes
                .Select(indice => Enmascarar(
                    indice < fila.Count ? fila[indice] : null,
                    Sensibilidad(sensibilidad, indice),
                    marcadores,
                    modo))
                .ToArray())
            .ToArray();

        return new ResultadoDeConsulta(
            [.. sobrevivientes.Select(indice => resultado.Columnas[indice])],
            filas,
            resultado.Truncado,
            [.. sobrevivientes.Select(indice => Sensibilidad(sensibilidad, indice))]);
    }

    private static object? Enmascarar(
        object? valor,
        SensibilidadDeColumna sensibilidad,
        Marcadores marcadores,
        ModoDeEnmascarado modo)
    {
        if (modo == ModoDeEnmascarado.SoloTextoLibre
            || sensibilidad.Clasificacion != ClasificacionDeSensibilidad.SensibleValor)
        {
            return valor;
        }

        // Un nulo se deja nulo: taparlo inventaría un dato donde no lo hay, y la
        // redacción lo muestra como «sin dato», que es la verdad.
        return valor is null ? null : marcadores.Para(sensibilidad.Etiqueta, valor);
    }

    private static SensibilidadDeColumna Sensibilidad(
        IReadOnlyList<SensibilidadDeColumna>? sensibilidad, int indice) =>
        sensibilidad is not null && indice < sensibilidad.Count
            ? sensibilidad[indice]
            : SensibilidadDeColumna.Publica;

    /// <summary>Asigna marcadores estables dentro de una respuesta.</summary>
    private sealed class Marcadores
    {
        private const string EtiquetaPorOmision = "dato reservado";

        private readonly Dictionary<(string Etiqueta, string Valor), string> _asignados = new();
        private readonly Dictionary<string, int> _proximo = new(StringComparer.Ordinal);

        public string Para(string? etiqueta, object valor)
        {
            var nombre = string.IsNullOrWhiteSpace(etiqueta) ? EtiquetaPorOmision : etiqueta;
            var clave = (nombre, Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty);

            if (_asignados.TryGetValue(clave, out var asignado))
            {
                return asignado;
            }

            var numero = _proximo.GetValueOrDefault(nombre) + 1;
            _proximo[nombre] = numero;

            var marcador = string.Create(CultureInfo.InvariantCulture, $"«{nombre} {numero}»");
            _asignados[clave] = marcador;
            return marcador;
        }
    }
}
