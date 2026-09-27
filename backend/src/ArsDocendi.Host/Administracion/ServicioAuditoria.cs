using ArsDocendi.Shared.Aplicacion;
using Microsoft.Extensions.Logging;

namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Validación y orquestación del feed unificado de auditoría —
/// <c>audit.change_log</c> más el rastro de administración del asistente
/// (sistema-seccion-unificada, design.md D2/D3/D8, tarea 2.8).
/// </summary>
/// <remarks>
/// Implementa design.md D2 al pie de la letra: <c>B</c> —el rastro del
/// asistente, ya acotado a 2000 filas por su propio cupo (D1)— se trae
/// SIEMPRE completo y filtrado en memoria (nunca en ventanas incrementales;
/// el cupo es lo que lo mantiene barato), y sólo <c>A</c>
/// (<c>audit.change_log</c>, sin cupo y del tamaño de toda la historia del
/// sistema) pide la ventana <c>a0</c>-acotada a la base. La fusión de las dos
/// listas ya acotadas y ya ordenadas es un merge de dos vías corriente
/// (<see cref="FusionDeFuentesAuditoria"/>), no una segunda derivación de
/// fórmula de posición.
/// </remarks>
public sealed class ServicioAuditoria(
    IRepositorioAuditoria repositorio,
    FuenteAuditoriaAsistente fuenteAsistente,
    ILogger<ServicioAuditoria> log)
{
    private const int TamanoPaginaPredeterminado = 50;
    private const int TamanoPaginaMaximo = 100;
    private static readonly HashSet<string> Acciones = ["INSERT", "UPDATE", "DELETE"];

    public async Task<PaginaAuditoriaDto> ListarAsync(ConsultaAuditoriaDto filtros, CancellationToken ct)
    {
        Validar(filtros);
        var f = Normalizar(filtros);

        var incluirAsistente = f.Modulo is null || f.Modulo == "asistente";
        var incluirCambios = f.Modulo != "asistente";

        if (!incluirAsistente)
        {
            // modulo selecciona un schema de change_log: el asistente ni se
            // consulta (design.md D2, "cuando un filtro de módulo selecciona
            // una fuente, la otra no se consulta").
            return await ListarSoloCambiosAsync(f, ct);
        }

        var (eventosAsistente, truncado, fallo) = await ListarAsistenteAsync(f, ct);

        if (!incluirCambios)
        {
            // modulo=asistente: la única fuente es B, paginada en memoria.
            var pagina = eventosAsistente
                .Skip((f.Pagina - 1) * f.TamanoPagina)
                .Take(f.TamanoPagina)
                .ToArray();
            var parcialSoloAsistente = fallo || truncado;
            return new PaginaAuditoriaDto(
                pagina, f.Pagina, f.TamanoPagina, eventosAsistente.Count,
                parcialSoloAsistente,
                parcialSoloAsistente ? [FusionDeFuentesAuditoria.OrigenAsistente] : []);
        }

        if (fallo || truncado)
        {
            // design.md D3: la fuente del asistente cuenta como nB = 0, la
            // página se sirve enteramente desde change_log.
            var soloCambios = await ListarSoloCambiosAsync(f, ct);
            return soloCambios with
            {
                Parcial = true,
                FuentesNoDisponibles = [FusionDeFuentesAuditoria.OrigenAsistente],
            };
        }

        return await ListarFusionadoAsync(f, eventosAsistente, ct);
    }

    private async Task<(IReadOnlyList<EventoAuditoriaDto> Eventos, bool Truncado, bool Fallo)> ListarAsistenteAsync(
        ConsultaAuditoriaDto f, CancellationToken ct)
    {
        try
        {
            var filtrosAsistente = new FiltrosAsistente(
                f.Accion, f.Tabla, f.RowPk,
                f.Q is { Length: > 0 } q ? EtiquetasAuditoria.Normalizar(q) : null);
            var (eventos, truncado) = await fuenteAsistente.ListarAsync(f.Desde, f.Hasta, filtrosAsistente, ct);

            if (truncado)
            {
                log.LogWarning(
                    "El rastro de administración del asistente alcanzó su cupo de lectura (Truncado=true).");
            }

            return (eventos, truncado, Fallo: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(
                ex, "El feed de auditoría no pudo leer la fuente {Origen}.", FusionDeFuentesAuditoria.OrigenAsistente);
            return ([], Truncado: false, Fallo: true);
        }
    }

    private async Task<PaginaAuditoriaDto> ListarSoloCambiosAsync(ConsultaAuditoriaDto f, CancellationToken ct)
    {
        var salto = (f.Pagina - 1) * f.TamanoPagina;
        var pagina = await repositorio.ListarAsync(f, salto, f.TamanoPagina, ct);
        var dtos = await MapearPaginaAsync(pagina.Registros, ct);
        return new PaginaAuditoriaDto(dtos, f.Pagina, f.TamanoPagina, pagina.Total, false, []);
    }

    /// <summary>El merge de las dos fuentes (design.md D2), con B ya completo y filtrado.</summary>
    private async Task<PaginaAuditoriaDto> ListarFusionadoAsync(
        ConsultaAuditoriaDto f, IReadOnlyList<EventoAuditoriaDto> eventosB, CancellationToken ct)
    {
        var o = (f.Pagina - 1) * f.TamanoPagina;
        var s = f.TamanoPagina;
        var nB = eventosB.Count;
        var bPrima = eventosB.Take(Math.Min(nB, o + s)).ToArray();
        var a0 = Math.Max(0, o - bPrima.Length);

        var ventanaA = await repositorio.ListarAsync(f, a0, s + bPrima.Length, ct);
        var dtosA = await MapearPaginaAsync(ventanaA.Registros, ct);

        var marcasA = dtosA.Select(MarcaDe).ToArray();
        var marcasB = bPrima.Select(MarcaDe).ToArray();
        var fusionLocal = FusionDeFuentesAuditoria.Fusionar(marcasA, marcasB);

        var indice = dtosA.Concat(bPrima).ToDictionary(dto => (dto.Origen, dto.Id));
        var ventana = fusionLocal
            .Skip(o - a0)
            .Take(s)
            .Select(marca => indice[(marca.Origen, $"{marca.Origen}-{marca.Id}")])
            .ToArray();

        return new PaginaAuditoriaDto(ventana, f.Pagina, f.TamanoPagina, ventanaA.Total + nB, false, []);
    }

    private static FusionDeFuentesAuditoria.Marca MarcaDe(EventoAuditoriaDto dto) =>
        new(dto.Origen, dto.CambiadoEn, long.Parse(dto.Id.AsSpan(dto.Origen.Length + 1)));

    private static ConsultaAuditoriaDto Normalizar(ConsultaAuditoriaDto filtros) => filtros with
    {
        Accion = filtros.Accion?.Trim().ToUpperInvariant(),
        Modulo = filtros.Modulo?.Trim().ToLowerInvariant(),
        Tabla = filtros.Tabla?.Trim().ToLowerInvariant(),
        RowPk = filtros.RowPk?.Trim(),
        Q = filtros.Q?.Trim(),
        TamanoPagina = filtros.TamanoPagina == 0 ? TamanoPaginaPredeterminado : filtros.TamanoPagina,
    };

    private static void Validar(ConsultaAuditoriaDto filtros)
    {
        var errores = new Dictionary<string, string[]>();
        if (filtros.Desde is { } desde && filtros.Hasta is { } hasta && desde > hasta)
            errores["hasta"] = ["Debe ser igual o posterior a desde."];
        if (filtros.Pagina < 1)
            errores["pagina"] = ["Debe ser un entero positivo."];
        if (filtros.TamanoPagina is < 0 or > TamanoPaginaMaximo)
            errores["tamanoPagina"] = [$"Debe estar entre 1 y {TamanoPaginaMaximo}."];
        var tamanoEfectivo = filtros.TamanoPagina == 0 ? TamanoPaginaPredeterminado : filtros.TamanoPagina;
        if ((long)(filtros.Pagina - 1) * tamanoEfectivo > int.MaxValue)
            errores["pagina"] = ["La página solicitada excede el límite consultable."];
        if (filtros.Accion is { Length: > 0 } accion && !Acciones.Contains(accion.Trim().ToUpperInvariant()))
            errores["accion"] = ["La acción debe ser INSERT, UPDATE o DELETE."];
        if (filtros.Modulo?.Trim().Length > 63)
            errores["modulo"] = ["No puede superar 63 caracteres."];
        if (filtros.Tabla?.Trim().Length > 127)
            errores["tabla"] = ["No puede superar 127 caracteres."];
        if (filtros.RowPk?.Trim().Length > 200)
            errores["rowPk"] = ["No puede superar 200 caracteres."];
        if (filtros.Q?.Trim().Length > 100)
            errores["q"] = ["No puede superar 100 caracteres."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "Revisá los filtros de auditoría.",
                errores);
        }
    }

    /// <summary>
    /// Mapea una página completa: PRIMERO recolecta los identificadores de
    /// sujeto humano de TODA la página y resuelve sus nombres en, como máximo,
    /// dos consultas batched (una de sujeto, una de rol) — nunca una por fila
    /// (design.md D5, «Humanized identity events», tarea 2.9).
    /// </summary>
    private async Task<EventoAuditoriaDto[]> MapearPaginaAsync(
        IReadOnlyList<RegistroAuditoria> pagina, CancellationToken ct)
    {
        var (idsDeUsuarios, idsDePersonas, idsDeRoles) = IdentidadDeAuditoria.RecolectarIdentificadores(pagina);
        var nombresSujeto = await repositorio.ResolverNombresDeSujetosAsync(idsDeUsuarios, idsDePersonas, ct);
        var nombresRoles = await repositorio.ResolverNombresDeRolesAsync(idsDeRoles, ct);
        return [.. pagina.Select(evento => Mapear(evento, nombresSujeto, nombresRoles))];
    }

    private static EventoAuditoriaDto Mapear(
        RegistroAuditoria evento,
        IReadOnlyDictionary<Guid, NombreResuelto> nombresSujeto,
        IReadOnlyDictionary<Guid, string> nombresRoles)
    {
        var registro = evento.Registro;
        using var anterior = MapeadorEventoAuditoria.Parsear(registro.FilaAnterior);
        using var nueva = MapeadorEventoAuditoria.Parsear(registro.FilaNueva);
        var columnas = registro.ColumnasCambiadas?.Distinct(StringComparer.Ordinal).ToArray()
            ?? MapeadorEventoAuditoria.ObtenerClaves(anterior, nueva);
        var cambios = columnas
            .Order(StringComparer.Ordinal)
            // «Fecha de baja: — → —» en un ALTA es ruido (sistema-seccion-unificada):
            // se evalúa sobre el JSON crudo, antes de enmascarar (ver EsAmbosAusentes).
            .Where(campo => !MapeadorEventoAuditoria.EsAmbosAusentes(anterior, nueva, campo))
            .Select(campo => MapeadorEventoAuditoria.MapearCambio(campo, anterior, nueva))
            .ToArray();
        var accionEtiqueta = MapeadorEventoAuditoria.EtiquetaDeAccion(registro.Accion);
        var modulo = MapeadorEventoAuditoria.ResolverModulo(registro.NombreSchema);
        var objetoGenerico = MapeadorEventoAuditoria.ResolverObjeto(registro.NombreSchema, registro.NombreTabla);
        var (objeto, resumenEspecial) = IdentidadDeAuditoria.Resolver(
            registro, columnas, objetoGenerico, nombresSujeto, nombresRoles);
        var resumen = resumenEspecial ?? MapeadorEventoAuditoria.ResumenDeCambio(
            registro.Accion, accionEtiqueta, objeto, objetoGenerico, registro.ClaveFila, cambios);
        var (actor, tipoActor) = MapeadorEventoAuditoria.ResolverActor(
            registro.CambiadoPor, registro.RequestId,
            evento.NombreCuenta, evento.NombrePersona, evento.ApellidoPersona);

        return new EventoAuditoriaDto(
            $"{FusionDeFuentesAuditoria.OrigenCambios}-{registro.Id}",
            registro.NombreSchema,
            registro.NombreTabla,
            registro.ClaveFila,
            registro.Accion,
            registro.CambiadoEn,
            registro.CambiadoPor,
            registro.RequestId,
            columnas.Order(StringComparer.Ordinal).ToArray(),
            cambios,
            actor,
            accionEtiqueta,
            modulo,
            objeto,
            resumen,
            tipoActor,
            FusionDeFuentesAuditoria.OrigenCambios);
    }
}
