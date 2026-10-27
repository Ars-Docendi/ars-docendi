namespace Modules.Asistente.Infrastructure;

/// <summary>Identidad de la corrida que produjo un cassette.</summary>
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
