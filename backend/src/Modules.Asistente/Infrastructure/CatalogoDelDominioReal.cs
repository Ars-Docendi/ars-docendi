using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Compone el catálogo del dominio: el índice de entidades más el vocabulario
/// cerrado del trámite.
/// </summary>
/// <remarks>
/// <b>Perezoso</b>, por el mismo motivo que el índice de entidades, el prefijo del
/// prompt y el catálogo de sensibilidad: construirlo al arrancar exigiría base
/// durante el arranque del Host, y el invariante #3 pide que el <c>ping</c>
/// responda con la base detenida.
///
/// Se lee con la conexión básica. Nombre de materia, apellido, estado, novedad y
/// nombre de cargo son columnas públicas para los dos roles del asistente: nada de
/// lo que entra acá es un dato personal sensible.
/// </remarks>
internal sealed class CatalogoDelDominioReal(
    IIndiceDeEntidades indice, CadenaSoloLectura cadena) : ICatalogoDelDominio
{
    /// <summary>Las restricciones de las que sale el vocabulario del trámite.</summary>
    /// <remarks>
    /// Los nombres son los que declara el DDL de designaciones. Que una desaparezca
    /// es un error ruidoso y no un vocabulario vacío: ver
    /// <see cref="VocabularioIlegible"/>.
    /// </remarks>
    private static readonly (string Restriccion, ClaseDeSlot Clase)[] Vocabularios =
    [
        ("pedidos_estado_valido", ClaseDeSlot.Estado),
        ("pedidos_novedad_valida", ClaseDeSlot.Novedad),
        ("pedidos_tipo_baja_valido", ClaseDeSlot.TipoDeBaja),
    ];

    private readonly SemaphoreSlim _turnoDeCalculo = new(1, 1);
    private CatalogoDelDominio? _catalogo;

    /// <summary>Veces que se consultó la base. Existe para los tests del caché.</summary>
    internal int Lecturas { get; private set; }

    public async Task<CatalogoDelDominio> ObtenerAsync(CancellationToken ct)
    {
        if (_catalogo is not null)
        {
            return _catalogo;
        }

        await _turnoDeCalculo.WaitAsync(ct);
        try
        {
            _catalogo ??= await ConstruirAsync(ct);
            return _catalogo;
        }
        finally
        {
            _turnoDeCalculo.Release();
        }
    }

    private async Task<CatalogoDelDominio> ConstruirAsync(CancellationToken ct)
    {
        // Las materias y las personas salen del índice que ya existe. No se las
        // vuelve a leer: si dos piezas construyeran el mismo índice, una de las dos
        // quedaría vieja.
        var entidades = await indice.ObtenerAsync(ct);

        var valores = new List<ValorDeSlot>(
            entidades.Terminos.SelectMany(termino => entidades
                .Valores(termino)
                .Select(valor => new ValorDeSlot(
                    ClaseDe(valor.Clase), valor.Valor, termino, valor.Discriminador))));

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);
        Lecturas++;

        var vocabularios = await LectorDeVocabulario.VocabulariosAsync(
            conexion,
            "designaciones",
            "pedidos",
            [.. Vocabularios.Select(v => v.Restriccion)],
            ct);

        foreach (var (restriccion, clase) in Vocabularios)
        {
            valores.AddRange(vocabularios[restriccion].Select(valor => Termino(clase, valor)));
        }

        foreach (var (valor, cargo) in await LectorDeVocabulario.CargosAsync(conexion, ct))
        {
            // El término sale de la forma preguntable —«JTP»— y el valor del nombre
            // canónico, para que las dos formas resuelvan al mismo cargo y la
            // colisión no se dispare entre un cargo y su propia abreviatura.
            valores.Add(new ValorDeSlot(
                ClaseDeSlot.Cargo, cargo, IndiceDeEntidades.Normalizar(valor), cargo));
        }

        return new CatalogoDelDominio(valores);
    }

    /// <summary>
    /// Un valor del vocabulario cerrado. Su discriminador es el valor mismo: los
    /// vocabularios del trámite no tienen homónimos, porque son listas cerradas de
    /// literales distintos.
    /// </summary>
    private static ValorDeSlot Termino(ClaseDeSlot clase, string valor) =>
        new(clase, valor, IndiceDeEntidades.Normalizar(valor), valor);

    private static ClaseDeSlot ClaseDe(ClaseDeEntidad clase) => clase switch
    {
        ClaseDeEntidad.Materia => ClaseDeSlot.Materia,
        ClaseDeEntidad.Persona => ClaseDeSlot.Persona,
        _ => throw new InvalidOperationException($"Clase de entidad desconocida: {clase}."),
    };
}
