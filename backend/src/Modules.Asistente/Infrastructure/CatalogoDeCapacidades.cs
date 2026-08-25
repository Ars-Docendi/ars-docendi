using System.Data;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Arma el catálogo de capacidades preguntándole a la base «¿qué puedo leer yo?».
/// </summary>
/// <remarks>
/// Reusa <see cref="LectorDeCatalogo"/>, que evalúa <c>has_column_privilege</c>
/// contra <c>current_user</c>: los dos roles del asistente obtienen catálogos
/// distintos sin que este código sepa nada de ellos.
///
/// <b>Es scoped y la caché es singleton, y la separación no es estética.</b> Esta
/// clase depende de <see cref="IPerfilDelActor"/>, que resuelve al actor del turno y
/// por lo tanto es scoped; un catálogo singleton que lo consumiera capturaría el
/// perfil del primer actor que consultara y se lo devolvería a todos los demás. El
/// contenedor rechaza esa registración al arrancar, y hace bien.
///
/// Lo que sí tiene que sobrevivir al request es el resultado de leer el catálogo de
/// PostgreSQL, y eso vive en <see cref="CacheDeCapacidades"/>, indexado por
/// <b>rol</b>: los privilegios no cambian en runtime y hay exactamente dos variantes
/// —con y sin datos personales—. Lo que es del actor, el ámbito, no toca la caché.
/// </remarks>
internal sealed class CatalogoDeCapacidades(
    CadenaSoloLectura cadenaBasica,
    CadenaSoloLecturaPii cadenaConDatosPersonales,
    IPerfilDelActor perfiles,
    ISelectorDeEjemplos ejemplos,
    CacheDeCapacidades cache,
    ILogger<CatalogoDeCapacidades> log) : ICatalogoDeCapacidades
{
    /// <summary>Cuántos ejemplos se ofrecen: entre cuatro y seis.</summary>
    private const int MinimoDeEjemplos = 4;
    private const int MaximoDeEjemplos = 6;

    /// <summary>Ajuste de sesión con el actor, igual que en el ejecutor.</summary>
    private const string AjusteDelActor = "app.asistente_user_id";

    /// <summary>SQLSTATE de <c>insufficient_privilege</c>.</summary>
    private const string PrivilegioDenegado = "42501";

    /// <summary>Cuántas veces se leyó la base. Lo mira el test del caché.</summary>
    internal int Lecturas => cache.Lecturas;

    public async Task<CapacidadesDelActor> ObtenerAsync(Guid actor, CancellationToken ct)
    {
        var perfil = await perfiles.ObtenerAsync(actor, ct);
        var resuelto = await ResolverAsync(actor, perfil.VeDatosPersonales, ct);

        return new CapacidadesDelActor(
            resuelto.Cubre,
            resuelto.Ejemplos,
            PoliticaDeAbstencion.LimitesDelAsistente,
            PoliticaDeAbstencion.TextoDeAlcance(perfil.EsGlobal));
    }

    private Task<Resuelto> ResolverAsync(Guid actor, bool conDatosPersonales, CancellationToken ct) =>
        cache.ObtenerAsync(conDatosPersonales, () => LeerAsync(actor, conDatosPersonales, ct), ct);

    private async Task<Resuelto> LeerAsync(Guid actor, bool conDatosPersonales, CancellationToken ct)
    {
        var cadena = conDatosPersonales
            ? cadenaConDatosPersonales.Valor
            : cadenaBasica.Valor;

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);

        var columnas = await LectorDeCatalogo.LeerColumnasAsync(conexion, ct);

        var cubre = columnas
            .GroupBy(columna => (columna.Esquema, columna.Tabla))
            .Select(grupo => new AreaCubierta(
                $"{grupo.Key.Esquema}.{grupo.Key.Tabla}",
                grupo.First().ComentarioDeTabla,
                grupo.Count()))
            .OrderBy(area => area.Nombre, StringComparer.Ordinal)
            .ToList();

        return new Resuelto(cubre, await ElegirEjemplosAsync(conexion, actor, ct));
    }

    /// <summary>
    /// Elige de cuatro a seis ejemplos que el actor <b>puede ejecutar</b>.
    /// </summary>
    /// <remarks>
    /// Cada candidato se pasa por <c>EXPLAIN</c> con la conexión del actor.
    /// <c>EXPLAIN</c> sin <c>ANALYZE</c> arranca el ejecutor —y por lo tanto chequea
    /// privilegios— pero no lee ninguna fila: si el ejemplo toca una columna que el
    /// rol no puede leer, el motor lo rechaza con <c>42501</c>.
    ///
    /// Es más caro que consultar una lista de ejemplos marcados «seguros», y es lo
    /// correcto: una lista se desincroniza del GRANT en silencio, y el modo de falla
    /// de esa desincronización es ofrecerle al usuario una pregunta que no puede
    /// hacer.
    ///
    /// Se diversifica por categoría antes de recortar: seis ejemplos de conteo
    /// simple no le muestran a nadie de qué es capaz el asistente.
    /// </remarks>
    private async Task<IReadOnlyList<string>> ElegirEjemplosAsync(
        NpgsqlConnection conexion, Guid actor, CancellationToken ct)
    {
        var elegidos = new List<string>();

        // Una vuelta por categoría primero, después el resto: da variedad sin
        // depender del orden en que estén escritos en el catálogo.
        var candidatos = ejemplos.Catalogo
            .GroupBy(ejemplo => ejemplo.Categoria, StringComparer.Ordinal)
            .SelectMany(grupo => grupo.Select((ejemplo, indice) => (ejemplo, indice)))
            .OrderBy(par => par.indice)
            .ThenBy(par => par.ejemplo.Categoria, StringComparer.Ordinal)
            .Select(par => par.ejemplo);

        foreach (var ejemplo in candidatos)
        {
            if (elegidos.Count == MaximoDeEjemplos)
            {
                break;
            }

            if (await EjecutableAsync(conexion, actor, ejemplo.Sql, ct))
            {
                elegidos.Add(ejemplo.Pregunta);
            }
        }

        if (elegidos.Count < MinimoDeEjemplos)
        {
            // No se rellena con ejemplos que el actor no puede ejecutar: el
            // requisito de «cuatro a seis» está por debajo del de «ejecutables».
            log.LogWarning(
                "El catálogo de capacidades quedó con {Cuantos} ejemplos ejecutables, "
                + "menos que el mínimo de {Minimo}.",
                elegidos.Count,
                MinimoDeEjemplos);
        }

        return elegidos;
    }

    /// <summary>
    /// Si el actor puede ejecutar esa consulta, según el motor.
    /// </summary>
    /// <remarks>
    /// Es <c>internal</c> para que el test pueda ejercitarla con una consulta
    /// sintética que toca una columna personal. El catálogo de ejemplos real no
    /// tiene ninguna, así que sin eso el filtro sería un no-op no verificado — y el
    /// día que alguien agregue un ejemplo con datos personales nadie se enteraría de
    /// si funciona.
    /// </remarks>
    internal static async Task<bool> EjecutableAsync(
        NpgsqlConnection conexion, Guid actor, string sql, CancellationToken ct)
    {
        // Transacción propia por candidato: un rechazo aborta la transacción en
        // PostgreSQL, y con una compartida el primer ejemplo denegado invalidaría
        // todos los siguientes.
        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        try
        {
            await using (var soloLectura = new NpgsqlCommand(
                "SET TRANSACTION READ ONLY", conexion, transaccion))
            {
                await soloLectura.ExecuteNonQueryAsync(ct);
            }

            // El actor va fijado igual que en el ejecutor: las policies RLS invocan
            // funciones que no resuelven sin él.
            await using (var fijarActor = new NpgsqlCommand(
                $"SELECT set_config('{AjusteDelActor}', @actor, true)", conexion, transaccion))
            {
                fijarActor.Parameters.AddWithValue("actor", actor.ToString());
                await fijarActor.ExecuteNonQueryAsync(ct);
            }

            await using var explicar = new NpgsqlCommand($"EXPLAIN {sql}", conexion, transaccion);
            await explicar.ExecuteNonQueryAsync(ct);

            return true;
        }
        catch (PostgresException excepcion) when (excepcion.SqlState == PrivilegioDenegado)
        {
            return false;
        }
        finally
        {
            await transaccion.RollbackAsync(ct);
        }
    }

    internal sealed record Resuelto(
        IReadOnlyList<AreaCubierta> Cubre, IReadOnlyList<string> Ejemplos);
}

/// <summary>
/// Guarda lo que cuesta leer del catálogo de PostgreSQL, por rol.
/// </summary>
/// <remarks>
/// Existe separada de <see cref="CatalogoDeCapacidades"/> porque los dos tienen
/// alcances distintos y ninguno de los dos puede tener el del otro: el catálogo
/// depende del perfil del actor y es del request; esto es del proceso.
///
/// Indexa por <b>rol</b> y no por actor: son dos variantes —con y sin datos
/// personales—, y los privilegios no cambian en runtime.
/// </remarks>
internal sealed class CacheDeCapacidades
{
    private readonly Dictionary<bool, CatalogoDeCapacidades.Resuelto> _porRol = [];
    private readonly SemaphoreSlim _candado = new(1, 1);

    /// <summary>Cuántas veces hubo que leer la base. Lo mira el test del caché.</summary>
    internal int Lecturas { get; private set; }

    /// <summary>Devuelve lo cacheado, o lo calcula una sola vez.</summary>
    public async Task<CatalogoDeCapacidades.Resuelto> ObtenerAsync(
        bool conDatosPersonales,
        Func<Task<CatalogoDeCapacidades.Resuelto>> calcular,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(calcular);

        if (_porRol.TryGetValue(conDatosPersonales, out var cacheado))
        {
            return cacheado;
        }

        await _candado.WaitAsync(ct);
        try
        {
            if (_porRol.TryGetValue(conDatosPersonales, out cacheado))
            {
                return cacheado;
            }

            Lecturas++;
            var resuelto = await calcular();
            _porRol[conDatosPersonales] = resuelto;

            return resuelto;
        }
        finally
        {
            _candado.Release();
        }
    }
}
