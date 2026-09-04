using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Construye el catálogo de entidades leyendo la base, y lo cachea.
/// </summary>
/// <remarks>
/// <b>Se carga de la base, no se escribe a mano.</b> Un índice embebido en el
/// código empieza correcto y deja de estarlo con la primera materia nueva, sin que
/// nada falle: el detector simplemente deja de disparar y las preguntas ambiguas
/// pasan a responderse con una de las opciones, en silencio.
///
/// <b>Perezoso</b>, por el mismo motivo que el prefijo del prompt y el catálogo de
/// sensibilidad: construirlo al arrancar exigiría base durante el arranque del
/// Host, y el invariante #3 pide que el <c>ping</c> responda con la base detenida.
///
/// Se lee con la conexión básica: nombre de materia, carrera, nombre y apellido son
/// columnas públicas para los dos roles.
/// </remarks>
internal sealed class IndiceDeEntidades(CadenaSoloLectura cadena) : IIndiceDeEntidades
{
    /// <summary>
    /// Materias con su carrera, y personas con su nombre completo.
    /// </summary>
    /// <remarks>
    /// Los esquemas van cualificados: los roles del asistente corren con
    /// <c>search_path</c> vacío.
    /// </remarks>
    private const string Sql = """
        SELECT 'materia' AS clase,
               m.name     AS valor,
               c.name     AS discriminador
          FROM identity.materias m
          JOIN identity.carreras c ON c.id = m.carrera_id
         WHERE m.is_active

        UNION ALL

        SELECT 'persona' AS clase,
               p.apellido AS valor,
               p.nombre || ' ' || p.apellido AS discriminador
          FROM identity.personas p
        """;

    private readonly SemaphoreSlim _turnoDeCalculo = new(1, 1);
    private CatalogoDeEntidades? _catalogo;

    /// <summary>Veces que se consultó la base. Existe para los tests del caché.</summary>
    internal int Lecturas { get; private set; }

    public async Task<CatalogoDeEntidades> ObtenerAsync(CancellationToken ct)
    {
        if (_catalogo is not null)
        {
            return _catalogo;
        }

        await _turnoDeCalculo.WaitAsync(ct);
        try
        {
            if (_catalogo is not null)
            {
                return _catalogo;
            }

            _catalogo = await ConstruirAsync(ct);
            return _catalogo;
        }
        finally
        {
            _turnoDeCalculo.Release();
        }
    }

    private async Task<CatalogoDeEntidades> ConstruirAsync(CancellationToken ct)
    {
        var valores = new List<ValorDelDominio>();

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

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

        Lecturas++;

        // Las personas se indexan por apellido, así que dos homónimos exactos
        // producirían dos entradas iguales y una colisión que no se puede
        // desambiguar por nombre. Se colapsan: para el detector, «hay dos personas
        // llamadas igual» y «hay una» se responden igual, porque el menú no
        // distinguiría entre ellas.
        var distintos = valores
            .GroupBy(v => (v.Clase, v.Termino, v.Discriminador))
            .Select(g => g.First());

        return new CatalogoDeEntidades(distintos);
    }

    /// <summary>
    /// Normaliza un valor con el mismo criterio con que se normaliza la pregunta.
    /// </summary>
    /// <remarks>
    /// Tiene que ser el mismo o el término del índice nunca coincidiría con el
    /// texto del usuario. Se usa <c>Palabras</c> y no <c>Terminos</c>: este último
    /// tira palabras vacías y aplica sinónimos, y «Inglés Técnico I» perdería la
    /// pieza que lo distingue.
    /// </remarks>
    internal static string Normalizar(string valor) =>
        string.Join(' ', NormalizadorLexico.Palabras(valor));
}
