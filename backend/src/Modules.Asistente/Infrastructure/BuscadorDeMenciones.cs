using System.Data;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Busca materias y docentes dentro del alcance del actor (design.md D10/D11 de
/// asistente-rediseno-v3).
/// </summary>
/// <remarks>
/// <b>Corre con el rol básico y el actor fijado, nunca con la conexión dueña ni
/// con un filtro de C# como única guardia.</b> El alcance de las materias lo
/// resuelve <c>identity.asistente_materias_visibles()</c>; el de los docentes lo
/// resuelve la policy de RLS de <c>designaciones.designaciones</c>, que ya
/// conjunta <c>designaciones.ver</c> con el ámbito del actor
/// (<c>009_designaciones_rls_asistente.sql</c>). Un actor sin ese permiso ve la
/// tabla vacía por la policy, no porque este código haya evaluado el permiso: si
/// alguien borrara el chequeo de acá adentro, el resultado no cambia.
///
/// Ninguna columna que toca está clasificada <c>sensible-*</c> en el manifiesto
/// de sensibilidad: <c>identity.materias.name/code</c>,
/// <c>identity.carreras.name</c>, <c>identity.personas.nombre/apellido</c> y
/// <c>designaciones.cargos.nombre</c> son todas <c>publica</c>. El identificador
/// que este puerto SÍ devuelve —a diferencia del carril SQL— es
/// <c>identificador</c> y no <c>sensible-*</c>: D10 registra por qué eso no
/// contradice el manifiesto (llega al navegador del propio actor, nunca al
/// modelo).
/// </remarks>
internal sealed class BuscadorDeMenciones(AperturaDeLectura apertura) : IBuscadorDeMenciones
{
    /// <summary>
    /// Tope de resultados por búsqueda (design.md D10: «a lo sumo 6»). Se pide
    /// uno de más —ver <see cref="EjecutorDeConsulta"/> para el mismo truco— para
    /// distinguir «hay exactamente el tope» de «hay más y se recortó» sin exponer
    /// un conteo.
    /// </summary>
    private const int Tope = 6;

    private const string SqlMaterias = """
        SELECT m.id, m.name, m.code, c.name AS carrera
          FROM identity.materias m
          JOIN identity.carreras c ON c.id = m.carrera_id
         WHERE m.is_active
           AND m.id IN (SELECT identity.asistente_materias_visibles())
           AND (
                 EXISTS (
                     SELECT 1
                       FROM unnest(regexp_split_to_array(
                                public.unaccent(lower(m.name)), '\s+')) AS palabra
                      WHERE palabra LIKE public.unaccent(lower(@termino)) || '%' ESCAPE '\'
                 )
              OR lower(m.code) LIKE lower(@termino) || '%' ESCAPE '\'
               )
         ORDER BY m.name, c.name
         LIMIT @tope
        """;

    private const string SqlMateriaPorId = """
        SELECT m.id, m.name, m.code, c.name AS carrera
          FROM identity.materias m
          JOIN identity.carreras c ON c.id = m.carrera_id
         WHERE m.is_active
           AND m.id = @id
           AND m.id IN (SELECT identity.asistente_materias_visibles())
        """;

    /// <summary>
    /// La designación vigente más reciente de cada persona, para el cargo que se
    /// muestra en el resultado. Lee de <c>designaciones.designaciones</c>, así que
    /// la policy de RLS ya filtró las filas ANTES de que <c>ROW_NUMBER()</c> las
    /// numere: nunca hay que volver a chequear el alcance sobre esta CTE.
    /// </summary>
    private const string ConDesignacionReciente = """
        WITH designacion_reciente AS (
            SELECT persona_id, cargo_id,
                   ROW_NUMBER() OVER (
                       PARTITION BY persona_id
                       ORDER BY vigente_desde DESC, created_at DESC
                   ) AS orden
              FROM designaciones.designaciones
        )
        """;

    private const string SqlDocentes = ConDesignacionReciente + """
        SELECT p.id, p.nombre, p.apellido, cg.nombre AS cargo
          FROM identity.personas p
          JOIN designacion_reciente dr ON dr.persona_id = p.id AND dr.orden = 1
          JOIN designaciones.cargos cg ON cg.id = dr.cargo_id
         WHERE EXISTS (
             SELECT 1
               FROM unnest(regexp_split_to_array(
                        public.unaccent(lower(p.apellido || ' ' || p.nombre)), '\s+')) AS palabra
              WHERE palabra LIKE public.unaccent(lower(@termino)) || '%' ESCAPE '\'
         )
         ORDER BY p.apellido, p.nombre
         LIMIT @tope
        """;

    private const string SqlDocentePorId = ConDesignacionReciente + """
        SELECT p.id, p.nombre, p.apellido, cg.nombre AS cargo
          FROM identity.personas p
          JOIN designacion_reciente dr ON dr.persona_id = p.id AND dr.orden = 1
          JOIN designaciones.cargos cg ON cg.id = dr.cargo_id
         WHERE p.id = @id
        """;

    public async Task<BusquedaDeMenciones> BuscarAsync(
        Guid actor, TipoDeMencion tipo, string termino, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termino);

        try
        {
            await using var conexion = await apertura.AbrirAsync(ct);
            await using var transaccion = await conexion.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

            await PreambuloDelActor.AplicarAsync(conexion, transaccion, actor, ct);

            var sql = tipo == TipoDeMencion.Materia ? SqlMaterias : SqlDocentes;
            await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("termino", EscaparParaLike(termino));
            comando.Parameters.AddWithValue("tope", Tope + 1);

            var resultados = new List<ResultadoDeMencion>(Tope + 1);
            await using var lector = await comando.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
            {
                resultados.Add(Leer(tipo, lector));
            }

            var hayMas = resultados.Count > Tope;
            return new BusquedaDeMenciones(
                hayMas ? resultados.GetRange(0, Tope) : resultados, hayMas);
        }
        catch (PostgresException excepcion)
        {
            throw FallaDelMotor.Traducir(excepcion);
        }
    }

    public async Task<ResultadoDeMencion?> ResolverAsync(
        Guid actor, TipoDeMencion tipo, Guid id, CancellationToken ct)
    {
        try
        {
            await using var conexion = await apertura.AbrirAsync(ct);
            await using var transaccion = await conexion.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

            await PreambuloDelActor.AplicarAsync(conexion, transaccion, actor, ct);

            var sql = tipo == TipoDeMencion.Materia ? SqlMateriaPorId : SqlDocentePorId;
            await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("id", id);

            await using var lector = await comando.ExecuteReaderAsync(ct);
            return await lector.ReadAsync(ct) ? Leer(tipo, lector) : null;
        }
        catch (PostgresException excepcion)
        {
            throw FallaDelMotor.Traducir(excepcion);
        }
    }

    private static ResultadoDeMencion Leer(TipoDeMencion tipo, NpgsqlDataReader lector) => tipo switch
    {
        TipoDeMencion.Materia => new ResultadoDeMencion(
            lector.GetGuid(0),
            Nombre: lector.GetString(1),
            Carrera: lector.GetString(3),
            Codigo: lector.GetString(2)),

        _ => new ResultadoDeMencion(
            lector.GetGuid(0),
            Nombre: $"{lector.GetString(1)} {lector.GetString(2)}",
            Cargo: lector.GetString(3)),
    };

    /// <summary>
    /// Escapa los comodines de <c>LIKE</c> (<c>%</c>, <c>_</c> y la barra de
    /// escape misma) del término escrito por el usuario, para que un signo de
    /// porcentaje tipeado a mano no se comporte como comodín.
    /// </summary>
    /// <remarks>
    /// No hay riesgo de inyección —viaja como parámetro, nunca interpolado en el
    /// texto de la consulta—; esto es sólo para que el patrón de <c>LIKE</c> siga
    /// significando «empieza con lo que se tipeó» y no algo más laxo.
    /// </remarks>
    private static string EscaparParaLike(string termino) =>
        termino.Replace("\\", "\\\\", StringComparison.Ordinal)
               .Replace("%", "\\%", StringComparison.Ordinal)
               .Replace("_", "\\_", StringComparison.Ordinal);
}
