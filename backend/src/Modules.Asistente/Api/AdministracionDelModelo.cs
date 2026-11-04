using System.Text.Json;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace Modules.Asistente.Api;

/// <summary>How an administration operation ended; the controller maps it to a status.</summary>
internal enum Desenlace
{
    Exito,

    /// <summary>400: the request names something the module does not offer.</summary>
    PedidoInvalido,

    /// <summary>422: the server-side connection test failed; nothing changed.</summary>
    PruebaFallida,

    /// <summary>503: a stored key has to be read or written and no master key is usable.</summary>
    ClaveMaestraNoDisponible,
}

internal sealed record Resultado<T>(Desenlace Desenlace, T? Valor, string? Codigo)
{
    public static Resultado<T> Ok(T valor) => new(Desenlace.Exito, valor, null);

    public static Resultado<T> Invalido(string codigo) => new(Desenlace.PedidoInvalido, default, codigo);

    public static Resultado<T> Fallida(string codigo) => new(Desenlace.PruebaFallida, default, codigo);

    public static Resultado<T> SinClaveMaestra() =>
        new(Desenlace.ClaveMaestraNoDisponible, default, CodigosDelModelo.ClaveMaestraNoDisponible);
}

/// <summary>Stable codes of the model administration surface, besides <see cref="CodigosDePrueba"/>.</summary>
internal static class CodigosDelModelo
{
    public const string EmpresaDesconocida = "empresa-desconocida";
    public const string ServidorLocalNoConfigurado = "servidor-local-no-configurado";
    public const string ModeloFueraDelCatalogo = "modelo-fuera-del-catalogo";
    public const string CredencialLocalNoAdmitida = "credencial-local-no-admitida";
    public const string ClaveDemasiadoCorta = "clave-demasiado-corta";
    public const string EmpresaSinClave = "empresa-sin-clave";
    public const string ClaveMaestraNoDisponible = "clave-maestra-no-disponible";
    public const string PrecioObligatorio = "precio-obligatorio";
    public const string PrecioNegativo = "precio-negativo";
    public const string PrecioFueraDeRango = "precio-fuera-de-rango";
    public const string PrecioConDemasiadosDecimales = "precio-con-demasiados-decimales";
    public const string VigenciaAnteriorALaVersionActual = "vigencia-anterior-a-la-version-actual";
}

/// <summary>
/// The logic of the model administration endpoints (asistente-modelo-en-caliente, slice 3): test,
/// apply, revert, replace a key, read the screen.
/// </summary>
internal sealed class AdministracionDelModelo(
    RepositorioDelModelo modelos,
    RepositorioDeCredenciales credenciales,
    CifradoDeCredenciales cifrado,
    ProbadorDeConexion probador,
    IAuditoriaDeAdministracion auditoria,
    CacheDeConsultasGeneradas cache,
    RepositorioDePrecios precios,
    TimeProvider reloj,
    IOptions<OpcionesAsistente> asistente,
    IOptions<OpcionesInferencia> inferencia)
{
    public async Task<EstadoDelModeloDto> LeerAsync(CancellationToken ct)
    {
        var seleccion = await modelos.LeerSeleccionAsync(ct);
        var catalogo = await ListarOfrecidosAsync(ct);
        var claves = (await credenciales.ListarAsync(ct)).ToDictionary(c => c.Empresa, StringComparer.Ordinal);

        var grupos = catalogo
            .GroupBy(m => m.Empresa, StringComparer.Ordinal)
            .Select(grupo => new EmpresaDelCatalogoDto(
                grupo.Key,
                ServidoresLocales.EsLocal(grupo.Key) ? "local" : "nube",
                ServidoresLocales.EsLocal(grupo.Key)
                    ? null
                    : claves.TryGetValue(grupo.Key, out var clave)
                        ? new CredencialDeEmpresaDto("guardada", clave.Pista)
                        : new CredencialDeEmpresaDto("faltante", null),
                [.. grupo.Select(m => m.Modelo)]))
            .ToList();

        var enUso = seleccion is null
            ? new ModeloEnUsoDto("servidor", asistente.Value.Proveedor, asistente.Value.Modelo)
            : new ModeloEnUsoDto("pantalla", seleccion.Empresa, seleccion.Modelo);

        return new EstadoDelModeloDto(
            enUso,
            [.. ServidoresLocales.Configurados(asistente.Value, inferencia.Value)],
            grupos,
            cifrado.Disponible,
            [.. (await precios.ListarVigentesAsync(reloj.GetUtcNow(), ct))
                .Where(p => catalogo.Any(m => m.Empresa == p.Empresa && m.Modelo == p.Modelo))
                .Select(ADto)]);
    }

    /// <summary>
    /// What the screen offers and the endpoints accept: the catalog plus, for each configured local
    /// server, the model the environment configures for it (D5).
    /// </summary>
    private async Task<IReadOnlyList<ModeloDelCatalogo>> ListarOfrecidosAsync(CancellationToken ct)
    {
        var catalogo = await modelos.ListarCatalogoAsync(ct);
        var ofrecidos = new List<ModeloDelCatalogo>(catalogo.Count + 2);

        foreach (var servidor in ServidoresLocales.Configurados(asistente.Value, inferencia.Value))
        {
            if (ModeloDelEntorno(servidor) is { } delEntorno)
            {
                ofrecidos.Add(new ModeloDelCatalogo(servidor, delEntorno));
            }
        }

        ofrecidos.AddRange(catalogo.Where(m => !ofrecidos.Any(o => o.Empresa == m.Empresa && o.Modelo == m.Modelo)));
        return ofrecidos;
    }

    /// <summary>
    /// The model the environment configures for a local server: <c>Asistente__Modelo</c>
    /// when <c>Asistente__Proveedor</c> names that server (the same value the resolver
    /// hands to the adapter when nothing is stored). Null otherwise.
    /// </summary>
    private string? ModeloDelEntorno(string servidor) =>
        string.Equals(asistente.Value.Proveedor, servidor, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(asistente.Value.Modelo)
            ? asistente.Value.Modelo.Trim()
            : null;

    public async Task<Resultado<ResultadoDePruebaDto>> ProbarAsync(PedidoDeModeloDto pedido, CancellationToken ct)
    {
        var invalido = await ValidarAsync(pedido, ct);
        if (invalido is not null)
        {
            return Resultado<ResultadoDePruebaDto>.Invalido(invalido);
        }

        var claveNueva = LimpiarClave(pedido.ClaveNueva);

        try
        {
            var prueba = await probador.ProbarAsync(pedido.Empresa, pedido.Modelo, claveNueva, ct);
            return Resultado<ResultadoDePruebaDto>.Ok(prueba.Exito
                ? new ResultadoDePruebaDto(true, prueba.LatenciaMs, null)
                : new ResultadoDePruebaDto(false, null, prueba.Codigo));
        }
        catch (ClaveMaestraNoDisponibleException)
        {
            return Resultado<ResultadoDePruebaDto>.SinClaveMaestra();
        }
    }

    public async Task<Resultado<ModeloAplicadoDto>> AplicarAsync(PedidoDeModeloDto pedido, Guid actor, CancellationToken ct)
    {
        var invalido = await ValidarAsync(pedido, ct);
        if (invalido is not null)
        {
            return Resultado<ModeloAplicadoDto>.Invalido(invalido);
        }

        var esNube = !ServidoresLocales.EsLocal(pedido.Empresa);
        var claveNueva = LimpiarClave(pedido.ClaveNueva);

        if (claveNueva is not null)
        {
            if (claveNueva.Length < RepositorioDeCredenciales.LongitudMinimaDeLaClave)
            {
                return Resultado<ModeloAplicadoDto>.Invalido(CodigosDelModelo.ClaveDemasiadoCorta);
            }

            // Saving needs the master key. Checked before any outbound call.
            if (!cifrado.Disponible)
            {
                return Resultado<ModeloAplicadoDto>.SinClaveMaestra();
            }
        }

        // The server re-tests; whatever the screen showed earlier proves nothing.
        ResultadoDePrueba prueba;
        try
        {
            prueba = await probador.ProbarAsync(pedido.Empresa, pedido.Modelo, claveNueva, ct);
        }
        catch (ClaveMaestraNoDisponibleException)
        {
            return Resultado<ModeloAplicadoDto>.SinClaveMaestra();
        }

        if (!prueba.Exito)
        {
            return Resultado<ModeloAplicadoDto>.Fallida(prueba.Codigo!);
        }

        // From here on the test passed: save the key (if any), then the selection.
        if (claveNueva is not null)
        {
            try
            {
                await GuardarYAuditarClaveAsync(pedido.Empresa, claveNueva, actor, ct);
            }
            catch (ClaveMaestraNoDisponibleException)
            {
                return Resultado<ModeloAplicadoDto>.SinClaveMaestra();
            }
        }
        else if (esNube)
        {
            await credenciales.RegistrarValidacionAsync(pedido.Empresa, ct);
        }

        var antes = await modelos.LeerSeleccionAsync(ct);

        var delEntorno = ServidoresLocales.EsLocal(pedido.Empresa) ? ModeloDelEntorno(pedido.Empresa) : null;
        if (!await modelos.AplicarSeleccionAsync(pedido.Empresa, pedido.Modelo, actor, ct, delEntorno))
        {
            // The catalog changed between the check and the write.
            return Resultado<ModeloAplicadoDto>.Invalido(CodigosDelModelo.ModeloFueraDelCatalogo);
        }

        // A query generated by the previous model must not be served after the switch.
        cache.Vaciar();

        var pista = esNube ? await PistaDeAsync(pedido.Empresa, ct) : null;

        await auditoria.RegistrarAsync(
            actor,
            "modelo.seleccionar",
            antes is null
                ? JsonSerializer.Serialize(new { origen = "servidor" })
                : JsonSerializer.Serialize(new { empresa = antes.Empresa, modelo = antes.Modelo }),
            JsonSerializer.Serialize(new { empresa = pedido.Empresa, modelo = pedido.Modelo, pista }),
            ct);

        return Resultado<ModeloAplicadoDto>.Ok(new ModeloAplicadoDto(
            new ModeloEnUsoDto("pantalla", pedido.Empresa, pedido.Modelo), prueba.LatenciaMs, pista));
    }

    /// <summary>Back to server configuration. Nothing to revert is not an error and writes no audit row.</summary>
    public async Task RestablecerAsync(Guid actor, CancellationToken ct)
    {
        var antes = await modelos.LeerSeleccionAsync(ct);
        if (antes is null || !await modelos.RestablecerAsync(ct))
        {
            return;
        }

        cache.Vaciar();

        await auditoria.RegistrarAsync(
            actor,
            "modelo.restablecer",
            JsonSerializer.Serialize(new { empresa = antes.Empresa, modelo = antes.Modelo }),
            JsonSerializer.Serialize(new { origen = "servidor" }),
            ct);
    }

    public async Task<Resultado<CredencialReemplazadaDto>> ReemplazarClaveAsync(
        string empresa, PedidoDeReemplazoDeClaveDto pedido, Guid actor, CancellationToken ct)
    {
        if (!ClientesDeChat.Empresas.Contains(empresa))
        {
            return Resultado<CredencialReemplazadaDto>.Invalido(CodigosDelModelo.EmpresaSinClave);
        }

        var clave = LimpiarClave(pedido.Clave);
        if (clave is null || clave.Length < RepositorioDeCredenciales.LongitudMinimaDeLaClave)
        {
            return Resultado<CredencialReemplazadaDto>.Invalido(CodigosDelModelo.ClaveDemasiadoCorta);
        }

        if (!cifrado.Disponible)
        {
            return Resultado<CredencialReemplazadaDto>.SinClaveMaestra();
        }

        // Validated with the model in use if it belongs to this company, else with the
        // first catalog model of the company. Independent of the stored selection.
        var seleccion = await modelos.LeerSeleccionAsync(ct);
        var modelo = seleccion is not null && seleccion.Empresa == empresa
            ? seleccion.Modelo
            : (await modelos.ListarCatalogoAsync(ct)).FirstOrDefault(m => m.Empresa == empresa)?.Modelo;
        if (modelo is null)
        {
            return Resultado<CredencialReemplazadaDto>.Invalido(CodigosDelModelo.ModeloFueraDelCatalogo);
        }

        var prueba = await probador.ProbarAsync(empresa, modelo, clave, ct);
        if (!prueba.Exito)
        {
            // The old key stays exactly as it was: nothing has been written.
            return Resultado<CredencialReemplazadaDto>.Fallida(prueba.Codigo!);
        }

        try
        {
            var pista = await GuardarYAuditarClaveAsync(empresa, clave, actor, ct);
            return Resultado<CredencialReemplazadaDto>.Ok(new CredencialReemplazadaDto(empresa, pista));
        }
        catch (ClaveMaestraNoDisponibleException)
        {
            return Resultado<CredencialReemplazadaDto>.SinClaveMaestra();
        }
    }

    /// <summary>
    /// Writes a price as a new version of its model (D9): validation first, then one transaction
    /// that closes the open version and opens the new one, then the audit row, then the answer.
    /// </summary>
    public async Task<Resultado<PrecioDelModeloDto>> CargarPrecioAsync(
        PedidoDePrecioDto pedido, Guid actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (string.IsNullOrWhiteSpace(pedido.Modelo) || string.IsNullOrWhiteSpace(pedido.Empresa)
            || !(await ListarOfrecidosAsync(ct)).Any(m => m.Empresa == pedido.Empresa && m.Modelo == pedido.Modelo))
        {
            return Resultado<PrecioDelModeloDto>.Invalido(CodigosDelModelo.ModeloFueraDelCatalogo);
        }

        if (pedido.EntradaPorMillon is not { } entrada
            || pedido.SalidaPorMillon is not { } salida
            || pedido.VigenteDesde is not { } desde)
        {
            return Resultado<PrecioDelModeloDto>.Invalido(CodigosDelModelo.PrecioObligatorio);
        }

        // Cached tokens priced like input tokens when no cache price is given: a cache
        // price of zero would make them silently free.
        var cache = pedido.CachePorMillon ?? entrada;

        foreach (var monto in new[] { entrada, salida, cache })
        {
            if (monto < 0)
            {
                return Resultado<PrecioDelModeloDto>.Invalido(CodigosDelModelo.PrecioNegativo);
            }

            if (monto > MaximoPorMillon)
            {
                return Resultado<PrecioDelModeloDto>.Invalido(CodigosDelModelo.PrecioFueraDeRango);
            }

            // numeric(18,12) per token holds six decimals per million: more would round.
            if (decimal.Round(monto, DecimalesPorMillon) != monto)
            {
                return Resultado<PrecioDelModeloDto>.Invalido(CodigosDelModelo.PrecioConDemasiadosDecimales);
            }
        }

        var escritura = await precios.EscribirNuevaVersionAsync(
            pedido.Empresa,
            pedido.Modelo,
            entrada / TokensPorMillon,
            salida / TokensPorMillon,
            cache / TokensPorMillon,
            new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            ct);

        if (escritura.Desenlace == DesenlaceDeEscrituraDePrecio.VigenciaAnteriorALaVersionActual)
        {
            return Resultado<PrecioDelModeloDto>.Invalido(CodigosDelModelo.VigenciaAnteriorALaVersionActual);
        }

        var nueva = escritura.Nueva!;

        await auditoria.RegistrarAsync(
            actor,
            "precio.cargar",
            escritura.Anterior is { } anterior ? JsonSerializer.Serialize(JsonDelPrecio(anterior)) : null,
            JsonSerializer.Serialize(JsonDelPrecio(nueva)),
            ct);

        return Resultado<PrecioDelModeloDto>.Ok(ADto(nueva));
    }

    private const decimal TokensPorMillon = 1_000_000m;
    private const decimal MaximoPorMillon = 10_000m;
    private const int DecimalesPorMillon = 6;

    private static PrecioDelModeloDto ADto(VersionDePrecio version) => new(
        version.Empresa,
        version.Modelo,
        PorMillon(version.EntradaPorToken),
        PorMillon(version.SalidaPorToken),
        PorMillon(version.CachePorToken),
        version.Version,
        DateOnly.FromDateTime(version.VigenteDesde.UtcDateTime));

    /// <summary>The per-token column back to USD per million, without trailing zeros.</summary>
    private static decimal PorMillon(decimal porToken) => (porToken * TokensPorMillon) / 1.000000000000000000000000000000m;

    // Audit payload: company, model, the three amounts per million (as exact text) and the
    // start date. Prices are not secrets and no key is in reach of this method.
    private static object JsonDelPrecio(VersionDePrecio version) => new
    {
        empresa = version.Empresa,
        modelo = version.Modelo,
        entradaPorMillon = TextoDelMonto(PorMillon(version.EntradaPorToken)),
        salidaPorMillon = TextoDelMonto(PorMillon(version.SalidaPorToken)),
        cachePorMillon = TextoDelMonto(PorMillon(version.CachePorToken)),
        vigenteDesde = DateOnly.FromDateTime(version.VigenteDesde.UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
    };

    private static string TextoDelMonto(decimal monto) =>
        monto.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Saves the key, records the validation, writes the audit row. Returns the new hint.</summary>
    private async Task<string> GuardarYAuditarClaveAsync(
        string empresa, string clave, Guid actor, CancellationToken ct)
    {
        var pistaAntes = await PistaDeAsync(empresa, ct);

        var pista = await credenciales.GuardarAsync(empresa, clave, actor, ct);
        await credenciales.RegistrarValidacionAsync(empresa, ct);

        await auditoria.RegistrarAsync(
            actor,
            pistaAntes is null ? "credencial.crear" : "credencial.reemplazar",
            pistaAntes is null ? null : JsonSerializer.Serialize(new { empresa, pista = pistaAntes }),
            JsonSerializer.Serialize(new { empresa, pista }),
            ct);

        return pista;
    }

    private async Task<string?> PistaDeAsync(string empresa, CancellationToken ct) =>
        (await credenciales.ListarAsync(ct)).FirstOrDefault(c => c.Empresa == empresa)?.Pista;

    /// <summary>
    /// Whether the company and model may be tested or applied. Local servers are
    /// checked against server configuration here, BEFORE any adapter exists, so a
    /// rejected request makes no outbound call.
    /// </summary>
    private async Task<string?> ValidarAsync(PedidoDeModeloDto pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var empresa = pedido.Empresa;
        var esNube = !string.IsNullOrWhiteSpace(empresa) && ClientesDeChat.Empresas.Contains(empresa);
        var esLocal = !string.IsNullOrWhiteSpace(empresa) && ServidoresLocales.EsLocal(empresa);

        if (!esNube && !esLocal)
        {
            return CodigosDelModelo.EmpresaDesconocida;
        }

        if (esLocal)
        {
            if (!ServidoresLocales.EstaConfigurado(empresa, asistente.Value, inferencia.Value))
            {
                return CodigosDelModelo.ServidorLocalNoConfigurado;
            }

            if (!string.IsNullOrWhiteSpace(pedido.ClaveNueva))
            {
                return CodigosDelModelo.CredencialLocalNoAdmitida;
            }
        }

        if (string.IsNullOrWhiteSpace(pedido.Modelo)
            || !(await ListarOfrecidosAsync(ct)).Any(m => m.Empresa == empresa && m.Modelo == pedido.Modelo))
        {
            return CodigosDelModelo.ModeloFueraDelCatalogo;
        }

        return null;
    }

    private static string? LimpiarClave(string? clave) =>
        string.IsNullOrWhiteSpace(clave) ? null : clave.Trim();
}
