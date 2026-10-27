using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// La única puerta a las bases del sistema para leer: una fuente de datos por rol, con el techo de
/// comando decidido acá y no heredado.
/// </summary>
internal sealed class AperturaDeLectura : IDisposable
{
    private readonly NpgsqlDataSource _basica;
    private readonly NpgsqlDataSource _conDatosPersonales;

    public AperturaDeLectura(
        CadenaSoloLectura basica,
        CadenaSoloLecturaPii conDatosPersonales,
        IOptions<OpcionesAsistente> opciones)
    {
        ArgumentNullException.ThrowIfNull(basica);
        ArgumentNullException.ThrowIfNull(conDatosPersonales);
        ArgumentNullException.ThrowIfNull(opciones);

        var techo = opciones.Value.TimeoutDeComandoSegundos;

        _basica = Construir(basica.Valor, techo);
        _conDatosPersonales = Construir(conDatosPersonales.Valor, techo);
    }

    private static NpgsqlDataSource Construir(string cadena, int techoDeComandoSegundos) =>
        new NpgsqlDataSourceBuilder(
            new NpgsqlConnectionStringBuilder(cadena)
            {
                CommandTimeout = techoDeComandoSegundos,
            }.ConnectionString).Build();

    /// <summary>Abre una conexión con el rol que no ve datos personales.</summary>
    public ValueTask<NpgsqlConnection> AbrirAsync(CancellationToken ct) =>
        _basica.OpenConnectionAsync(ct);

    /// <summary>Abre una conexión con el rol que corresponda.</summary>
    public ValueTask<NpgsqlConnection> AbrirAsync(bool conDatosPersonales, CancellationToken ct) =>
        (conDatosPersonales ? _conDatosPersonales : _basica).OpenConnectionAsync(ct);

    public void Dispose()
    {
        _basica.Dispose();
        _conDatosPersonales.Dispose();
    }
}
