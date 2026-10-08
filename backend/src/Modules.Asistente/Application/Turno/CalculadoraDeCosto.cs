namespace Modules.Asistente.Application;

/// <summary>
/// Una fila de consumo a costear: lo mínimo que hace falta de una fila de
/// <c>asistente.registro_operativo</c>.
/// </summary>
/// <param name="Proveedor">
/// El valor combinado que <c>IProveedorDeModelo.Nombre</c> escribe en
/// <c>registro_operativo.proveedor</c>, con la forma <c>"{proveedor}/{modelo}"</c>
/// (p. ej. <c>"anthropic/claude-sonnet-5"</c>). Nulo si la fila no invocó a
/// ningún proveedor.
/// </param>
internal sealed record FilaDeConsumo(
    string? Proveedor,
    DateTimeOffset OcurridoEn,
    int TokensDeEntrada,
    int TokensDeSalida,
    int? TokensDeCache);

/// <summary>Una fila de <c>asistente.tabla_de_precios</c> (design.md D6).</summary>
internal sealed record PrecioVigente(
    string Proveedor,
    string Modelo,
    decimal PrecioPorTokenEntrada,
    decimal PrecioPorTokenSalida,
    decimal PrecioPorTokenCache,
    int Version,
    DateTimeOffset VigenteDesde,
    DateTimeOffset? VigenteHasta);

/// <summary>El resultado de costear un conjunto de filas.</summary>
/// <param name="CostoEstimadoTotal">Suma de las filas que sí tenían precio.</param>
/// <param name="FilasSinPrecio">
/// Cuántas filas no tenían ningún precio vigente para su proveedor/modelo en
/// el momento en que ocurrieron. NUNCA se costean como cero: se reportan
/// aparte (design.md, Risks).
/// </param>
internal sealed record ResultadoDeCosteo(decimal CostoEstimadoTotal, int FilasSinPrecio);

/// <summary>
/// Estima el costo en USD de una fila (o un conjunto de filas) de uso del
/// asistente contra una foto de <c>asistente.tabla_de_precios</c>
/// (asistente-panel-de-uso / asistente-presupuesto-persistente, design.md D6).
/// </summary>
/// <remarks>
/// Puro: no consulta nada. Recibe las filas y el snapshot de precios ya
/// resueltos, igual que <see cref="PoliticaDeAbstencion"/> — lo que la hace
/// testeable sin base.
///
/// <b>La versión de precio que importa es la que estaba VIGENTE cuando la
/// fila OCURRIÓ</b>, nunca la más reciente: un precio publicado hoy no puede
/// reescribir la estimación de un turno de la semana pasada (D1/A1). Por eso
/// cada fila busca, entre todas las versiones de su proveedor/modelo, la que
/// contiene su <c>OcurridoEn</c> en el rango <c>[VigenteDesde, VigenteHasta)</c>.
/// </remarks>
internal static class CalculadoraDeCosto
{
    /// <summary>Costea un conjunto de filas, agregando el total y las sin precio.</summary>
    public static ResultadoDeCosteo Costear(
        IReadOnlyList<FilaDeConsumo> filas, IReadOnlyList<PrecioVigente> precios)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(precios);

        var total = 0m;
        var sinPrecio = 0;

        foreach (var fila in filas)
        {
            var costo = CostoDeUnaFila(fila, precios);
            if (costo is { } valor)
            {
                total += valor;
            }
            else
            {
                sinPrecio++;
            }
        }

        return new ResultadoDeCosteo(total, sinPrecio);
    }

    /// <summary>
    /// El costo estimado de una fila, o <c>null</c> si no hay precio vigente
    /// para su proveedor/modelo al momento en que ocurrió.
    /// </summary>
    public static decimal? CostoDeUnaFila(FilaDeConsumo fila, IReadOnlyList<PrecioVigente> precios)
    {
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(precios);

        if (fila.Proveedor is null)
        {
            return null;
        }

        var (proveedor, modelo) = PartirClaveDelProveedor(fila.Proveedor);

        var vigente = precios
            .Where(p => string.Equals(p.Proveedor, proveedor, StringComparison.Ordinal)
                && string.Equals(p.Modelo, modelo, StringComparison.Ordinal)
                && p.VigenteDesde <= fila.OcurridoEn
                && (p.VigenteHasta is null || fila.OcurridoEn < p.VigenteHasta))
            .OrderByDescending(p => p.VigenteDesde)
            .FirstOrDefault();

        if (vigente is null)
        {
            return null;
        }

        return (fila.TokensDeEntrada * vigente.PrecioPorTokenEntrada)
            + (fila.TokensDeSalida * vigente.PrecioPorTokenSalida)
            + ((fila.TokensDeCache ?? 0) * vigente.PrecioPorTokenCache);
    }

    /// <summary>
    /// Separa <c>"{proveedor}/{modelo}"</c> (el valor que
    /// <c>IProveedorDeModelo.Nombre</c> escribe en <c>registro_operativo</c>)
    /// en sus dos partes, para matchear contra las columnas separadas de
    /// <c>tabla_de_precios</c>.
    /// </summary>
    /// <remarks>
    /// Decidido acá porque design.md no especifica cómo se cruzan las dos
    /// formas: el registro guarda un valor combinado (una sola columna,
    /// pensada para mostrarse) y la tabla de precios separa proveedor y
    /// modelo (pensada para versionar cada uno de forma independiente). Un
    /// valor sin <c>/</c> —no debería ocurrir con los proveedores reales,
    /// pero el simulado no lo tiene— se trata como "modelo vacío", que
    /// simplemente no va a matchear ninguna fila de precios y cae en "sin
    /// precio", nunca en una excepción.
    /// </remarks>
    private static (string Proveedor, string Modelo) PartirClaveDelProveedor(string clave)
    {
        var separador = clave.IndexOf('/');
        return separador < 0
            ? (clave, string.Empty)
            : (clave[..separador], clave[(separador + 1)..]);
    }
}
