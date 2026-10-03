using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// La única puerta a las bases del sistema para leer: una fuente de datos por
/// rol, con el techo de comando decidido acá y no heredado.
/// </summary>
/// <remarks>
/// Había <b>doce</b> puntos donde el módulo construía una conexión y <b>tres</b>
/// ternarios que elegían el rol. Con doce constructores, «con qué techo de comando
/// lee el asistente» no tenía respuesta: dependía de cuál de los doce corriera.
/// Sólo el ejecutor del carril ponía el suyo; los otros heredaban el default de
/// Npgsql, 30 s — el doble del que el módulo eligió.
///
/// Es un <see cref="NpgsqlDataSource"/> y no una cadena porque el pool vive en la
/// fuente: doce conexiones construidas a mano sobre la misma cadena comparten
/// pool igual, pero nadie puede decir dónde está configurado.
///
/// <b>Esto NO reemplaza la cota del servidor.</b> Los roles llevan
/// <c>statement_timeout</c> fijado por <c>provision-db.sh</c>, y esa es la que
/// vale: corta del lado del motor y libera el backend. Ésta es la del cliente, y
/// existe para que la aplicación no se quede esperando una respuesta que el
/// servidor ya abandonó.
///
/// Los tres puntos que quedan afuera —<c>MigradorAsistente</c>,
/// <c>RegistroDelTurno</c> y <c>PurgaDeRegistros</c>— usan la cadena del DUEÑO
/// para escribir el schema propio del asistente. No son lectura y no pasan por
/// acá; el guard de arquitectura los nombra uno por uno.
/// </remarks>
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
    /// <remarks>
    /// El booleano es lo que antes era un ternario repetido en tres archivos. Que
    /// esté acá adentro significa que elegir el rol equivocado deja de ser algo
    /// que cada consumidor puede hacer mal por su cuenta.
    /// </remarks>
    public ValueTask<NpgsqlConnection> AbrirAsync(bool conDatosPersonales, CancellationToken ct) =>
        (conDatosPersonales ? _conDatosPersonales : _basica).OpenConnectionAsync(ct);

    public void Dispose()
    {
        _basica.Dispose();
        _conDatosPersonales.Dispose();
    }
}
