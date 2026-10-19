using System.Text.RegularExpressions;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Api;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure.Guarani;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

/// <summary>
/// Consulta del snapshot de comisiones y su importación desde Guaraní en tres fases:
/// reservar el período, leer/agrupar/vincular fuera de transacción y reemplazar en una
/// sola transacción. Una falla en cualquier fase deja intactas las comisiones previas.
/// </summary>
public sealed partial class ServicioComisiones(
    RepositorioComisiones repositorio,
    ClienteGuarani guarani,
    IConsultasIdentity identity,
    ICurrentUser usuarioActual,
    IOptions<OpcionesGuarani> opciones,
    ILogger<ServicioComisiones> logger)
{
    public async Task<IReadOnlyList<PeriodoImportadoDto>> ListarPeriodosAsync(CancellationToken ct)
    {
        var periodos = await repositorio.ListarPeriodosImportadosAsync(ct);
        var usuarioIds = periodos
            .SelectMany(p => new[] { p.UltimaExitosa?.UsuarioId, p.UltimoIntento?.UsuarioId })
            .OfType<Guid>().Distinct().ToArray();
        var nombres = usuarioIds.Length == 0
            ? new Dictionary<Guid, string>()
            : (await identity.ObtenerUsuariosAsync(usuarioIds, ct)).ToDictionary(u => u.Id, u => u.NombreParaMostrar);

        ImportacionResumenDto? Resumen(ImportacionComisiones? i) => i is null ? null : new(
            i.FinalizadaEn ?? i.IniciadaEn,
            i.Estado,
            i.Causa,
            i.UsuarioId is { } id ? nombres.GetValueOrDefault(id) : null);

        return periodos.Select(p => new PeriodoImportadoDto(
            p.AnioAcademico, p.PeriodoLectivoId, p.PeriodoLectivoNombre,
            Resumen(p.UltimaExitosa), Resumen(p.UltimoIntento))).ToArray();
    }

    public async Task<IReadOnlyList<ComisionDto>> ListarAsync(string periodoLectivoId, CancellationToken ct)
    {
        ValidarPeriodoId(periodoLectivoId);
        return (await repositorio.ListarComisionesAsync(periodoLectivoId, ct)).Select(Mapear).ToArray();
    }

    public async Task<IReadOnlyList<PeriodoLectivoGuaraniDto>> ListarPeriodosGuaraniAsync(CancellationToken ct)
    {
        ExigirConfiguracion();
        var periodos = await LlamarAsync(() => guarani.ListarPeriodosLectivosAsync(ct));
        return periodos
            .OrderByDescending(p => p.Anio).ThenBy(p => p.Nombre, StringComparer.Ordinal)
            .Select(p => new PeriodoLectivoGuaraniDto(p.Id, p.Nombre, p.Anio))
            .ToArray();
    }

    public async Task<ResultadoImportacionDto> ImportarAsync(ImportarComisionesDto datos, CancellationToken ct)
    {
        ExigirConfiguracion();
        ValidarPeriodoId(datos.PeriodoLectivoId);
        var periodos = await LlamarAsync(() => guarani.ListarPeriodosLectivosAsync(ct));
        var periodo = periodos.FirstOrDefault(p => p.Id == datos.PeriodoLectivoId)
            ?? throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Guaraní no informa ese período lectivo.",
                new Dictionary<string, string[]> { ["periodoLectivoId"] = ["Período lectivo inexistente."] });

        var caducidad = TimeSpan.FromSeconds(2 * Math.Max(1, opciones.Value.TimeoutSegundos));
        var importacion = await repositorio.ReservarAsync(
            periodo.Anio, periodo.Id, periodo.Nombre, UsuarioActualId(), caducidad, ct);
        try
        {
            var filas = await guarani.ListarComisionesAsync(periodo.Id, ct);
            var comisiones = AgrupadorComisionesGuarani.Agrupar(filas);
            var vinculadas = await VincularAsync(comisiones, importacion, ct);

            if (!await repositorio.ReemplazarAsync(
                    importacion, vinculadas.Comisiones, vinculadas.DocentesSinVincular, vinculadas.CarrerasSinVincular, ct))
            {
                throw new ExcepcionGuarani(CausasImportacion.RespuestaInvalida,
                    "Guaraní devolvió comisiones que ya pertenecen a otro período lectivo.");
            }
            logger.LogInformation(
                "Importadas {Cantidad} comisiones del período {Periodo}; {DocentesSinVincular} docentes y {CarrerasSinVincular} carreras sin vincular",
                vinculadas.Comisiones.Count, periodo.Id, vinculadas.DocentesSinVincular, vinculadas.CarrerasSinVincular);
            return new ResultadoImportacionDto(
                importacion.Id, periodo.Anio, periodo.Id, periodo.Nombre,
                vinculadas.Comisiones.Count, vinculadas.DocentesSinVincular, vinculadas.CarrerasSinVincular);
        }
        catch (ExcepcionGuarani error)
        {
            await repositorio.FallarAsync(importacion.Id, error.Causa, CancellationToken.None);
            throw Traducir(error);
        }
        catch
        {
            await repositorio.FallarAsync(importacion.Id, CausasImportacion.Interrumpida, CancellationToken.None);
            throw;
        }
    }

    private async Task<(IReadOnlyList<ComisionGuarani> Comisiones, int DocentesSinVincular, int CarrerasSinVincular)>
        VincularAsync(IReadOnlyList<ComisionImportada> comisiones, ImportacionComisiones importacion, CancellationToken ct)
    {
        var documentos = comisiones.SelectMany(c => c.Docentes).Select(d => d.Documento).OfType<string>().ToArray();
        var personas = await identity.BuscarPersonasPorDocumentosAsync(documentos, ct);
        var carreras = (await identity.ListarCarrerasAsync(ct))
            .GroupBy(c => c.Codigo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
        var materias = (await identity.ListarMateriasAsync(ct))
            .GroupBy(m => m.Codigo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
        var ahora = DateTimeOffset.UtcNow;

        var entidades = comisiones.Select(c => new ComisionGuarani
        {
            ComisionGuaraniId = c.Id,
            ImportacionId = importacion.Id,
            AnioAcademico = importacion.AnioAcademico,
            PeriodoLectivoGuaraniId = importacion.PeriodoLectivoGuaraniId,
            PeriodoLectivoNombre = importacion.PeriodoLectivoNombre,
            Nombre = c.Nombre,
            MateriaCodigo = c.MateriaCodigo,
            MateriaNombre = c.MateriaNombre,
            MateriaId = materias.TryGetValue(c.MateriaCodigo, out var materiaId) ? materiaId : null,
            ModalidadCodigo = c.ModalidadCodigo,
            Modalidad = c.Modalidad,
            Ubicacion = c.Ubicacion,
            CreadoEn = ahora,
            Carreras = c.Carreras.Select(k => new ComisionCarrera
            {
                ComisionGuaraniId = c.Id,
                CarreraCodigo = k.Codigo,
                CarreraNombre = k.Nombre,
                CarreraId = carreras.TryGetValue(k.Codigo, out var carreraId) ? carreraId : null,
            }).ToList(),
            Docentes = c.Docentes.Select((d, i) => new ComisionDocente
            {
                ComisionGuaraniId = c.Id,
                Orden = (short)(i + 1),
                Nombre = d.Nombre,
                Responsabilidad = d.Responsabilidad,
                PersonaId = d.Documento is not null && personas.TryGetValue(d.Documento, out var personaId) ? personaId : null,
            }).ToList(),
            Horarios = c.Horarios.Select(h => new ComisionHorario
            {
                ComisionGuaraniId = c.Id,
                AsignacionGuaraniId = h.AsignacionId,
                Dia = h.Dia,
                Desde = h.Desde,
                Hasta = h.Hasta,
                Aula = h.Aula,
            }).ToList(),
        }).ToArray();

        return (
            entidades,
            entidades.Sum(c => c.Docentes.Count(d => d.PersonaId is null)),
            entidades.Sum(c => c.Carreras.Count(k => k.CarreraId is null)));
    }

    private static ComisionDto Mapear(ComisionGuarani c) => new(
        c.ComisionGuaraniId,
        c.Nombre,
        new MateriaComisionDto(c.MateriaCodigo, c.MateriaNombre, c.MateriaId),
        c.Carreras.OrderBy(k => k.CarreraCodigo, StringComparer.Ordinal)
            .Select(k => new CarreraComisionDto(k.CarreraCodigo, k.CarreraNombre, k.CarreraId)).ToArray(),
        c.Docentes.OrderBy(d => d.Orden)
            .Select(d => new DocenteComisionDto(d.Nombre, d.Responsabilidad, d.PersonaId)).ToArray(),
        c.Horarios.OrderBy(h => h.Dia).ThenBy(h => h.Desde)
            .Select(h => new HorarioComisionDto(
                h.AsignacionGuaraniId, h.Dia, h.Desde.ToString("HH:mm"), h.Hasta.ToString("HH:mm"), h.Aula)).ToArray(),
        c.ModalidadCodigo,
        c.Modalidad,
        c.Ubicacion);

    private void ExigirConfiguracion()
    {
        if (!guarani.EstaConfigurado)
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ServicioExternoNoConfigurado,
                "guarani-no-configurado",
                "La integración con Guaraní no está configurada en este ambiente.");
    }

    private static async Task<T> LlamarAsync<T>(Func<Task<T>> llamada)
    {
        try
        {
            return await llamada();
        }
        catch (ExcepcionGuarani error)
        {
            throw Traducir(error);
        }
    }

    private static ExcepcionAplicacion Traducir(ExcepcionGuarani error) => error.Causa switch
    {
        CausasImportacion.Timeout => new(TipoErrorAplicacion.ServicioExternoSinRespuesta,
            "guarani-timeout", "Guaraní no respondió a tiempo. Probá de nuevo más tarde."),
        CausasImportacion.Autenticacion => new(TipoErrorAplicacion.ServicioExternoFallido,
            "guarani-autenticacion", "Guaraní rechazó las credenciales de la integración."),
        CausasImportacion.SinComisiones => new(TipoErrorAplicacion.ReglaDeNegocio,
            "guarani-sin-comisiones", "Guaraní no informó comisiones para el período; se conservan las anteriores."),
        CausasImportacion.RespuestaInvalida => new(TipoErrorAplicacion.ServicioExternoFallido,
            "guarani-respuesta-invalida", "Guaraní devolvió datos inconsistentes; se conservan las comisiones anteriores."),
        _ => new(TipoErrorAplicacion.ServicioExternoFallido,
            "guarani-no-disponible", "No se pudo obtener la información de Guaraní."),
    };

    private Guid? UsuarioActualId() => Guid.TryParse(usuarioActual.UserId, out var id) ? id : null;

    private static void ValidarPeriodoId(string? periodoLectivoId)
    {
        if (periodoLectivoId is null || !FormatoPeriodo().IsMatch(periodoLectivoId))
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Período lectivo inválido.",
                new Dictionary<string, string[]> { ["periodoLectivoId"] = ["Formato inválido."] });
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex FormatoPeriodo();
}
