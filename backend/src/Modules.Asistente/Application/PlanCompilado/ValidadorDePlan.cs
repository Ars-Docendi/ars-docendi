using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>El resultado de validar una muestra: el plan tipado, o por qué no lo es.</summary>
internal sealed record VeredictoDelPlan(PlanValidado? Plan, string? Motivo)
{
    public bool EsValido => Plan is not null;

    public static VeredictoDelPlan Invalido(string motivo) => new(null, motivo);
}

/// <summary>
/// Valida una muestra del plan contra el catálogo y contra la pregunta (D6 de
/// <c>asistente-plan-compilado</c>).
/// </summary>
/// <remarks>
/// <b>En los dos sentidos.</b> Hacia adelante, cada condición tiene que estar
/// anclada en el texto: su término, su valor y, si compara, una señal compatible.
/// Hacia atrás, todo término del catálogo que aparece en la pregunta tiene que
/// estar en el plan. Lo primero frena las condiciones inventadas; lo segundo, los
/// filtros que se pierden en silencio —el defecto que tiene hoy el enrutador de
/// intenciones—. Una muestra que falla cualquiera de las dos no se ejecuta.
/// </remarks>
internal static class ValidadorDePlan
{
    private static readonly string[] Negaciones = ["no", "sin", "excepto", "salvo", "menos los", "menos las"];

    private static readonly string[] PalabrasVaciasDeEntidad =
        ["de", "del", "la", "el", "los", "las", "en", "y", "e", "a"];

    /// <summary>Palabras que no distinguen una carrera o una materia de otra.</summary>
    private static readonly string[] PalabrasGenericas =
        ["ingenieria", "licenciatura", "tecnicatura", "carrera", "materia"];

    private static readonly (string Operador, string[] Senales)[] SenalesDeComparacion =
    [
        (">", ["mas de", "mayor a", "mayor que", "mayores a", "superior a", "supera", "superan", "por encima de"]),
        (">=", ["al menos", "como minimo", "por lo menos", "o mas", "minimo"]),
        ("<", ["menos de", "menor a", "menor que", "inferior a", "por debajo de"]),
        ("<=", ["como maximo", "a lo sumo", "o menos", "maximo", "hasta"]),
        ("=", ["exactamente", "justo", "solo", "solamente"]),
    ];

    private static readonly string[] SenalesDePorcentaje = ["porcentaje", "proporcion", "que parte", "fraccion"];
    private static readonly string[] SenalesDeListado =
        ["quienes", "quien", "cuales", "lista", "listado", "listame", "nombres", "nombrame"];
    private static readonly string[] SenalesDeConteo = ["cuantos", "cuantas", "numero de"];

    private static readonly string[] ExactosSinSenal =
        [CatalogoDelPlan.Dedicacion, CatalogoDelPlan.CantidadDeCarreras, CatalogoDelPlan.CantidadDeMaterias];

    public static VeredictoDelPlan Validar(
        PlanDeConsulta plan, TextoDeLaPregunta texto, IReadOnlyList<string>? carreras = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(texto);

        if (!plan.Expresable)
        {
            return VeredictoDelPlan.Invalido("La muestra declaró la pregunta no expresable.");
        }

        if (!CatalogoDelPlan.Medidas.Contains(plan.Medida))
        {
            return VeredictoDelPlan.Invalido($"La medida '{plan.Medida}' no está en el catálogo.");
        }

        var esPorcentaje = plan.Medida == CatalogoDelPlan.Porcentaje;
        if (esPorcentaje && plan.Condiciones.Count == 0)
        {
            return VeredictoDelPlan.Invalido("Un porcentaje necesita al menos una condición.");
        }

        var filtros = new List<CondicionValidada>();
        var condiciones = new List<CondicionValidada>();
        var indice = 0;

        // En conteo y listado las dos listas son la misma conjunción: la muestra que
        // reparte sus condiciones entre ambas dice lo mismo que la que las pone todas en
        // `filtros`, así que se pliegan ahí. Solo el porcentaje separa el denominador
        // del numerador. Rechazarla era abstenerse por la forma y no por el contenido.
        var destinoDeCondiciones = esPorcentaje ? condiciones : filtros;

        foreach (var (cruda, destino) in plan.Filtros.Select(c => (c, filtros))
                     .Concat(plan.Condiciones.Select(c => (c, destinoDeCondiciones))))
        {
            var (validada, motivo) = ValidarCondicion(cruda, indice++, texto);
            if (validada is null)
            {
                return VeredictoDelPlan.Invalido(motivo!);
            }

            destino.Add(validada);
        }

        var validado = new PlanValidado(plan.Medida, filtros, condiciones);

        var sinConsumir = TerminoSinConsumir(validado, texto, carreras ?? []);
        if (sinConsumir is not null)
        {
            return VeredictoDelPlan.Invalido($"La pregunta nombra {sinConsumir} y el plan no lo usa.");
        }

        var medidaEsperada = MedidaSenalada(texto);
        if (medidaEsperada is not null && medidaEsperada != plan.Medida)
        {
            return VeredictoDelPlan.Invalido(
                $"La pregunta pide {medidaEsperada} y el plan calcula {plan.Medida}.");
        }

        return new VeredictoDelPlan(validado, null);
    }

    private static (CondicionValidada? Condicion, string? Motivo) ValidarCondicion(
        CondicionDelPlan cruda, int indice, TextoDeLaPregunta texto)
    {
        if (!CatalogoDelPlan.Campos.TryGetValue(cruda.Campo, out var campo))
        {
            return (null, $"El campo '{cruda.Campo}' no está en el catálogo.");
        }

        if (!campo.Operadores.Contains(cruda.Operador))
        {
            return (null, $"El campo '{campo.Nombre}' no admite el operador '{cruda.Operador}'.");
        }

        if (cruda.Operador == "!=" && !texto.ContieneAlguna(Negaciones))
        {
            return (null, $"La condición '{campo.Nombre} !=' no tiene una negación en la pregunta.");
        }

        return campo.Tipo switch
        {
            TipoDeCampo.Cargo => ValidarCargo(cruda, campo, indice, texto),
            TipoDeCampo.Entidad => ValidarEntidad(cruda, campo, indice, texto),
            _ => ValidarEntero(cruda, campo, indice, texto),
        };
    }

    private static (CondicionValidada?, string?) ValidarCargo(
        CondicionDelPlan cruda, CampoDelPlan campo, int indice, TextoDeLaPregunta texto)
    {
        var cargo = CatalogoDelPlan.CargoDe(cruda.Valor);
        if (cargo is null)
        {
            return (null, $"El cargo '{cruda.Valor}' no está en el catálogo.");
        }

        return texto.ContieneAlguna(cargo.Sinonimos)
            ? (new CondicionValidada(indice, campo, cruda.Operador, cargo.Codigo, null, null, null), null)
            : (null, $"El cargo '{cargo.Codigo}' no aparece en la pregunta.");
    }

    private static (CondicionValidada?, string?) ValidarEntidad(
        CondicionDelPlan cruda, CampoDelPlan campo, int indice, TextoDeLaPregunta texto)
    {
        var nombre = TextoDeLaPregunta.Normalizar(cruda.Valor);
        if (nombre.Length == 0)
        {
            return (null, $"La condición '{campo.Nombre}' no tiene valor.");
        }

        return EstaNombrada(nombre, texto)
            ? (new CondicionValidada(indice, campo, cruda.Operador, null, null, nombre, cruda.Valor.Trim()), null)
            : (null, $"La {campo.Nombre} '{cruda.Valor}' no aparece en la pregunta.");
    }

    private static (CondicionValidada?, string?) ValidarEntero(
        CondicionDelPlan cruda, CampoDelPlan campo, int indice, TextoDeLaPregunta texto)
    {
        if (!int.TryParse(cruda.Valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
            || numero < campo.Minimo
            || numero > campo.Maximo)
        {
            return (null, $"El valor '{cruda.Valor}' de '{campo.Nombre}' no es un entero del rango.");
        }

        if (!texto.ContieneAlguna(campo.Terminos))
        {
            return (null, $"La pregunta no nombra '{campo.Nombre}'.");
        }

        var calificadores = campo.Nombre switch
        {
            CatalogoDelPlan.AntiguedadDesdeDesignacion => CatalogoDelPlan.CalificadoresDeDesignacion,
            CatalogoDelPlan.AntiguedadDeclarada => CatalogoDelPlan.CalificadoresDeDeclarada,
            _ => null,
        };

        if (calificadores is not null && !texto.ContieneAlguna(calificadores))
        {
            return (null, $"La pregunta no califica la antigüedad como '{campo.Nombre}'.");
        }

        var ventanas = texto.VentanasDelNumero(numero);
        if (ventanas.Count == 0)
        {
            return (null, $"El número {numero} no aparece en la pregunta.");
        }

        var operador = cruda.Operador;

        // Sin señal de comparación junto al número, la categoría y las cantidades son
        // exactamente ese número: «categoría 5» es una etiqueta, y «en dos carreras» quedó
        // fijado en «exactamente dos» (decisión del 2026-10-08). El modelo local igual
        // escribe «>=», y con las tres muestras de acuerdo el turno respondía «al menos».
        // Se corrige el operador en vez de rechazar la muestra, que sería abstenerse de
        // una pregunta que tiene una sola lectura. La antigüedad sigue admitiendo las dos.
        if (ExactosSinSenal.Contains(campo.Nombre, StringComparer.Ordinal)
            && operador == ">="
            && ventanas.All(ventana => Senalados(ventana).Count == 0))
        {
            operador = "=";
        }

        return ventanas.Any(ventana => OperadoresSenalados(ventana).Contains(operador))
            ? (new CondicionValidada(indice, campo, operador, null, numero, null, null), null)
            : (null, $"El operador '{operador}' no corresponde a cómo la pregunta compara {numero}.");
    }

    /// <summary>
    /// Los operadores que admite el entorno de un número. Sin señal, «=» o «&gt;=»; en la
    /// categoría y las cantidades ese «&gt;=» ya llegó corregido a «=».
    /// </summary>
    private static IReadOnlySet<string> OperadoresSenalados(string ventana)
    {
        var senalados = Senalados(ventana);

        return senalados.Count > 0 ? senalados : new HashSet<string>(["=", ">="], StringComparer.Ordinal);
    }

    /// <summary>Los operadores que la pregunta señala con palabras junto al número; vacío si no hay señal.</summary>
    private static HashSet<string> Senalados(string ventana) =>
        SenalesDeComparacion
            .Where(par => par.Senales.Any(senal => ventana.Contains($" {senal} ", StringComparison.Ordinal)))
            .Select(par => par.Operador)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Si la entidad está nombrada en la pregunta: todas sus palabras distintivas, o
    /// alguna si el nombre es solo genérico.
    /// </summary>
    internal static bool EstaNombrada(string nombreNormalizado, TextoDeLaPregunta texto)
    {
        var palabras = nombreNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(palabra => !PalabrasVaciasDeEntidad.Contains(palabra))
            .ToArray();

        var distintivas = palabras.Where(palabra => !PalabrasGenericas.Contains(palabra)).ToArray();

        return distintivas.Length > 0
            ? distintivas.All(texto.Contiene)
            : palabras.Any(texto.Contiene);
    }

    private static string? TerminoSinConsumir(
        PlanValidado plan, TextoDeLaPregunta texto, IReadOnlyList<string> carreras)
    {
        bool Usa(params string[] campos) => plan.Todas.Any(c => campos.Contains(c.Campo.Nombre));

        foreach (var cargo in CatalogoDelPlan.Cargos)
        {
            if (texto.ContieneAlguna(cargo.Sinonimos)
                && !plan.Todas.Any(c => c.Codigo == cargo.Codigo))
            {
                return $"el cargo {cargo.Etiqueta}";
            }
        }

        if (texto.ContieneAlguna(CatalogoDelPlan.PalabrasDeCarrera)
            && !Usa(CatalogoDelPlan.Carrera, CatalogoDelPlan.CantidadDeCarreras))
        {
            return "una carrera";
        }

        if (texto.ContieneAlguna(CatalogoDelPlan.PalabrasDeMateria)
            && !Usa(CatalogoDelPlan.Materia, CatalogoDelPlan.CantidadDeMaterias))
        {
            return "una materia";
        }

        if (texto.ContieneAlguna(CatalogoDelPlan.PalabrasDeDedicacion) && !Usa(CatalogoDelPlan.Dedicacion))
        {
            return "una categoría";
        }

        if (texto.ContieneAlguna(["antiguedad", "anos"])
            && !Usa(CatalogoDelPlan.AntiguedadDesdeDesignacion, CatalogoDelPlan.AntiguedadDeclarada))
        {
            return "la antigüedad";
        }

        if (texto.ContieneAlguna(Negaciones) && !plan.Todas.Any(c => c.Operador == "!="))
        {
            return "una negación";
        }

        var carreraNombrada = carreras.FirstOrDefault(carrera =>
            EstaNombrada(TextoDeLaPregunta.Normalizar(carrera), texto)
            && TextoDeLaPregunta.Normalizar(carrera).Split(' ').Any(palabra => !PalabrasGenericas.Contains(palabra)
                && !PalabrasVaciasDeEntidad.Contains(palabra)));

        return carreraNombrada is not null && !Usa(CatalogoDelPlan.Carrera)
            ? $"la carrera {carreraNombrada}"
            : null;
    }

    private static string? MedidaSenalada(TextoDeLaPregunta texto)
    {
        if (texto.ContieneAlguna(SenalesDePorcentaje) || texto.Original.Contains('%', StringComparison.Ordinal))
        {
            return CatalogoDelPlan.Porcentaje;
        }

        var listado = texto.ContieneAlguna(SenalesDeListado);
        var conteo = texto.ContieneAlguna(SenalesDeConteo);

        return (listado, conteo) switch
        {
            (true, false) => CatalogoDelPlan.Listado,
            (false, true) => CatalogoDelPlan.Conteo,
            _ => null,
        };
    }
}
