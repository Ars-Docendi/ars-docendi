using System.Text;
using System.Text.RegularExpressions;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Key confinement (asistente-modelo-en-caliente, D4 and task 3.13): no response, log
/// call, exception message or audit row can carry the text of a provider key,
/// <c>Inferencia__Contrasena</c> or the Cloudflare Access secret.
/// </summary>
/// <remarks>
/// Like the other scans of this class, each guard comes with a synthetic violation fed
/// to the same detector, so a broken detector cannot pass green forever. The scan is
/// literal-aware: the TEXT of a string is not code (a log message may name
/// <c>Inferencia__Contrasena</c> to tell an operator what to check), but the
/// expressions interpolated into it are, so <c>$"... {clave}"</c> is caught.
/// </remarks>
public sealed partial class ArquitecturaAsistenteTests
{
    /// <summary>The files that handle key text, where the stricter rules apply.</summary>
    private static readonly string[] ArchivosQueManejanClaves =
    [
        "ProveedorChat.cs",
        "ProbadorDeConexion.cs",
        "ClientesDeChat.cs",
        "ResolutorDelProveedor.cs",
        "RepositorioDeCredenciales.cs",
        "CifradoDeCredenciales.cs",
        "AdministracionDelModelo.cs",
        "AdministracionDelModeloController.cs",
        "ModelosDelModelo.cs",
    ];

    /// <summary>The only files that may open a stored key (to send it to its provider).</summary>
    private static readonly string[] PuedenAbrirUnaClaveGuardada =
    [
        "ResolutorDelProveedor.cs",
        "ProbadorDeConexion.cs",
    ];

    private static Archivo[] FuentesDelModuloSinComentarios()
    {
        var raiz = RaizRepositorio.Ruta();
        var modulo = Path.Combine(RaizRepositorio.BackendSrc(), "Modules.Asistente");

        return [.. Directory.EnumerateFiles(modulo, "*.cs", SearchOption.AllDirectories)
            .Where(ruta => !EnCarpeta(ruta, "obj") && !EnCarpeta(ruta, "bin"))
            .Select(ruta => new Archivo(
                Path.GetRelativePath(raiz, ruta), QuitarComentarios(File.ReadAllText(ruta))))];
    }

    private static bool EsArchivo(Archivo archivo, params string[] nombres) =>
        nombres.Contains(Path.GetFileName(archivo.Ruta), StringComparer.Ordinal);

    // ------------------------------------------------------- log, exception, audit

    [Fact]
    public void No_log_call_exception_message_or_audit_row_can_carry_key_text()
    {
        var culpables = ViolacionesDeConfinamiento(FuentesDelModuloSinComentarios());

        Assert.True(
            culpables.Count == 0,
            "A key, Inferencia__Contrasena or the Access secret reaches a log call, an exception, an audit row "
            + "or an error text. Detected in: " + string.Join("; ", culpables));
    }

    [Fact]
    public void The_confinement_detector_recognises_each_leak()
    {
        Archivo[] violaciones =
        [
            new("a/ProveedorChat.cs", "class A { void M() { _log.LogError(\"fallo {Clave}\", clave); } }"),
            new("b/Otro.cs", "class B { void M() { log.LogWarning($\"credencial {claveNueva}\"); } }"),
            new("c/ProbadorDeConexion.cs", "class C { void M() { throw new InvalidOperationException($\"mala {clave}\"); } }"),
            new("d/Otro.cs", "class D { void M() { auditoria.RegistrarAsync(actor, \"x\", null, Serialize(pedido), ct); } }"),
            new("e/AdministracionDelModelo.cs", "class E { void M() { auditoria.RegistrarAsync(actor, \"x\", null, $\"{{\\\"k\\\":\\\"{clave}\\\"}}\", ct); } }"),
            new("f/Otro.cs", "class F { void M() { _log.LogError(\"x {P}\", opciones.Contrasena); } }"),
            new("g/Otro.cs", "class G { void M() { var p = new ProblemDetails { Detail = excepcion.Message }; } }"),
            new("h/Otro.cs", "class H { void M() { var p = new ProblemDetails { Title = $\"x {AccessClientSecret}\" }; } }"),
            new("i/ProveedorChat.cs", "class I { void M() { _log.LogError(\"x\", excepcion.Message); } }"),
            new("j/ProveedorChat.cs", "class J { void M() { throw new HttpRequestException(\"x\", excepcion, estado); } }"),
            new("k/Otro.cs", "class K { void M() { var c = credenciales.AbrirAsync(\"openai\", ct); } }"),
            new("l/Api/ModelosDelModelo.cs", "public sealed record RespuestaDto(string Empresa, string Clave);"),
            new("m/Api/Dto.cs", "public sealed record OtraDto(string Pista, [Required] string ApiKey);"),
            new("n/Otro.cs", "class N { string S(O o) => o.Contrasena; }"),
            new("o/Api/ModelosDelModelo.cs", "public sealed record PedidoDto(string Empresa, string? ClaveNueva = null);"),
        ];

        foreach (var violacion in violaciones)
        {
            Assert.NotEmpty(ViolacionesDeConfinamiento([violacion]));
        }
    }

    [Fact]
    public void The_confinement_detector_accepts_what_the_module_legitimately_does()
    {
        Archivo[] limpios =
        [
            // The TEXT of a message may name a secret setting to tell an operator what to check.
            new("a/ProveedorOllama.cs", "class A { void M() { _log.LogError(\"Revisá Inferencia__Contrasena y Access\", codigo); } }"),
            new("b/ProveedorChat.cs", "class B { void M() { _log.LogWarning(\"HTTP {Estado}\", estado is { } e ? (int)e : 0); } }"),
            new("c/ResolutorDelProveedor.cs", "class C { void M() { log.LogError(\"x {Causa}\", excepcion.GetType().Name); } }"),
            new("d/AdministracionDelModelo.cs", "class D { void M() { auditoria.RegistrarAsync(actor, \"x\", null, Serialize(new { empresa, pista }), ct); } }"),
            new("e/ResolutorDelProveedor.cs", "class E { void M() { var c = credenciales.AbrirAsync(\"openai\", ct); } }"),
            new("f/Api/ModelosDelModelo.cs", "public sealed record PedidoDto(string Empresa, string? ClaveNueva = null)"
                + " { private bool PrintMembers(StringBuilder b) { b.Append(\"***\"); return true; } }"),
            new("g/Api/ModelosDelModelo.cs", "public sealed record CredencialDto(string Estado, string? Pista);"),
            new("h/Otro.cs", "class H { void M() { throw new ArgumentException(\"The key must be longer\", \"clave\"); } }"),
            new("i/OpcionesInferencia.cs", "class I { string S() => Contrasena; }"),
        ];

        Assert.Empty(ViolacionesDeConfinamiento(limpios));
    }

    // ----------------------------------------------------------------- the detector

    private static List<string> ViolacionesDeConfinamiento(IEnumerable<Archivo> archivos)
    {
        var culpables = new List<string>();

        foreach (var archivo in archivos)
        {
            var codigo = archivo.Contenido;
            var enArchivoDeClaves = EsArchivo(archivo, ArchivosQueManejanClaves);

            // The module also says "clave" for a cache key, a provider id or a user id, so the bare
            // word is a secret only where key text is handled; the named secrets are secrets everywhere.
            var secretos = enArchivoDeClaves ? SecretosEstrictos() : Secretos();

            // 1. A log call carries no key identifier; a key-handling file also carries no exception.
            foreach (var argumentos in LlamadasA(codigo, LlamadaDeLog()))
            {
                var expresiones = ExpresionesSinLiterales(argumentos);
                if (secretos.IsMatch(expresiones))
                {
                    culpables.Add($"{archivo.Ruta}: log call with a secret identifier");
                }

                if (enArchivoDeClaves && (ExcepcionEnUnLog().IsMatch(expresiones) || MensajeDeExcepcion().IsMatch(expresiones)))
                {
                    culpables.Add($"{archivo.Ruta}: log call with an exception or its message");
                }
            }

            // 2. An exception message carries no key identifier; in a key-handling file a provider
            //    failure never travels as an inner exception (a provider can echo part of the key).
            foreach (var construccion in ConstruccionesDeExcepcion(codigo))
            {
                var expresiones = ExpresionesSinLiterales(construccion.Argumentos);
                if (secretos.IsMatch(expresiones))
                {
                    culpables.Add($"{archivo.Ruta}: {construccion.Tipo} with a secret identifier");
                }

                if (enArchivoDeClaves
                    && construccion.Tipo is "HttpRequestException" or "InvalidOperationException" or "Exception"
                    && VariableDeExcepcion().IsMatch(expresiones))
                {
                    culpables.Add($"{archivo.Ruta}: {construccion.Tipo} built from another exception");
                }
            }

            // 3. An audit row is built from company, model and hint: no key identifier, no request object.
            foreach (var argumentos in LlamadasA(codigo, LlamadaDeAuditoria()))
            {
                var expresiones = ExpresionesSinLiterales(argumentos);
                if (secretos.IsMatch(expresiones) || ObjetoDePedido().IsMatch(expresiones))
                {
                    culpables.Add($"{archivo.Ruta}: audit row built from a key or a request object");
                }
            }

            // 4. The text of an error response carries no key identifier and no exception message.
            foreach (var expresion in ExpresionesAsignadas(codigo, TextoDeProblema()))
            {
                var expresiones = ExpresionesSinLiterales(expresion);
                if (secretos.IsMatch(expresiones) || MensajeDeExcepcion().IsMatch(expresiones))
                {
                    culpables.Add($"{archivo.Ruta}: error text built from a secret or an exception message");
                }
            }

            // 5. Only the resolver and the prober open a stored key.
            if (!EsArchivo(archivo, PuedenAbrirUnaClaveGuardada) && AperturaDeUnaClave().IsMatch(codigo))
            {
                culpables.Add($"{archivo.Ruta}: opens a stored key");
            }

            // 6. The server credentials of the shared service are named only where they are applied.
            if (!EsArchivo(archivo, "OpcionesInferencia.cs") && CredencialesDelServicio().IsMatch(ExpresionesSinLiterales(codigo)))
            {
                culpables.Add($"{archivo.Ruta}: names an Inferencia credential outside OpcionesInferencia");
            }

            // 7. A response record has no member that could hold a secret. Request records (Pedido*) may.
            if (EsDeApi(archivo.Ruta))
            {
                foreach (var registro in RegistrosDeApi(codigo))
                {
                    var llevaSecretos = secretos.IsMatch(registro.Parametros);
                    if (!registro.Nombre.StartsWith("Pedido", StringComparison.Ordinal))
                    {
                        if (llevaSecretos)
                        {
                            culpables.Add($"{archivo.Ruta}: response record {registro.Nombre} has a secret member");
                        }
                    }
                    else if (llevaSecretos && !codigo.Contains("PrintMembers(StringBuilder", StringComparison.Ordinal))
                    {
                        // The generated ToString would print the key, and the framework logs bound parameters.
                        culpables.Add($"{archivo.Ruta}: request record {registro.Nombre} carries a key and prints it");
                    }
                }
            }
        }

        return culpables;
    }

    private static bool EsDeApi(string ruta) =>
        ("/" + ruta.Replace('\\', '/')).Contains("/Api/", StringComparison.Ordinal);

    private sealed record Construccion(string Tipo, string Argumentos);

    private sealed record RegistroDeApi(string Nombre, string Parametros);

    private static IEnumerable<string> LlamadasA(string codigo, Regex inicio)
    {
        foreach (Match coincidencia in inicio.Matches(codigo))
        {
            var cierre = CerrarParentesis(codigo, coincidencia.Index + coincidencia.Length - 1);
            yield return codigo[(coincidencia.Index + coincidencia.Length)..cierre];
        }
    }

    private static IEnumerable<Construccion> ConstruccionesDeExcepcion(string codigo)
    {
        foreach (Match coincidencia in ConstruccionDeExcepcion().Matches(codigo))
        {
            var cierre = CerrarParentesis(codigo, coincidencia.Index + coincidencia.Length - 1);
            yield return new Construccion(
                coincidencia.Groups["tipo"].Value,
                codigo[(coincidencia.Index + coincidencia.Length)..cierre]);
        }
    }

    private static IEnumerable<RegistroDeApi> RegistrosDeApi(string codigo)
    {
        foreach (Match coincidencia in DeclaracionDeRegistro().Matches(codigo))
        {
            var cierre = CerrarParentesis(codigo, coincidencia.Index + coincidencia.Length - 1);
            yield return new RegistroDeApi(
                coincidencia.Groups["nombre"].Value,
                codigo[(coincidencia.Index + coincidencia.Length)..cierre]);
        }
    }

    /// <summary>The expressions assigned after each match, up to the next top-level comma, semicolon or brace.</summary>
    private static IEnumerable<string> ExpresionesAsignadas(string codigo, Regex asignacion)
    {
        foreach (Match coincidencia in asignacion.Matches(codigo))
        {
            var i = coincidencia.Index + coincidencia.Length;
            var inicio = i;
            var profundidad = 0;

            while (i < codigo.Length)
            {
                if (EsInicioDeLiteral(codigo, i))
                {
                    i = FinDeLiteral(codigo, i, out _);
                    continue;
                }

                var c = codigo[i];
                if (c is '(' or '[')
                {
                    profundidad++;
                }
                else if (c is ')' or ']')
                {
                    profundidad--;
                }
                else if (profundidad <= 0 && c is ',' or ';' or '}')
                {
                    break;
                }

                i++;
            }

            yield return codigo[inicio..Math.Min(i, codigo.Length)];
        }
    }

    /// <summary>Index of the parenthesis that closes the one at <paramref name="abre"/>, skipping string literals.</summary>
    private static int CerrarParentesis(string codigo, int abre)
    {
        var profundidad = 0;
        var i = abre;

        while (i < codigo.Length)
        {
            if (EsInicioDeLiteral(codigo, i))
            {
                i = FinDeLiteral(codigo, i, out _);
                continue;
            }

            if (codigo[i] == '\'')
            {
                i = FinDeCaracter(codigo, i);
                continue;
            }

            if (codigo[i] == '(')
            {
                profundidad++;
            }
            else if (codigo[i] == ')' && --profundidad == 0)
            {
                return i;
            }

            i++;
        }

        return codigo.Length;
    }

    /// <summary>The code with every string literal replaced by the expressions interpolated into it.</summary>
    private static string ExpresionesSinLiterales(string codigo)
    {
        var salida = new StringBuilder();
        var i = 0;

        while (i < codigo.Length)
        {
            if (EsInicioDeLiteral(codigo, i))
            {
                i = FinDeLiteral(codigo, i, out var huecos);
                salida.Append(' ').Append(string.Join(' ', huecos)).Append(' ');
                continue;
            }

            if (codigo[i] == '\'')
            {
                i = FinDeCaracter(codigo, i);
                salida.Append(' ');
                continue;
            }

            salida.Append(codigo[i++]);
        }

        return salida.ToString();
    }

    /// <summary>Comments removed, string literals left intact (a <c>//</c> inside a URL is not a comment).</summary>
    private static string QuitarComentarios(string codigo)
    {
        var salida = new StringBuilder(codigo.Length);
        var i = 0;

        while (i < codigo.Length)
        {
            if (EsInicioDeLiteral(codigo, i))
            {
                var fin = FinDeLiteral(codigo, i, out _);
                salida.Append(codigo, i, fin - i);
                i = fin;
            }
            else if (codigo[i] == '\'')
            {
                var fin = FinDeCaracter(codigo, i);
                salida.Append(codigo, i, fin - i);
                i = fin;
            }
            else if (codigo[i] == '/' && i + 1 < codigo.Length && codigo[i + 1] == '/')
            {
                while (i < codigo.Length && codigo[i] != '\n')
                {
                    i++;
                }
            }
            else if (codigo[i] == '/' && i + 1 < codigo.Length && codigo[i + 1] == '*')
            {
                var fin = codigo.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = fin < 0 ? codigo.Length : fin + 2;
            }
            else
            {
                salida.Append(codigo[i++]);
            }
        }

        return salida.ToString();
    }

    private static int FinDeCaracter(string codigo, int i)
    {
        var j = i + 1;
        while (j < codigo.Length && codigo[j] != '\'')
        {
            j += codigo[j] == '\\' ? 2 : 1;
        }

        return Math.Min(j + 1, codigo.Length);
    }

    private static bool EsInicioDeLiteral(string codigo, int i)
    {
        if (codigo[i] == '"')
        {
            return true;
        }

        if (codigo[i] is not ('$' or '@'))
        {
            return false;
        }

        var j = i;
        while (j < codigo.Length && codigo[j] is '$' or '@')
        {
            j++;
        }

        return j < codigo.Length && codigo[j] == '"' && (i == 0 || !char.IsLetterOrDigit(codigo[i - 1]));
    }

    /// <summary>
    /// Index just after the string literal that starts at <paramref name="i"/> (regular,
    /// verbatim, raw, interpolated), and the expressions of its interpolation holes.
    /// </summary>
    private static int FinDeLiteral(string codigo, int i, out List<string> huecos)
    {
        huecos = [];
        var j = i;
        var dolares = 0;
        var verbatim = false;

        while (codigo[j] is '$' or '@')
        {
            if (codigo[j] == '$')
            {
                dolares++;
            }
            else
            {
                verbatim = true;
            }

            j++;
        }

        var comillas = 0;
        while (j < codigo.Length && codigo[j] == '"')
        {
            comillas++;
            j++;
        }

        if (comillas == 2)
        {
            return j;
        }

        var cruda = comillas >= 3;
        while (j < codigo.Length)
        {
            var c = codigo[j];

            if (cruda)
            {
                if (c == '"' && CuentaDeComillas(codigo, j) >= comillas)
                {
                    return j + comillas;
                }
            }
            else if (verbatim)
            {
                if (c == '"')
                {
                    if (j + 1 < codigo.Length && codigo[j + 1] == '"')
                    {
                        j += 2;
                        continue;
                    }

                    return j + 1;
                }
            }
            else
            {
                if (c == '\\')
                {
                    j += 2;
                    continue;
                }

                if (c == '"')
                {
                    return j + 1;
                }
            }

            if (dolares > 0 && c == '{')
            {
                if (!cruda && j + 1 < codigo.Length && codigo[j + 1] == '{')
                {
                    j += 2;
                    continue;
                }

                var profundidad = 1;
                var inicio = j + 1;
                j++;
                while (j < codigo.Length && profundidad > 0)
                {
                    profundidad += codigo[j] == '{' ? 1 : codigo[j] == '}' ? -1 : 0;
                    j++;
                }

                huecos.Add(codigo[inicio..Math.Max(inicio, j - 1)]);
                continue;
            }

            j++;
        }

        return codigo.Length;
    }

    private static int CuentaDeComillas(string codigo, int desde)
    {
        var n = 0;
        while (desde + n < codigo.Length && codigo[desde + n] == '"')
        {
            n++;
        }

        return n;
    }

    // Identifiers that hold, or name, a secret. Matched as whole words on code with literals removed.
    [GeneratedRegex(
        @"\b(?:claveNueva|claveEnClaro|ClaveDelProveedor|ClaveMaestra\w*|ClaveDescifrada|contrasena\w*|apikey|password\w*|\w*secret)\b"
        + @"|\b(?:credencial|credenciales|abierta|descifrada)\w*\s*\.\s*Clave\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Secretos();

    // The same, plus the bare word where key text is handled.
    [GeneratedRegex(
        @"\bclave\b|\b(?:claveNueva|claveEnClaro|ClaveDelProveedor|ClaveMaestra\w*|ClaveDescifrada|contrasena\w*|apikey|password\w*|\w*secret)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretosEstrictos();

    [GeneratedRegex(@"\b(?:_?log|_?logger)\s*\.\s*Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\(")]
    private static partial Regex LlamadaDeLog();

    [GeneratedRegex(@"\bauditoria\s*\.\s*RegistrarAsync\s*\(")]
    private static partial Regex LlamadaDeAuditoria();

    [GeneratedRegex(@"\bnew\s+(?:\w+\s*\.\s*)*(?<tipo>\w*Exception)\s*\(")]
    private static partial Regex ConstruccionDeExcepcion();

    [GeneratedRegex(@"\b(?:Title|Detail)\s*=(?!=)")]
    private static partial Regex TextoDeProblema();

    [GeneratedRegex(@"\b(?:excepcion|ex)\b(?!\s*\.\s*GetType\s*\(\s*\)\s*\.\s*Name)")]
    private static partial Regex ExcepcionEnUnLog();

    [GeneratedRegex(@"\.\s*Message\b")]
    private static partial Regex MensajeDeExcepcion();

    [GeneratedRegex(@"\b(?:excepcion|ex)\b")]
    private static partial Regex VariableDeExcepcion();

    [GeneratedRegex(@"\bpedido\w*\b(?!\s*\.\s*(?:Empresa|Modelo)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex ObjetoDePedido();

    // Opening a stored key: the repository call on a credentials repository, or its result types.
    [GeneratedRegex(@"[Cc]redenciales\s*\)?\s*\.\s*AbrirAsync\s*\(|GetRequiredService<RepositorioDeCredenciales>\(\)\s*\.\s*AbrirAsync")]
    private static partial Regex AperturaDeUnaClave();

    [GeneratedRegex(@"\b(?:Contrasena|AccessClientSecret|AccessClientId)\b")]
    private static partial Regex CredencialesDelServicio();

    [GeneratedRegex(@"\brecord\s+(?<nombre>\w+)\s*\(")]
    private static partial Regex DeclaracionDeRegistro();
}
