using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Persistencia;

/// <summary>Fuente única del orden de recursos de una migración SQL.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RecursosMigracionSqlAttribute : Attribute
{
    public IReadOnlyList<string> Rutas { get; }

    public RecursosMigracionSqlAttribute(params string[] rutas)
    {
        if (rutas.Length == 0 || rutas.Any(ruta => !Regex.IsMatch(ruta,
                @"\A[a-z][a-z0-9_-]*/[a-zA-Z0-9_-]+\.sql\z", RegexOptions.CultureInvariant))
            || rutas.Distinct(StringComparer.Ordinal).Count() != rutas.Length)
            throw new ArgumentException("Recursos SQL vacíos, duplicados o con rutas inválidas.", nameof(rutas));
        Rutas = Array.AsReadOnly((string[])rutas.Clone());
    }
}

public static class MigracionSqlExtensions
{
    public static void AplicarRecursosSql(this MigrationBuilder builder, Type migracion)
    {
        foreach (var sql in LeerRecursos(migracion))
            builder.Sql(sql);
    }

    public static IReadOnlyList<string> LeerRecursos(Type migracion)
    {
        var declaracion = migracion.GetCustomAttribute<RecursosMigracionSqlAttribute>()
            ?? throw new InvalidOperationException($"La migración {migracion.Name} no declara sus recursos SQL.");
        return declaracion.Rutas.Select(ruta => RecursosSql.Leer(migracion.Assembly, ruta)).ToArray();
    }
}
