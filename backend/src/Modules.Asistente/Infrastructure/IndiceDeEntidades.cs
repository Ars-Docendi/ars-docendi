using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Construye el catálogo de entidades leyendo la base, y lo cachea.</summary>
internal sealed class IndiceDeEntidades(AperturaDeLectura apertura) : IIndiceDeEntidades
{
    /// <summary>Materias con su carrera, y personas con su nombre completo.</summary>
    private const string Sql = """
        SELECT 'materia' AS clase,
               m.name     AS valor,
               c.name     AS discriminador
          FROM identity.materias m
          JOIN identity.materias_plan mp ON mp.materia_id = m.id AND mp.activo
          JOIN identity.planes pl ON pl.id = mp.plan_id AND pl.vigente AND pl.activo
          JOIN identity.carreras c ON c.id = pl.carrera_id
         WHERE m.is_active

        UNION ALL

        SELECT 'persona' AS clase,
               p.apellido AS valor,
               p.nombre || ' ' || p.apellido AS discriminador
          FROM identity.personas p
        """;

    private readonly ValorPerezoso<CatalogoDeEntidades> _catalogo = new();

    /// <summary>Veces que se consultó la base. Existe para los tests del caché.</summary>
    internal int Lecturas => _catalogo.Calculos;

    public Task<CatalogoDeEntidades> ObtenerAsync(CancellationToken ct) =>
        _catalogo.ObtenerAsync(ConstruirAsync, ct);

    private async Task<CatalogoDeEntidades> ConstruirAsync(CancellationToken ct)
    {
        var valores = new List<ValorDelDominio>();

        await using var conexion = await apertura.AbrirAsync(ct);

        await using var comando = new NpgsqlCommand(Sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            var clase = lector.GetString(0) == "materia"
                ? ClaseDeEntidad.Materia
                : ClaseDeEntidad.Persona;

            var valor = lector.GetString(1);

            valores.Add(new ValorDelDominio(
                clase,
                valor,
                Normalizar(valor),
                lector.GetString(2)));
        }

        // Las personas se indexan por apellido, así que dos homónimos exactos producirían dos
        // entradas iguales y una colisión que no se puede desambiguar por nombre.
        var distintos = valores
            .GroupBy(v => (v.Clase, v.Termino, v.Discriminador))
            .Select(g => g.First());

        return new CatalogoDeEntidades(distintos);
    }

    /// <summary>
    /// Normaliza un valor con el mismo criterio con que se normaliza la pregunta.
    /// </summary>
    internal static string Normalizar(string valor) =>
        string.Join(' ', NormalizadorLexico.Palabras(valor));
}
