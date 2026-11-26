using System.Text.RegularExpressions;

namespace Modules.Asistente.Application;

/// <summary>
/// Whether a question refers to the asker's own scope («los docentes que tengo a cargo», «mis
/// profesores», «de mi cátedra»), which the database already applies through RLS.
/// </summary>
internal static partial class AlcancePropio
{
    /// <summary>True when the question names the asker's own scope.</summary>
    public static bool Nombra(string pregunta) =>
        !string.IsNullOrWhiteSpace(pregunta) && Patron().IsMatch(NormalizadorLexico.SinAcentos(pregunta));

    [GeneratedRegex(
        @"\b((tengo|tenemos) a cargo|a mi cargo|mis (docentes|profesores|materias|asignaturas|catedras|designaciones)"
        + @"|mi (catedra|carrera|materia|asignatura|ambito)|que (dirijo|coordino))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Patron();
}
