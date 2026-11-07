namespace Modules.Asistente.Application;

/// <summary>Un par pregunta-consulta verificado del catálogo.</summary>
public sealed record EjemploSql(string Pregunta, string Sql, string Categoria);

/// <summary>Elige del catálogo los ejemplos más parecidos a la pregunta del turno.</summary>
public interface ISelectorDeEjemplos
{
    /// <summary>Huella estable del catálogo, para el sellado de reportes.</summary>
    string Huella { get; }

    /// <summary>Todos los ejemplos del catálogo, en el orden en que están escritos.</summary>
    IReadOnlyList<EjemploSql> Catalogo { get; }

    /// <summary>Devuelve los ejemplos más parecidos, de mayor a menor parecido.</summary>
    IReadOnlyList<EjemploSql> Elegir(string pregunta);
}
