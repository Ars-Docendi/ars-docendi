using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>
/// Frontera de salida: qué del resultado puede viajar al proveedor del modelo.
/// </summary>
/// <remarks>
/// Se interpone entre la ejecución de la SQL y la llamada de redacción. Las filas
/// que llegan al modelo van enmascaradas; las reales siguen viaje al llamador del
/// carril intactas.
///
/// <b>Consecuencia de diseño, no efecto colateral</b>: si el valor está tapado, el
/// modelo no puede pronunciarlo. Con columnas sensibles la narración deja de ser
/// el vehículo del dato — el modelo redacta el marco («encontré 4 docentes») y el
/// dato lo renderiza la interfaz.
///
/// <b>El enmascaramiento es asimétrico y no cierra la entrada.</b> La pregunta
/// cruda del usuario viaja al proveedor a través de la generación. Si alguien
/// tipea un documento en la pregunta, llega al modelo igual. Esto protege el
/// camino de vuelta, no el de ida. Se declara acá porque un lector que asuma
/// simetría va a confiar de más.
///
/// Es una función pura: se la puede ejercitar en memoria, sin base y sin proveedor.
/// </remarks>
public static class Enmascarador
{
    /// <summary>
    /// Devuelve el resultado que puede viajar al modelo.
    /// </summary>
    /// <remarks>
    /// Las columnas <see cref="ClasificacionDeSensibilidad.SensibleTexto"/>
    /// desaparecen enteras —nombre incluido— y las
    /// <see cref="ClasificacionDeSensibilidad.SensibleValor"/> conservan su columna
    /// con los valores reemplazados por marcadores.
    /// </remarks>
    public static ResultadoDeConsulta Enmascarar(ResultadoDeConsulta resultado)
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
            && !resultado.TieneColumnasTapadas)
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
                    marcadores))
                .ToArray())
            .ToArray();

        return new ResultadoDeConsulta(
            [.. sobrevivientes.Select(indice => resultado.Columnas[indice])],
            filas,
            resultado.Truncado,
            [.. sobrevivientes.Select(indice => Sensibilidad(sensibilidad, indice))]);
    }

    private static object? Enmascarar(
        object? valor, SensibilidadDeColumna sensibilidad, Marcadores marcadores)
    {
        if (sensibilidad.Clasificacion != ClasificacionDeSensibilidad.SensibleValor)
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

    /// <summary>
    /// Asigna marcadores estables dentro de una respuesta.
    /// </summary>
    /// <remarks>
    /// <b>El marcador es un contador, no una función del valor.</b> Derivarlo del
    /// valor —un hash corto— saldría estable sin llevar estado, y es exactamente lo
    /// que no hay que hacer: el espacio de documentos y de teléfonos es chico y
    /// conocido, así que un hash se invierte por fuerza bruta en segundos. El
    /// marcador viajaría al proveedor y sería el dato, con un paso más.
    ///
    /// Con un contador por orden de primera aparición no hay ninguna relación entre
    /// el marcador y el valor, y el diccionario no sobrevive al turno.
    ///
    /// La etiqueta la da el manifiesto, así que el modelo sabe <b>de qué</b> es el
    /// marcador sin saber cuál. Eso es lo que le permite redactar una respuesta con
    /// sujeto en vez de una frase hueca.
    /// </remarks>
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
