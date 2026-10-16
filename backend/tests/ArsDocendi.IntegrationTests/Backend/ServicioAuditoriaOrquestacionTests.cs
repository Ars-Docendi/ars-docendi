using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente.Contracts;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// La orquestación de <see cref="ServicioAuditoria"/> sobre las dos fuentes
/// (sistema-seccion-unificada, design.md D2/D3/D8, tarea 2.8): fusión, skip
/// de fuente por <c>modulo</c>, y degradación parcial cuando el asistente falla.
/// </summary>
public sealed class ServicioAuditoriaOrquestacionTests
{
    private static readonly Guid Actor = Guid.Parse("a0000000-0000-4000-8000-000000000007");

    [Fact]
    public async Task Fusiona_las_dos_fuentes_en_orden_y_suma_el_total()
    {
        // Cinco eventos de change_log en segundos pares, tres del asistente en
        // impares — intercalados a propósito para que el merge tenga trabajo.
        var registros = Enumerable.Range(0, 5)
            .Select(i => RegistroEn(id: i + 1, segundos: (4 - i) * 2))
            .ToArray();
        var eventosAsistente = Enumerable.Range(0, 3)
            .Select(i => EventoAsistenteEn(id: i + 1, segundos: (2 - i) * 2 + 1))
            .ToArray();

        var repositorio = new RepositorioFalso(registros);
        var contrato = new ContratoFalso(eventosAsistente);
        var servicio = ServicioCon(repositorio, contrato);

        var pagina1 = await servicio.ListarAsync(new ConsultaAuditoriaDto(TamanoPagina: 4), CancellationToken.None);
        var pagina2 = await servicio.ListarAsync(
            new ConsultaAuditoriaDto(Pagina: 2, TamanoPagina: 4), CancellationToken.None);

        Assert.Equal(8, pagina1.Total);
        Assert.Equal(8, pagina2.Total);
        Assert.False(pagina1.Parcial);

        // El orden total es CambiadoEn DESC. change_log queda en 8,6,4,2,0s;
        // asistente en 5,3,1s — intercalados: 8,6,5,4,3,2,1,0.
        var idsEnOrden = pagina1.Elementos.Concat(pagina2.Elementos).Select(e => e.Id).ToArray();
        Assert.Equal(
            [
                "cambios-1", "cambios-2", "asistente-1", "cambios-3",
                "asistente-2", "cambios-4", "asistente-3", "cambios-5",
            ],
            idsEnOrden);

        // Cada evento aparece EXACTAMENTE una vez a través de las dos páginas.
        Assert.Equal(idsEnOrden.Length, idsEnOrden.Distinct().Count());
    }

    [Fact]
    public async Task Modulo_asistente_nunca_toca_change_log()
    {
        var repositorio = new RepositorioFalso([RegistroEn(1, 10)]);
        var contrato = new ContratoFalso([EventoAsistenteEn(1, 5)]);
        var servicio = ServicioCon(repositorio, contrato);

        var pagina = await servicio.ListarAsync(
            new ConsultaAuditoriaDto(Modulo: "asistente"), CancellationToken.None);

        Assert.False(repositorio.FueLlamado);
        Assert.True(contrato.FueLlamado);
        Assert.Single(pagina.Elementos);
        Assert.Equal("asistente-1", pagina.Elementos[0].Id);
    }

    [Fact]
    public async Task Modulo_portal_nunca_llama_al_contrato_del_asistente()
    {
        var repositorio = new RepositorioFalso([RegistroEn(1, 10, schema: "portal")]);
        var contrato = new ContratoFalso([EventoAsistenteEn(1, 5)]);
        var servicio = ServicioCon(repositorio, contrato);

        var pagina = await servicio.ListarAsync(
            new ConsultaAuditoriaDto(Modulo: "portal"), CancellationToken.None);

        Assert.False(contrato.FueLlamado);
        Assert.True(repositorio.FueLlamado);
        Assert.Single(pagina.Elementos);
        Assert.False(pagina.Parcial);
    }

    [Fact]
    public async Task Una_fuente_de_asistente_que_falla_sirve_solo_change_log_y_marca_parcial()
    {
        var repositorio = new RepositorioFalso([RegistroEn(1, 10), RegistroEn(2, 5)]);
        var contrato = new ContratoFalso(fallar: true);
        var servicio = ServicioCon(repositorio, contrato);

        var pagina = await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None);

        Assert.True(pagina.Parcial);
        Assert.Equal(["asistente"], pagina.FuentesNoDisponibles);
        Assert.Equal(2, pagina.Total);
        Assert.All(pagina.Elementos, e => Assert.Equal("cambios", e.Origen));
    }

    [Fact]
    public async Task Un_lote_truncado_del_asistente_tambien_degrada_a_parcial()
    {
        var repositorio = new RepositorioFalso([RegistroEn(1, 10)]);
        var contrato = new ContratoFalso([EventoAsistenteEn(1, 5)], truncado: true);
        var servicio = ServicioCon(repositorio, contrato);

        var pagina = await servicio.ListarAsync(new ConsultaAuditoriaDto(), CancellationToken.None);

        Assert.True(pagina.Parcial);
        Assert.Equal(["asistente"], pagina.FuentesNoDisponibles);
        Assert.Single(pagina.Elementos);
        Assert.Equal("cambios-1", pagina.Elementos[0].Id);
    }

    [Fact]
    public async Task Una_pagina_de_50_eventos_de_identidad_resuelve_nombres_en_una_sola_consulta()
    {
        // sistema-seccion-unificada, design.md D5, «Humanized identity events»
        // (tarea 2.9): la resolución de nombres de sujeto tiene que ser UNA
        // consulta batched por página, no una por fila.
        var registros = Enumerable.Range(0, 50)
            .Select(i => RegistroDePersonaEn(id: i + 1, segundos: i))
            .ToArray();
        var repositorio = new RepositorioFalso(registros);
        var contrato = new ContratoFalso();
        var servicio = ServicioCon(repositorio, contrato);

        var pagina = await servicio.ListarAsync(
            new ConsultaAuditoriaDto(TamanoPagina: 50), CancellationToken.None);

        Assert.Equal(50, pagina.Elementos.Count);
        Assert.Equal(1, repositorio.LlamadasAResolverSujetos);
    }

    // ------------------------------------------------------------------ apoyo

    private static ServicioAuditoria ServicioCon(IRepositorioAuditoria repositorio, ContratoFalso contrato)
    {
        var fuente = new FuenteAuditoriaAsistente(contrato, repositorio);
        return new ServicioAuditoria(repositorio, fuente, NullLogger<ServicioAuditoria>.Instance);
    }

    private static RegistroAuditoria RegistroEn(long id, int segundos, string schema = "identity") => new(
        new RegistroCambio
        {
            Id = id,
            NombreSchema = schema,
            NombreTabla = "personas",
            ClaveFila = $"fila-{id}",
            Accion = "UPDATE",
            CambiadoEn = DateTimeOffset.UnixEpoch.AddSeconds(segundos),
            CambiadoPor = Actor,
            RequestId = "req",
        },
        "Cuenta visible", null, null);

    private static RegistroAuditoria RegistroDePersonaEn(long id, int segundos) => new(
        new RegistroCambio
        {
            Id = id,
            NombreSchema = "identity",
            NombreTabla = "personas",
            ClaveFila = Guid.NewGuid().ToString(),
            Accion = "UPDATE",
            FilaAnterior = """{"telefono":"111"}""",
            FilaNueva = """{"telefono":"222"}""",
            ColumnasCambiadas = ["telefono"],
            CambiadoEn = DateTimeOffset.UnixEpoch.AddSeconds(segundos),
            CambiadoPor = Actor,
            RequestId = "req",
        },
        "Cuenta visible", null, null);

    private static EventoDeAdministracion EventoAsistenteEn(long id, int segundos) => new(
        id, Actor, DateTimeOffset.UnixEpoch.AddSeconds(segundos), "tope_organizacional", null, null,
        [new CampoDeAdministracion("tope_mensual_usd", "0", "1")]);

    private sealed class RepositorioFalso(IReadOnlyList<RegistroAuditoria> registros) : IRepositorioAuditoria
    {
        public bool FueLlamado { get; private set; }

        public Task<ResultadoPaginaAuditoria> ListarAsync(
            ConsultaAuditoriaDto filtros, int salto, int cantidad, CancellationToken ct)
        {
            FueLlamado = true;
            var ordenados = registros.OrderByDescending(r => r.Registro.CambiadoEn)
                .ThenByDescending(r => r.Registro.Id).ToArray();
            return Task.FromResult(new ResultadoPaginaAuditoria(
                ordenados.Skip(salto).Take(cantidad).ToArray(), ordenados.Length));
        }

        public Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, NombreResuelto>>(new Dictionary<Guid, NombreResuelto>());

        public int LlamadasAResolverSujetos { get; private set; }

        public Task<IReadOnlyDictionary<Guid, NombreResuelto>> ResolverNombresDeSujetosAsync(
            IReadOnlyCollection<Guid> idsDeUsuarios, IReadOnlyCollection<Guid> idsDePersonas, CancellationToken ct)
        {
            LlamadasAResolverSujetos++;
            return Task.FromResult<IReadOnlyDictionary<Guid, NombreResuelto>>(new Dictionary<Guid, NombreResuelto>());
        }

        public Task<IReadOnlyDictionary<Guid, string>> ResolverNombresDeRolesAsync(
            IReadOnlyCollection<Guid> idsDeRoles, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }

    private sealed class ContratoFalso(
        IReadOnlyList<EventoDeAdministracion>? eventos = null, bool truncado = false, bool fallar = false)
        : IConsultasDeAuditoriaDeAdministracion
    {
        public bool FueLlamado { get; private set; }

        public Task<LoteDeAuditoriaDeAdministracion> ListarAsync(
            DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
        {
            FueLlamado = true;
            if (fallar)
            {
                throw new InvalidOperationException("Falla simulada para el test.");
            }

            var ordenados = (eventos ?? [])
                .OrderByDescending(e => e.OcurridoEn).ThenByDescending(e => e.Id).ToArray();
            return Task.FromResult(new LoteDeAuditoriaDeAdministracion(ordenados, truncado));
        }
    }
}
