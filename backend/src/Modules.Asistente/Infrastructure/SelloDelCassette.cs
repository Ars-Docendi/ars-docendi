namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Identidad de la corrida que produjo un cassette.
/// </summary>
/// <param name="Modelo">Modelo que respondió.</param>
/// <param name="Fecha">Día de la grabación, en formato ISO.</param>
/// <param name="HashDelPrefijo">Huella del prefijo estable con que se preguntó.</param>
/// <param name="HashDelFixture">Huella del fixture sintético contra el que se grabó.</param>
/// <remarks>
/// Los tres primeros campos son los mismos que RNF-03 le exige a los reportes de
/// evaluación, y por el mismo motivo: una respuesta grabada contra otro esquema, o
/// con otro modelo, describe otro sistema.
///
/// <b>El cuarto no es decoración.</b> Es lo que hace mecánica la garantía de que
/// ningún cassette tiene filas reales. Sin él, «se graba contra el fixture
/// sintético» es una convención que se cumple hasta que alguien grabe contra su
/// base de desarrollo con datos importados; con él, un cassette que no declare el
/// hash del fixture vigente no se sirve y el guard que barre el repositorio tiene
/// contra qué comparar. Importa porque los cassettes de la llamada de redacción
/// llevan filas adentro: la redacción recibe el resultado ya enmascarado, y
/// enmascarado no es sintético.
///
/// Costo asumido: cambiar el fixture invalida los cassettes. Es correcto y es
/// visible — el evaluador ya recalcula ese hash en cada corrida.
/// </remarks>
internal sealed record SelloDelCassette(
    string Modelo,
    string Fecha,
    string HashDelPrefijo,
    string HashDelFixture)
{
    /// <summary>Nombre del campo del modelo en el sobre.</summary>
    public const string CampoModelo = "modelo";

    /// <summary>Nombre del campo de la fecha en el sobre.</summary>
    public const string CampoFecha = "fecha";

    /// <summary>Nombre del campo de la huella del prefijo en el sobre.</summary>
    public const string CampoHashDelPrefijo = "hash_del_prefijo";

    /// <summary>Nombre del campo de la huella del fixture en el sobre.</summary>
    public const string CampoHashDelFixture = "hash_del_fixture";

    /// <summary>Nombre del campo que lleva el cuerpo crudo de la respuesta.</summary>
    public const string CampoCuerpo = "cuerpo";

    /// <summary>Los campos del sello que están vacíos, con su nombre en el sobre.</summary>
    /// <remarks>
    /// Devuelve la lista y no un booleano porque el mensaje de error tiene que
    /// nombrar qué falta: el modo de fallar esperado es «alguien grabó sin la huella
    /// del fixture a mano», y eso se resuelve leyendo el mensaje.
    /// </remarks>
    public IReadOnlyList<string> CamposVacios()
    {
        var faltantes = new List<string>();

        Anotar(CampoModelo, Modelo);
        Anotar(CampoFecha, Fecha);
        Anotar(CampoHashDelPrefijo, HashDelPrefijo);
        Anotar(CampoHashDelFixture, HashDelFixture);

        return faltantes;

        void Anotar(string campo, string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                faltantes.Add(campo);
            }
        }
    }
}
