using System.Data;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Arma el catálogo de capacidades preguntándole a la base «¿qué puedo leer yo?». Lo que es del
/// actor, el ámbito, no toca la caché.
/// </summary>
internal sealed class CatalogoDeCapacidades(
    AperturaDeLectura apertura,
    IPerfilDelActor perfiles,
    ISelectorDeEjemplos ejemplos,
    CacheDeCapacidades cache,
    IDisponibilidadDelModelo disponibilidad,
    IDisponibilidadDelModulo disponibilidadDelModulo,
    ICuotaDelActor cuota,
    IConsultasIdentity identidad,
    ILogger<CatalogoDeCapacidades> log) : ICatalogoDeCapacidades
{
    /// <summary>Cuántos ejemplos se ofrecen: entre cuatro y seis.</summary>
    private const int MinimoDeEjemplos = 4;
    private const int MaximoDeEjemplos = 6;

    private const string EsquemaDeComisiones = "comisiones.";

    /// <summary>Cuántos ejemplos de comisiones se suman como mucho, para quien puede preguntarlas.</summary>
    private const int MaximoDeEjemplosDeComisiones = 2;

    /// <summary>SQLSTATE de <c>insufficient_privilege</c>.</summary>
    private const string PrivilegioDenegado = "42501";

    /// <summary>Cuántas veces se leyó la base. Lo mira el test del caché.</summary>
    internal int Lecturas => cache.Lecturas;

    public async Task<CapacidadesDelActor> ObtenerAsync(Guid actor, CancellationToken ct)
    {
        var perfil = await perfiles.ObtenerAsync(actor, ct);
        var resuelto = await ResolverAsync(actor, perfil.VeDatosPersonales, ct);

        return new CapacidadesDelActor(
            // Same gate as the examples and the presentation: the GRANT is common to both read
            // roles, but the areas listed (and counted, and described by their comments) must not
            // announce comisiones to an actor whose policy returns nothing.
            perfil.VeComisiones
                ? resuelto.Cubre
                : [.. resuelto.Cubre.Where(area => !area.Nombre.StartsWith(EsquemaDeComisiones, StringComparison.Ordinal))],
            // Fuera de la caché, como la presentación: el GRANT de comisiones es de los dos roles de
            // lectura, pero ofrecer sus preguntas depende del permiso vivo del actor.
            perfil.VeComisiones ? ConComisiones(resuelto) : resuelto.Ejemplos,
            PoliticaDeAbstencion.LimitesDelAsistente,
            PoliticaDeAbstencion.TextoDeAlcance(perfil.EsGlobal),
            // Fuera de la caché a propósito: lo cacheado se indexa por rol de lectura —dos
            // variantes— y la presentación es del actor, como el alcance.
            PresentacionPorRol.Texto(perfil),
            await disponibilidadDelModulo.ConsultarAsync(ct),
            await CupoDeAsync(actor, ct));
    }

    /// <summary>
    /// El cupo del actor, con el MISMO bypass de mantenimiento que aplica <c>CapaConversacional</c>
    /// (design.md D8): sin él, un admin vería su propio cupo como "bloqueado por mantenimiento"
    /// aunque en la práctica pueda seguir consultando.
    /// </summary>
    private async Task<EstadoDelCupoDelActor> CupoDeAsync(Guid actor, CancellationToken ct)
    {
        var restante = await cuota.CupoRestanteAsync(actor, ct);
        var motivoModelo = await disponibilidad.ConsultarAsync(actor, ct);

        if (motivoModelo == MotivoSinModelo.Mantenimiento)
        {
            var permisos = await identidad.ObtenerCodigosDePermisosAsync(actor, ct);
            if (permisos.Contains(Permisos.AsistenteAdministrar))
            {
                motivoModelo = MotivoSinModelo.Ninguno;
            }
        }

        return motivoModelo switch
        {
            MotivoSinModelo.CuotaAgotada => new EstadoDelCupoDelActor(
                restante, true, EstadoDelCupoDelActor.MotivoPresupuestoPropio,
                await disponibilidad.CupoVuelveAAsync(actor, ct)),
            MotivoSinModelo.TopeOrganizacionalAgotado => new EstadoDelCupoDelActor(
                restante, true, EstadoDelCupoDelActor.MotivoTopeOrganizacional, null),
            MotivoSinModelo.Mantenimiento => new EstadoDelCupoDelActor(
                restante, true, EstadoDelCupoDelActor.MotivoMantenimiento, null),
            MotivoSinModelo.SinAcceso => new EstadoDelCupoDelActor(
                restante, true, EstadoDelCupoDelActor.MotivoSinAcceso, null),
            _ => new EstadoDelCupoDelActor(restante, false, null, null),
        };
    }

    /// <summary>Los ejemplos de comisiones ocupan el final de la lista sin pasar del máximo.</summary>
    private static IReadOnlyList<string> ConComisiones(Resuelto resuelto) =>
    [
        .. resuelto.Ejemplos.Take(MaximoDeEjemplos - resuelto.EjemplosDeComisiones.Count),
        .. resuelto.EjemplosDeComisiones,
    ];

    private Task<Resuelto> ResolverAsync(Guid actor, bool conDatosPersonales, CancellationToken ct) =>
        cache.ObtenerAsync(conDatosPersonales, () => LeerAsync(actor, conDatosPersonales, ct), ct);

    private async Task<Resuelto> LeerAsync(Guid actor, bool conDatosPersonales, CancellationToken ct)
    {
        await using var conexion = await apertura.AbrirAsync(conDatosPersonales, ct);

        var columnas = await LectorDeCatalogo.LeerColumnasAsync(conexion, ct);

        var cubre = columnas
            .GroupBy(columna => (columna.Esquema, columna.Tabla))
            .Select(grupo => new AreaCubierta(
                $"{grupo.Key.Esquema}.{grupo.Key.Tabla}",
                grupo.First().ComentarioDeTabla,
                grupo.Count()))
            .OrderBy(area => area.Nombre, StringComparer.Ordinal)
            .ToList();

        // Los ejemplos de comisiones se eligen aparte: nunca entran en la lista general, que es igual
        // para todos los actores de un rol de lectura.
        var esDeComisiones = ejemplos.Catalogo.ToLookup(
            e => DetectorDeDominios.Detectar(e.Sql).Dominios.HasFlag(DominioConsultado.Comisiones));
        var deComisiones = esDeComisiones[true].ToList();
        var generales = esDeComisiones[false].ToList();

        return new Resuelto(
            cubre,
            await ElegirEjemplosAsync(conexion, actor, generales, MaximoDeEjemplos, ct),
            await ElegirEjemplosAsync(conexion, actor, deComisiones, MaximoDeEjemplosDeComisiones, ct));
    }

    /// <summary>Elige hasta <paramref name="maximo"/> ejemplos que el actor <b>puede ejecutar</b>.</summary>
    private async Task<IReadOnlyList<string>> ElegirEjemplosAsync(
        NpgsqlConnection conexion,
        Guid actor,
        IReadOnlyList<EjemploSql> catalogo,
        int maximo,
        CancellationToken ct)
    {
        var elegidos = new List<string>();

        // Una vuelta por categoría primero, después el resto: da variedad sin
        // depender del orden en que estén escritos en el catálogo.
        var candidatos = catalogo
            .GroupBy(ejemplo => ejemplo.Categoria, StringComparer.Ordinal)
            .SelectMany(grupo => grupo.Select((ejemplo, indice) => (ejemplo, indice)))
            .OrderBy(par => par.indice)
            .ThenBy(par => par.ejemplo.Categoria, StringComparer.Ordinal)
            .Select(par => par.ejemplo);

        foreach (var ejemplo in candidatos)
        {
            if (elegidos.Count == maximo)
            {
                break;
            }

            if (await EjecutableAsync(conexion, actor, ejemplo.Sql, ct))
            {
                elegidos.Add(ejemplo.Pregunta);
            }
        }

        if (maximo == MaximoDeEjemplos && elegidos.Count < MinimoDeEjemplos)
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

    /// <summary>Si el actor puede ejecutar esa consulta, según el motor.</summary>
    internal static async Task<bool> EjecutableAsync(
        NpgsqlConnection conexion, Guid actor, string sql, CancellationToken ct)
    {
        // Transacción propia por candidato: un rechazo aborta la transacción en PostgreSQL, y con
        // una compartida el primer ejemplo denegado invalidaría todos los siguientes.
        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        try
        {
            // Las policies RLS invocan funciones que no resuelven sin el actor.
            await PreambuloDelActor.AplicarAsync(conexion, transaccion, actor, ct);

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
        IReadOnlyList<AreaCubierta> Cubre,
        IReadOnlyList<string> Ejemplos,
        IReadOnlyList<string> EjemplosDeComisiones);
}

/// <summary>Guarda lo que cuesta leer del catálogo de PostgreSQL, por rol.</summary>
internal sealed class CacheDeCapacidades
{
    private readonly ValorPerezosoPorRol<CatalogoDeCapacidades.Resuelto> _porRol = new();

    /// <summary>Cuántas veces hubo que leer la base. Lo mira el test del caché.</summary>
    internal int Lecturas => _porRol.Calculos;

    /// <summary>Devuelve lo cacheado, o lo calcula una sola vez.</summary>
    public Task<CatalogoDeCapacidades.Resuelto> ObtenerAsync(
        bool conDatosPersonales,
        Func<Task<CatalogoDeCapacidades.Resuelto>> calcular,
        CancellationToken ct) =>
        _porRol.ObtenerAsync(conDatosPersonales, _ => calcular(), ct);
}
