using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using Modules.Asistente.Application;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// Corre el eje social y meta.
/// </summary>
/// <remarks>
/// Mide dos cosas que ningún otro eje ve: qué proporción del tráfico trivial captura
/// el carril de cero tokens, y qué proporción de preguntas legítimas se come de más.
///
/// <b>El assert de costo cero ES la métrica</b>: si el saludo costó tokens, el
/// enrutador no lo capturó, por más que la respuesta haya sido perfecta.
/// </remarks>
public sealed class RunnerSocial(
    Func<CapaConversacional> capaPorItem,
    IResolutorDeActores actores,
    MedidorDeConsumo medidor)
{
    /// <summary>Código de salida cuando la corrida se descarta por no tener proveedor.</summary>
    public const int CodigoDeCorridaSinProveedor = 3;

    /// <summary>Corre el eje.</summary>
    /// <remarks>
    /// No hay preflight acá, y no es un olvido: el preflight verifica que el
    /// proveedor responda, y este eje tiene una guarda <b>más fuerte y específica</b>
    /// al final. Ver <see cref="TodoACero"/>.
    /// </remarks>
    public async Task<ResultadoDeCorrida> CorrerAsync(
        DatasetSocial dataset, SelloDeIdentidad sello, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var resultados = new List<ResultadoDeItem>(dataset.Items.Count);
        var tokensDeLaCorrida = 0;

        foreach (var item in dataset.Items)
        {
            medidor.Reiniciar();

            var evaluado = await EvaluarAsync(item, ct);
            tokensDeLaCorrida += medidor.TokensDeEntrada;

            resultados.Add(evaluado);
        }

        if (TodoACero(tokensDeLaCorrida))
        {
            // LA TRAMPA DE ESTE EJE, Y SE AGRAVA RESPECTO DEL DE CAPACIDAD.
            //
            // Allá, sin crédito, los ítems infactibles pasan espuriamente porque el
            // turno falla y devuelve abstención. Acá el assert es «consumió cero
            // tokens», y un proveedor caído consume cero tokens en TODOS los ítems:
            // la corrida entera daría verde perfecto.
            //
            // En una corrida sana, los ítems negativos —preguntas legítimas que el
            // enrutador NO debe capturar— tienen que haber llegado al modelo. Que
            // ninguno lo haya hecho significa que no hubo modelo.
            return new ResultadoDeCorrida(
                CodigoDeCorridaSinProveedor,
                null,
                "Ningún turno consumió tokens de entrada. En una corrida sana los ítems "
                + "negativos llegan al modelo, así que esto significa que no hay proveedor: "
                + "un reporte de este eje con el proveedor caído daría verde perfecto.");
        }

        return new ResultadoDeCorrida(
            0, new Reporte("social y meta", sello, resultados, dataset.ConteoPorClase()), null);
    }

    /// <summary>Si la corrida no consumió nada, que es la firma de no tener proveedor.</summary>
    public static bool TodoACero(int tokensDeLaCorrida) => tokensDeLaCorrida == 0;

    private async Task<ResultadoDeItem> EvaluarAsync(ItemSocial item, CancellationToken ct)
    {
        var actor = actores.Resolver(item.Actor);

        ResultadoDelTurno turno;
        try
        {
            // Una capa nueva por ítem: el techo de llamadas es por turno, y una
            // instancia compartida lo volvería un techo de la corrida.
            turno = await capaPorItem().ResponderAsync(actor, null, item.Pregunta, ct);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return new ResultadoDeItem(
                item.Id, item.Clase, DesenlaceDeItem.Fallo,
                $"El turno lanzó `{excepcion.GetType().Name}`.");
        }

        return item.Clase switch
        {
            ClaseSocial.Social => EvaluarSocial(item, turno),
            ClaseSocial.NoContestable => EvaluarNoContestable(item, turno),
            ClaseSocial.Negativo => EvaluarNegativo(item),
            _ => throw new InvalidOperationException($"Clase social desconocida: '{item.Clase}'."),
        };
    }

    private ResultadoDeItem EvaluarSocial(ItemSocial item, ResultadoDelTurno turno)
    {
        // Se miden TOKENS y no llamadas. Cero llamadas implica cero tokens, pero no
        // al revés, y el requisito habla de tokens de entrada.
        var costoCero = medidor.TokensDeEntrada == 0;
        var sugirio = turno.Sugerencias is { Count: > 0 };

        if (!costoCero)
        {
            return new ResultadoDeItem(
                item.Id, item.Clase, DesenlaceDeItem.TraduccionIncorrecta,
                $"El enrutador no lo capturó: costó {medidor.TokensDeEntrada} tokens de entrada.");
        }

        if (turno.Estado != EstadoDelTurno.Respondida)
        {
            return new ResultadoDeItem(
                item.Id, item.Clase, DesenlaceDeItem.TraduccionIncorrecta,
                "Resolvió a costo cero pero no respondió.");
        }

        // La meta-pregunta trae ejemplos ejecutables; el saludo y el agradecimiento,
        // no. Exigir sugerencias en todos convertiría una decisión de redacción en un
        // fallo, así que solo se informa.
        return new ResultadoDeItem(
            item.Id, item.Clase, DesenlaceDeItem.TraduccionCorrecta,
            sugirio ? "Cero tokens, con sugerencias." : "Cero tokens.");
    }

    private static ResultadoDeItem EvaluarNoContestable(ItemSocial item, ResultadoDelTurno turno)
    {
        // Resolvió «no contestable» sin que el modelo decidiera nada: no se acredita.
        if (turno.Categoria == GeneracionDeSql.CategoriaTruncada)
        {
            return ResultadoDeItem.PorGeneracionTruncada(item.Id, item.Clase);
        }

        var seAbstuvo = turno.Estado is EstadoDelTurno.NoContestable
            or EstadoDelTurno.NecesitaAclaracion;

        // ABSTENERSE NO ALCANZA. Un «no puedo» sin salida deja al usuario sin nada
        // que hacer, y el rechazo cooperativo existe justamente para eso.
        var sugirio = turno.Sugerencias is { Count: > 0 };

        if (seAbstuvo && sugirio)
        {
            return new ResultadoDeItem(
                item.Id, item.Clase, DesenlaceDeItem.AbstencionCorrecta,
                "Se abstuvo y sugirió una salida.");
        }

        return new ResultadoDeItem(
            item.Id,
            item.Clase,
            seAbstuvo ? DesenlaceDeItem.TraduccionIncorrecta : DesenlaceDeItem.IntentoSobreLoInfactible,
            seAbstuvo ? "Se abstuvo sin sugerir nada." : "Respondió lo que no podía responder.");
    }

    private ResultadoDeItem EvaluarNegativo(ItemSocial item)
    {
        // Un negativo es una pregunta LEGÍTIMA: capturarla es el modo de falla que
        // este eje existe para detectar. Se mide por llamadas y no por tokens porque
        // lo que importa es si salió del enrutador, no cuánto costó.
        var llegoAlModelo = medidor.Llamadas > 0;

        return new ResultadoDeItem(
            item.Id,
            item.Clase,
            llegoAlModelo ? DesenlaceDeItem.TraduccionCorrecta : DesenlaceDeItem.IntentoSobreLoInfactible,
            llegoAlModelo
                ? "El enrutador la dejó pasar, como corresponde."
                : "El enrutador capturó una pregunta legítima.");
    }
}
