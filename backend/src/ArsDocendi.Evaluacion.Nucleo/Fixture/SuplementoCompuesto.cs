using System.Globalization;
using System.Text;

namespace ArsDocendi.Evaluacion.Nucleo.Fixture;

/// <summary>
/// El suplemento del fixture para las preguntas compuestas (change
/// <c>asistente-plan-compilado</c>, D10).
/// </summary>
/// <remarks>
/// <b>El fixture de siempre no discrimina ninguna pregunta compuesta</b>: tiene
/// exactamente una designación por persona, todas desde la misma fecha, así que
/// «en más de una carrera» siempre da cero y «más de 20 años» también. El
/// suplemento agrega, sobre las mismas personas y materias:
///
/// <list type="bullet">
/// <item>designaciones vigentes en otras materias y carreras, para que haya gente
/// con dos y tres carreras, con cargos distintos en cada una;</item>
/// <item>designaciones históricas antiguas, ya cerradas, para que la antigüedad
/// desde la primera designación varíe entre 1 y 31 años;</item>
/// <item>experiencias declaradas en el portal que NO coinciden con esa
/// antigüedad, para que las dos definiciones den respuestas distintas.</item>
/// </list>
///
/// Los índices de persona y materia son los del generador; ver los comentarios de
/// cada fila. Los identificadores nuevos arrancan en 1000 para no chocar con los
/// del fixture base.
/// </remarks>
internal static class SuplementoCompuesto
{
    private const int Base = 1000;

    // Índices de cargo en el orden de GeneradorDeFixture.CodigosDeCargo.
    private const int Titular = 0;
    private const int Asociado = 1;
    private const int Adjunto = 2;
    private const int Jtp = 3;
    private const int AyudanteDePrimera = 4;

    /// <summary>(persona, materia, cargo, dedicación, desde, hasta o nulo).</summary>
    private static readonly (int Persona, int Materia, int Cargo, int Dedicacion, string Desde, string? Hasta)[] Designaciones =
    [
        // Vigentes adicionales: más de una carrera o de una materia.
        (0, 3, Titular, 3, "2024-03-01", null),            // titular INF + titular IND → 2 carreras
        (6, 8, Adjunto, 5, "2024-03-01", null),            // titular ELE + adjunto IND …
        (6, 10, Jtp, 1, "2024-03-01", null),               // … + JTP INF → 3 carreras, 3 materias
        (12, 1, Titular, 5, "2024-03-01", null),           // titular INF en dos materias → 1 carrera
        (1, 5, AyudanteDePrimera, 1, "2024-03-01", null),  // asociado INF + ayudante 1.º ELE → 2 carreras
        (7, 13, Asociado, 3, "2024-03-01", null),          // asociado INF + asociado IND → 2 carreras
        // Categoría 6 a propósito: es la única, y la persona no tiene ninguna de categoría 5,
        // así que «categoría 5» y «categoría 5 o más» dan resultados distintos (`cmp-016`).
        // Agregada el 2026-10-08 mirando esa falla: sin ella las dos lecturas coincidían.
        (3, 11, Jtp, 6, "2024-03-01", null),               // JTP IND en dos materias → 1 carrera

        // Históricas cerradas: fijan la antigüedad desde la primera designación.
        (0, 2, Adjunto, 3, "1998-03-01", "2005-12-31"),    // 28 años
        (6, 6, Adjunto, 3, "2010-03-01", "2015-02-28"),    // 16 años
        (12, 0, Adjunto, 3, "2001-03-01", "2010-12-31"),   // 25 años
        (1, 2, Jtp, 1, "1999-03-01", "2003-12-31"),        // 27 años
        (13, 4, Jtp, 1, "2004-03-01", "2008-12-31"),       // 22 años
        (3, 3, AyudanteDePrimera, 1, "1995-03-01", "2000-12-31"), // 31 años
    ];

    /// <summary>(perfil, desde): experiencias declaradas que no coinciden con las designaciones.</summary>
    private static readonly (int Perfil, string Desde)[] Experiencias =
    [
        (0, "1990-03-01"), // 36 años declarados; 28 desde la designación
        (6, "2000-03-01"), // 26 declarados; 16 desde la designación
        (1, "2010-03-01"), // 16 declarados; 27 desde la designación
    ];

    public static void Escribir(StringBuilder sql)
    {
        sql.Append(
            """

            -- Suplemento de preguntas compuestas (asistente-plan-compilado). Solo con
            -- GeneradorDeFixture(conSuplementoCompuesto: true).

            """);

        sql.Append(
            "\nINSERT INTO designaciones.designaciones "
            + "(id, persona_id, materia_id, cargo_id, dedicacion_id, horas, vigente_desde, vigente_hasta) VALUES\n");

        var filas = Designaciones.Select((fila, indice) =>
            $"    ('{GeneradorDeFixture.IdDeDesignacion(Base + indice)}', '{GeneradorDeFixture.IdDePersona(fila.Persona)}', "
            + $"'{GeneradorDeFixture.IdDeMateria(fila.Materia)}', '{GeneradorDeFixture.IdDeCargo(fila.Cargo)}', "
            + $"'d6000000-0000-4000-8000-{fila.Dedicacion.ToString("D12", CultureInfo.InvariantCulture)}', 6, "
            + $"DATE '{fila.Desde}', "
            + (fila.Hasta is null ? "NULL)" : $"DATE '{fila.Hasta}')"));

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");

        sql.Append(
            "\nINSERT INTO portal.experiencias "
            + "(id, perfil_id, puesto, organizacion, descripcion, desde, hasta) VALUES\n");

        var experiencias = Experiencias.Select((fila, indice) =>
            $"    ('{GeneradorDeFixture.IdDeExperiencia(Base + indice)}', '{GeneradorDeFixture.IdDePerfil(fila.Perfil)}', "
            + $"'Docente', 'Otra institución', '', DATE '{fila.Desde}', NULL)");

        sql.Append(string.Join(",\n", experiencias));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }
}
