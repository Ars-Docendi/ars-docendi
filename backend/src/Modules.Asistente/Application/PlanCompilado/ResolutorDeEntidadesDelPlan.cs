namespace Modules.Asistente.Application;

/// <summary>Una carrera tal como la lee el plan.</summary>
internal sealed record CarreraDelPlan(Guid Id, string Nombre, string Codigo);

/// <summary>Los ids de una entidad del plan y el nombre con que se muestra.</summary>
internal sealed record EntidadResuelta(IReadOnlyList<Guid> Ids, string Nombre);

/// <summary>Una lectura posible de una entidad ambigua.</summary>
/// <param name="Carrera">
/// La carrera que la desambigua, cuando el nombre es el mismo en varias carreras
/// («Análisis Matemático»). Nula si lo que cambia es el nombre.
/// </param>
internal sealed record CandidatoDeEntidad(string Nombre, string? Carrera);

/// <summary>
/// Las entidades del plan ya resueltas, o la primera que no resolvió y por qué.
/// </summary>
/// <param name="Fallida">Nula si todas resolvieron.</param>
/// <param name="Candidatos">Vacío si la fallida no existe; dos o más si es ambigua.</param>
internal sealed record ResolucionDelPlan(
    IReadOnlyDictionary<int, EntidadResuelta> Entidades,
    CondicionValidada? Fallida,
    IReadOnlyList<CandidatoDeEntidad> Candidatos)
{
    public bool Resolvio => Fallida is null;
}

/// <summary>
/// Resuelve las carreras y materias del plan dentro del alcance del actor (D7 de
/// <c>asistente-plan-compilado</c>).
/// </summary>
/// <remarks>
/// <b>Coincidencia exacta del nombre normalizado, o de sus palabras distintivas.</b>
/// Nada de distancia de edición: un nombre que no resuelve se le dice al usuario,
/// que es preferible a contar sobre la materia vecina.
///
/// <b>Un nombre compartido por varias carreras es ambiguo</b> —la misma política que
/// el detector de ambigüedad de la capa conversacional—, salvo que la misma lista
/// del plan nombre la carrera: «Análisis Matemático» sola se aclara; «Análisis
/// Matemático en Ingeniería Industrial» resuelve a una sola materia.
/// </remarks>
internal sealed class ResolutorDeEntidadesDelPlan(IEjecutorDeConsulta ejecutor, IBuscadorDeMenciones menciones)
{
    private const string ConsultaDeCarreras =
        "SELECT c.id, c.name, c.code FROM identity.carreras c WHERE c.is_active ORDER BY c.name";

    private IReadOnlyList<CarreraDelPlan>? _carreras;

    /// <summary>Las carreras activas. Se leen una vez por turno.</summary>
    public async Task<IReadOnlyList<CarreraDelPlan>> CarrerasAsync(Guid actor, CancellationToken ct)
    {
        if (_carreras is not null)
        {
            return _carreras;
        }

        var resultado = await ejecutor.EjecutarAsync(ConsultaDeCarreras, actor, conDatosPersonales: false, ct);

        _carreras = [.. resultado.Filas.Select(fila => new CarreraDelPlan(
            (Guid)fila[0]!, (string)fila[1]!, (string)fila[2]!))];

        return _carreras;
    }

    public async Task<ResolucionDelPlan> ResolverAsync(Guid actor, PlanValidado plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var entidades = new Dictionary<int, EntidadResuelta>();

        foreach (var lista in new[] { plan.Filtros, plan.Condiciones })
        {
            // Las carreras primero: una carrera afirmativa de la misma lista es lo que
            // desambigua una materia compartida.
            foreach (var condicion in lista.Where(c => c.Campo.Nombre == CatalogoDelPlan.Carrera))
            {
                var (resuelta, candidatos) = ResolverCarrera(condicion.Nombre!, await CarrerasAsync(actor, ct));
                if (resuelta is null)
                {
                    return new ResolucionDelPlan(entidades, condicion, candidatos);
                }

                entidades[condicion.Indice] = resuelta;
            }

            var carreraDeLaLista = lista
                .Where(c => c.Campo.Nombre == CatalogoDelPlan.Carrera && c.Operador == "=")
                .Select(c => entidades[c.Indice].Nombre)
                .FirstOrDefault();

            foreach (var condicion in lista.Where(c => c.Campo.Nombre == CatalogoDelPlan.Materia))
            {
                var (resuelta, candidatos) = await ResolverMateriaAsync(
                    actor, condicion.Nombre!, carreraDeLaLista, ct);
                if (resuelta is null)
                {
                    return new ResolucionDelPlan(entidades, condicion, candidatos);
                }

                entidades[condicion.Indice] = resuelta;
            }
        }

        return new ResolucionDelPlan(entidades, null, []);
    }

    private static (EntidadResuelta?, IReadOnlyList<CandidatoDeEntidad>) ResolverCarrera(
        string nombre, IReadOnlyList<CarreraDelPlan> carreras)
    {
        var exactas = carreras.Where(carrera =>
            TextoDeLaPregunta.Normalizar(carrera.Nombre) == nombre
            || TextoDeLaPregunta.Normalizar(carrera.Codigo) == nombre).ToList();

        var candidatas = exactas.Count > 0
            ? exactas
            : [.. carreras.Where(carrera =>
                ValidadorDePlan.EstaNombrada(nombre, new TextoDeLaPregunta(carrera.Nombre)))];

        return candidatas.Count switch
        {
            1 => (new EntidadResuelta([candidatas[0].Id], candidatas[0].Nombre), []),
            0 => (null, []),
            _ => (null, [.. candidatas.Select(carrera => new CandidatoDeEntidad(carrera.Nombre, null))]),
        };
    }

    private async Task<(EntidadResuelta?, IReadOnlyList<CandidatoDeEntidad>)> ResolverMateriaAsync(
        Guid actor, string nombre, string? carrera, CancellationToken ct)
    {
        // El buscador busca por prefijo de palabra: la palabra más larga del nombre
        // es la que menos resultados ajenos trae dentro de su tope.
        var termino = nombre.Split(' ').OrderByDescending(palabra => palabra.Length).First();
        var busqueda = await menciones.BuscarAsync(actor, TipoDeMencion.Materia, termino, ct);

        var exactas = busqueda.Resultados
            .Where(materia => TextoDeLaPregunta.Normalizar(materia.Nombre) == nombre)
            .ToList();

        var candidatas = exactas.Count > 0
            ? exactas
            : [.. busqueda.Resultados.Where(materia =>
                ValidadorDePlan.EstaNombrada(nombre, new TextoDeLaPregunta(materia.Nombre)))];

        if (carrera is not null)
        {
            candidatas = [.. candidatas.Where(materia => materia.Carrera == carrera)];
        }

        var nombres = candidatas.Select(materia => materia.Nombre).Distinct(StringComparer.Ordinal).ToList();

        if (nombres.Count > 1)
        {
            return (null, [.. nombres.Select(otro => new CandidatoDeEntidad(otro, null))]);
        }

        if (nombres.Count == 0)
        {
            return (null, []);
        }

        return candidatas.Count == 1
            ? (new EntidadResuelta([candidatas[0].Id], nombres[0]), [])
            : (null, [.. candidatas.Select(materia => new CandidatoDeEntidad(materia.Nombre, materia.Carrera))]);
    }
}
