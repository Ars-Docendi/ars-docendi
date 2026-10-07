namespace ArsDocendi.Migraciones;

public enum ModoMigracion { Ninguno, Migrar, Estado, Script, ValidarRecursos }

public sealed record SolicitudMigracion(ModoMigracion Modo, string? Directorio = null)
{
    public static SolicitudMigracion Parsear(string[] args)
    {
        var opciones = new Dictionary<string, ModoMigracion>(StringComparer.Ordinal)
        {
            ["--migrate"] = ModoMigracion.Migrar,
            ["--estado-migraciones"] = ModoMigracion.Estado,
            ["--script-migraciones"] = ModoMigracion.Script,
            ["--validar-recursos"] = ModoMigracion.ValidarRecursos,
        };
        var modos = args.Where(opciones.ContainsKey).ToArray();
        if (modos.Length == 0) return new(ModoMigracion.Ninguno);
        var modo = opciones[modos[0]];
        if (modos.Length != 1 || args[0] != modos[0]
            || args.Length != (modo == ModoMigracion.Script ? 2 : 1))
            throw new ArgumentException("Elegir un único modo de migración con sus argumentos exactos.");
        if (modo != ModoMigracion.Script) return new(modo);
        var ruta = args[1];
        if (ruta != "-" && (string.IsNullOrWhiteSpace(ruta)
            || ruta.Split('/', '\\').Any(segmento => segmento is "." or "..")
            || ruta.StartsWith('-')
            || Path.GetFullPath(ruta) == Path.GetPathRoot(Path.GetFullPath(ruta))))
            throw new ArgumentException("Directorio de preview inseguro; usar una ruta nueva o '-' para tar por stdout.");
        return new(modo, ruta);
    }
}
