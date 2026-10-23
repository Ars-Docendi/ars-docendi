using System.Text;

namespace Modules.Asistente.Application;

/// <summary>
/// Le pide al modelo una muestra del plan (D5 de <c>asistente-plan-compilado</c>).
/// </summary>
/// <remarks>
/// Una llamada por muestra, con salida estructurada y sin razonamiento: el plan es
/// una sola decisión corta, y pensar antes de una llamada a herramienta empeora la
/// abstención de los modelos chicos. El prefijo es estable byte a byte para que el
/// servidor lo reutilice entre muestras y entre turnos.
/// </remarks>
internal sealed class GeneradorDePlan(IProveedorDeModelo proveedor)
{
    /// <summary>Un plan es corto: con este techo, uno que no cierra es una muestra inválida.</summary>
    private const int MaximoDeTokensDelPlan = 400;

    internal static string Prefijo { get; } = ArmarPrefijo();

    /// <summary>La muestra interpretada, o nula si la salida no es un plan.</summary>
    public async Task<PlanDeConsulta?> GenerarAsync(string pregunta, decimal temperatura, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        var respuesta = await proveedor.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = Prefijo,
                Mensaje = $"Pregunta: {pregunta.Trim()}",
                Temperatura = temperatura,
                Esfuerzo = EsfuerzoDelModelo.Minimo,
                MaximoDeTokens = MaximoDeTokensDelPlan,
                EsquemaDeSalidaJson = CatalogoDelPlan.EsquemaJson,
            },
            ct);

        return respuesta.SeQuedoSinTokens ? null : PlanDeConsulta.Interpretar(respuesta.Texto);
    }

    private static string ArmarPrefijo()
    {
        var prefijo = new StringBuilder(
            """
            Traducí una pregunta sobre el plantel docente de la UNLaM a un plan JSON. No escribas SQL
            ni respondas la pregunta: solo el plan.

            La población es siempre la de las personas con al menos una designación vigente.
            - "medida": "conteo" (cuántos), "porcentaje" (qué porcentaje) o "listado" (quiénes).
            - "filtros": las condiciones que definen a quiénes se cuenta. En un porcentaje, son el
              denominador ("de los titulares" → filtro cargo = titular).
            - "condiciones": solo en un porcentaje, lo que tienen que cumplir para entrar en el
              numerador. En conteo y listado va vacío.
            - Cada condición es {"campo", "operador", "valor"}; el valor va siempre como texto, y
              los números en cifras ("2", no "dos").
            - Usá solo lo que la pregunta dice. No agregues condiciones ni cambies nombres: copiá
              el nombre de la carrera o de la materia como aparece en la pregunta.
            - "más de N" es ">", "al menos N" o "N o más" es ">=", "menos de N" es "<",
              "como máximo N" es "<=". "No" o "sin" delante de un cargo, carrera o materia es "!=".
            - Si la pregunta pide algo que estos campos no expresan (horas, pedidos, períodos,
              agrupar por algo, datos personales), respondé "expresable": false con listas vacías.

            Campos:

            """);

        foreach (var campo in CatalogoDelPlan.Campos.Values)
        {
            prefijo.Append($"- {campo.Nombre} ({string.Join(' ', campo.Operadores)}): {campo.Descripcion}\n");
        }

        prefijo.Append("\nCargos:\n");
        foreach (var cargo in CatalogoDelPlan.Cargos)
        {
            prefijo.Append($"- {cargo.Codigo}: {cargo.Etiqueta}\n");
        }

        prefijo.Append(
            """

            Ejemplos:

            Pregunta: ¿Cuántos adjuntos hay en Ingeniería Electrónica?
            {"expresable":true,"medida":"conteo","filtros":[{"campo":"cargo","operador":"=","valor":"adjunto"},{"campo":"carrera","operador":"=","valor":"Ingeniería Electrónica"}],"condiciones":[]}

            Pregunta: ¿Qué porcentaje de los asociados dicta en al menos dos materias?
            {"expresable":true,"medida":"porcentaje","filtros":[{"campo":"cargo","operador":"=","valor":"asociado"}],"condiciones":[{"campo":"cantidad_materias","operador":">=","valor":"2"}]}

            Pregunta: ¿Quiénes son los docentes con categoría 5 que no son jefes de trabajos prácticos?
            {"expresable":true,"medida":"listado","filtros":[{"campo":"dedicacion","operador":"=","valor":"5"},{"campo":"cargo","operador":"!=","valor":"jtp"}],"condiciones":[]}

            Pregunta: ¿Cuántas horas suman los adjuntos?
            {"expresable":false,"medida":"conteo","filtros":[],"condiciones":[]}
            """);

        return prefijo.ToString();
    }
}
