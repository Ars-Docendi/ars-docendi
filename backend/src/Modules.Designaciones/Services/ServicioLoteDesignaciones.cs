using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;
using Modules.Portal.Contracts.Queries;

namespace Modules.Designaciones.Services;

public sealed record ArchivoLoteDesignaciones(byte[] Contenido, string NombreArchivo);

public interface IServicioLoteDesignaciones
{
    Task<ArchivoLoteDesignaciones> ExportarAsync(Guid periodoId, CancellationToken ct);
}

internal sealed class ServicioLoteDesignaciones(
    RepositorioLoteDesignaciones repositorio,
    ResolutorActor resolutorActor,
    IConsultasIdentity identity,
    IPortalQueries portal,
    Infrastructure.UnidadDeTrabajo unidadDeTrabajo) : IServicioLoteDesignaciones
{
    public async Task<ArchivoLoteDesignaciones> ExportarAsync(Guid periodoId, CancellationToken ct)
    {
        var actor = await resolutorActor.ResolverAsync(ct);
        if (!actor.EsDeptoWide)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Prohibido,
                "lote-scope-forbidden",
                "El actor no está autorizado a descargar el lote departamental.");
        }

        ConsultaLoteDesignaciones? consulta = null;
        await unidadDeTrabajo.EjecutarEnTransaccionAsync(async token =>
        {
            consulta = await repositorio.LeerAsync(periodoId, token);
            if (consulta is null)
            {
                throw new ExcepcionAplicacion(
                    TipoErrorAplicacion.NoEncontrado,
                    "periodo-not-found",
                    "No se encontró el período solicitado.");
            }

            if (!consulta.Periodo.Activo)
            {
                throw new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "periodo-inactive",
                    "El período dejó de estar activo. Actualizá los períodos antes de reintentar.");
            }
        }, ct, System.Data.IsolationLevel.RepeatableRead);

        var personas = (await identity.ListarPersonasAsync(ct)).ToDictionary(p => p.Id);
        var materias = (await identity.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);
        var datos = consulta!;
        var pedidosEnLote = datos.PedidosDelPeriodo
            .Where(p => p.Estado == EstadosPedido.EnLote)
            .OrderBy(p => p.PersonaId)
            .ThenBy(p => p.MateriaId)
            .ThenBy(p => p.Numero, StringComparer.Ordinal)
            .ToArray();
        var pedidosPorId = pedidosEnLote.ToDictionary(p => p.Id);
        var designacionesPorClave = datos.DesignacionesVigentes
            .GroupBy(d => (d.PersonaId, d.MateriaId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.VigenteDesde).ThenByDescending(d => d.CreadoEn).First());
        var bajas = pedidosEnLote
            .Where(p => p.Novedad == Novedades.Baja)
            .GroupBy(p => (p.PersonaId, p.MateriaId))
            .Select(g => g.First())
            .ToArray();
        var clavesBaja = bajas.Select(p => (p.PersonaId, p.MateriaId)).ToHashSet();
        var altasSinDesignacion = pedidosEnLote
            .Where(p => p.Novedad == Novedades.Alta)
            .Where(p => !designacionesPorClave.ContainsKey((p.PersonaId, p.MateriaId)))
            .Where(p => !clavesBaja.Contains((p.PersonaId, p.MateriaId)))
            .Select(p => MapearPropuestaAlta(p, personas, materias));

        var propuesta = datos.DesignacionesVigentes
            .Where(d => !clavesBaja.Contains((d.PersonaId, d.MateriaId)))
            .Select(d => MapearPropuesta(d, personas, materias, pedidosPorId))
            .Concat(bajas.Select(p => MapearPropuestaBaja(p, personas, materias)))
            .Concat(altasSinDesignacion)
            .OrderBy(f => f.Docente ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(f => f.PersonaId)
            .ThenBy(f => f.Materia ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(f => f.MateriaId)
            .ThenBy(f => f.NumeroPedido, StringComparer.Ordinal)
            .ToArray();

        var altas = new List<FilaAltaLote>();
        foreach (var pedido in pedidosEnLote.Where(p => p.Novedad == Novedades.Alta))
        {
            personas.TryGetValue(pedido.PersonaId, out var persona);
            designacionesPorClave.TryGetValue((pedido.PersonaId, pedido.MateriaId), out var designacion);
            var perfil = await portal.ObtenerPerfilAsync(pedido.PersonaId, ct);
            altas.Add(new FilaAltaLote(
                pedido.PersonaId,
                pedido.MateriaId,
                Nombre(persona),
                persona?.Cuil,
                CargoDedicacion(
                    designacion?.Cargo?.Nombre ?? pedido.CargoSolicitado?.Nombre,
                    designacion?.DedicacionCatalogo?.Nombre
                        ?? designacion?.Dedicacion
                        ?? pedido.DedicacionSolicitadaCatalogo?.Nombre
                        ?? pedido.DedicacionSolicitada),
                persona?.FechaNacimiento,
                perfil?.Contacto.Mail,
                persona?.Telefono,
                pedido.Numero,
                materias.TryGetValue(pedido.MateriaId, out var materia) ? materia.Nombre : null));
        }

        var bajasExportar = bajas
            .Select(p => MapearBaja(p, personas, materias))
            .OrderBy(f => f.Docente ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(f => f.PersonaId)
            .ThenBy(f => f.Materia ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(f => f.MateriaId)
            .ThenBy(f => f.NumeroPedido, StringComparer.Ordinal)
            .ToArray();

        return new ArchivoLoteDesignaciones(
            EscritorXlsxLote.Escribir(
                datos.Periodo,
                datos.PeriodoAnterior,
                propuesta,
                altas
                    .OrderBy(f => f.Docente ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(f => f.PersonaId)
                    .ThenBy(f => f.Materia ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(f => f.MateriaId)
                    .ThenBy(f => f.NumeroPedido, StringComparer.Ordinal)
                    .ToArray(),
                bajasExportar),
            "lote-designaciones.xlsx");
    }

    private static FilaPropuestaLote MapearPropuesta(
        Designacion designacion,
        IReadOnlyDictionary<Guid, Persona> personas,
        IReadOnlyDictionary<Guid, Materia> materias,
        IReadOnlyDictionary<Guid, Pedido> pedidosPorId)
    {
        personas.TryGetValue(designacion.PersonaId, out var persona);
        materias.TryGetValue(designacion.MateriaId, out var materia);
        var pedido = designacion.OrigenPedidoId is { } origen && pedidosPorId.TryGetValue(origen, out var encontrado)
            ? encontrado
            : null;
        var anterior = pedido?.Novedad switch
        {
            Novedades.Alta => null,
            Novedades.CambioDeCargoODedicacion => pedido.Snapshot,
            _ => new SnapshotPedido(
                designacion.Cargo?.Nombre,
                designacion.DedicacionCatalogo?.Nombre ?? designacion.Dedicacion,
                designacion.Horas,
                materia?.Nombre,
                designacion.HorasInvestigacion,
                designacion.HorasExternas),
        };

        return new FilaPropuestaLote(
            designacion.PersonaId,
            designacion.MateriaId,
            Nombre(persona),
            persona?.Cuil,
            CargoDedicacion(anterior?.Cargo, anterior?.Dedicacion),
            CargoDedicacion(
                designacion.Cargo?.Nombre,
                designacion.DedicacionCatalogo?.Nombre ?? designacion.Dedicacion),
            pedido?.Justificacion,
            designacion.Horas,
            materia?.Nombre,
            pedido?.Numero ?? string.Empty);
    }

    private static FilaPropuestaLote MapearPropuestaBaja(
        Pedido pedido,
        IReadOnlyDictionary<Guid, Persona> personas,
        IReadOnlyDictionary<Guid, Materia> materias)
    {
        personas.TryGetValue(pedido.PersonaId, out var persona);
        materias.TryGetValue(pedido.MateriaId, out var materia);
        return new FilaPropuestaLote(
            pedido.PersonaId,
            pedido.MateriaId,
            Nombre(persona),
            persona?.Cuil,
            CargoDedicacion(pedido.Snapshot?.Cargo, pedido.Snapshot?.Dedicacion),
            null,
            MotivoBaja(pedido),
            pedido.Snapshot?.Horas,
            pedido.Snapshot?.Materia ?? materia?.Nombre,
            pedido.Numero);
    }

    private static FilaPropuestaLote MapearPropuestaAlta(
        Pedido pedido,
        IReadOnlyDictionary<Guid, Persona> personas,
        IReadOnlyDictionary<Guid, Materia> materias)
    {
        personas.TryGetValue(pedido.PersonaId, out var persona);
        materias.TryGetValue(pedido.MateriaId, out var materia);
        return new FilaPropuestaLote(
            pedido.PersonaId,
            pedido.MateriaId,
            Nombre(persona),
            persona?.Cuil,
            null,
            CargoDedicacion(
                pedido.CargoSolicitado?.Nombre,
                pedido.DedicacionSolicitadaCatalogo?.Nombre ?? pedido.DedicacionSolicitada),
            pedido.Justificacion,
            pedido.Horas,
            materia?.Nombre,
            pedido.Numero);
    }

    private static FilaBajaLote MapearBaja(
        Pedido pedido,
        IReadOnlyDictionary<Guid, Persona> personas,
        IReadOnlyDictionary<Guid, Materia> materias)
    {
        var fila = MapearPropuestaBaja(pedido, personas, materias);
        return new FilaBajaLote(
            fila.PersonaId,
            fila.MateriaId,
            fila.Docente,
            fila.Cuil,
            fila.CargoAnterior,
            fila.Observacion,
            fila.Horas,
            fila.Materia,
            fila.NumeroPedido);
    }

    private static string? Nombre(Persona? persona) =>
        persona is null ? null : $"{persona.Apellido}, {persona.Nombre}";

    private static string? CargoDedicacion(string? cargo, string? dedicacion)
    {
        var partes = new[] { cargo, dedicacion }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToArray();
        return partes.Length == 0 ? null : string.Join(" / ", partes);
    }

    private static string? MotivoBaja(Pedido pedido)
    {
        var partes = new[] { pedido.TipoBaja, pedido.TipoBajaDetalle, pedido.Justificacion }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToArray();
        return partes.Length == 0 ? null : string.Join(" - ", partes);
    }
}

internal sealed record FilaPropuestaLote(
    Guid PersonaId,
    Guid MateriaId,
    string? Docente,
    string? Cuil,
    string? CargoAnterior,
    string? CargoPropuesto,
    string? Observacion,
    int? Horas,
    string? Materia,
    string NumeroPedido);

internal sealed record FilaAltaLote(
    Guid PersonaId,
    Guid MateriaId,
    string? Docente,
    string? Cuil,
    string? CargoDedicacion,
    DateOnly? FechaNacimiento,
    string? Mail,
    string? Telefono,
    string NumeroPedido,
    string? Materia);

internal sealed record FilaBajaLote(
    Guid PersonaId,
    Guid MateriaId,
    string? Docente,
    string? Cuil,
    string? CargoAnterior,
    string? Motivo,
    int? Horas,
    string? Materia,
    string NumeroPedido);

/// <summary>Escritor de la planilla institucional a partir de la plantilla OOXML.</summary>
internal static class EscritorXlsxLote
{
    private const string NombreRecurso = ".planilla_designaciones.xlsx";
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace XmlNs = "http://www.w3.org/XML/1998/namespace";

    public static byte[] Escribir(
        Periodo periodo,
        Periodo? periodoAnterior,
        IReadOnlyList<FilaPropuestaLote> propuesta,
        IReadOnlyList<FilaAltaLote> altas,
        IReadOnlyList<FilaBajaLote> bajas)
    {
        var nombre = typeof(EscritorXlsxLote).Assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(NombreRecurso, StringComparison.Ordinal));
        using var plantilla = typeof(EscritorXlsxLote).Assembly.GetManifestResourceStream(nombre)
            ?? throw new InvalidOperationException("No se encontró la plantilla de exportación embebida.");
        using var origen = new ZipArchive(plantilla, ZipArchiveMode.Read);
        using var salida = new MemoryStream();
        using (var zip = new ZipArchive(salida, ZipArchiveMode.Create, true))
        {
            foreach (var entrada in origen.Entries)
            {
                var destino = zip.CreateEntry(entrada.FullName, CompressionLevel.Fastest);
                using var stream = destino.Open();
                switch (entrada.FullName)
                {
                    case "xl/workbook.xml":
                        EscribirXml(stream, ModificarLibro(LeerXml(entrada)));
                        break;
                    case "xl/worksheets/sheet1.xml":
                        EscribirXml(stream, ModificarPropuesta(LeerXml(entrada), periodo, periodoAnterior, propuesta));
                        break;
                    case "xl/worksheets/sheet2.xml":
                        EscribirXml(stream, ModificarAltas(LeerXml(entrada), periodo, altas));
                        break;
                    case "xl/worksheets/sheet3.xml":
                        EscribirXml(stream, ModificarBajas(LeerXml(entrada), periodo, bajas));
                        break;
                    case "xl/sharedStrings.xml":
                        EscribirXml(stream, LimpiarPeriodosModelo(LeerXml(entrada)));
                        break;
                    case "docProps/app.xml":
                        EscribirXml(stream, ModificarPropiedades(LeerXml(entrada)));
                        break;
                    default:
                    {
                        using var original = entrada.Open();
                        original.CopyTo(stream);
                        break;
                    }
                }
            }
        }
        return salida.ToArray();
    }

    private static XDocument ModificarLibro(XDocument documento)
    {
        var nombres = new[] { "PROPUESTA COMPLETA", "ALTAS", "BAJAS" };
        var hojas = documento.Descendants(Ns + "sheet").ToArray();
        if (hojas.Length != nombres.Length) throw new InvalidDataException("La plantilla no tiene tres hojas.");
        for (var i = 0; i < nombres.Length; i++) hojas[i].SetAttributeValue("name", nombres[i]);
        return documento;
    }

    private static XDocument ModificarPropuesta(
        XDocument documento,
        Periodo periodo,
        Periodo? anterior,
        IReadOnlyList<FilaPropuestaLote> filas)
    {
        PonerTexto(documento, "D1", Encabezado("DESIGNACIÓN", anterior?.Nombre));
        PonerTexto(documento, "E1", Encabezado("PROPUESTA", periodo.Nombre));
        EscribirFilas(documento, 5, "K", 4, filas.Count, (fila, indice) =>
        {
            var propuesta = filas[indice - 1];
            PonerNumero(fila, "A", indice);
            PonerTexto(fila, "B", propuesta.Docente);
            PonerTexto(fila, "C", propuesta.Cuil);
            PonerTexto(fila, "D", propuesta.CargoAnterior);
            PonerTexto(fila, "E", propuesta.CargoPropuesto);
            PonerTexto(fila, "F", propuesta.Observacion);
            PonerNumero(fila, "G", propuesta.Horas);
            PonerTexto(fila, "H", propuesta.Materia);
            PonerTexto(fila, "I", null);
            PonerTexto(fila, "J", null);
            PonerTexto(fila, "K", null);
        });
        return documento;
    }

    private static XDocument ModificarAltas(
        XDocument documento,
        Periodo periodo,
        IReadOnlyList<FilaAltaLote> filas)
    {
        PonerTexto(documento, "D1", $"CARGO /DEDICACIÓN {periodo.Nombre}");
        EscribirFilas(documento, 2, "G", 1, filas.Count, (fila, indice) =>
        {
            var alta = filas[indice - 1];
            PonerNumero(fila, "A", indice);
            PonerTexto(fila, "B", alta.Docente);
            PonerTexto(fila, "C", alta.Cuil);
            PonerTexto(fila, "D", alta.CargoDedicacion);
            PonerFecha(fila, "E", alta.FechaNacimiento);
            PonerTexto(fila, "F", alta.Mail);
            PonerTexto(fila, "G", alta.Telefono);
        });
        return documento;
    }

    private static XDocument ModificarBajas(
        XDocument documento,
        Periodo periodo,
        IReadOnlyList<FilaBajaLote> filas)
    {
        PonerTexto(documento, "D1", $"CARGO /DEDICACIÓN {periodo.Nombre}");
        EscribirFilas(documento, 2, "E", 1, filas.Count, (fila, indice) =>
        {
            var baja = filas[indice - 1];
            PonerNumero(fila, "A", indice);
            PonerTexto(fila, "B", baja.Docente);
            PonerTexto(fila, "C", baja.Cuil);
            PonerTexto(fila, "D", baja.CargoAnterior);
            PonerTexto(fila, "E", baja.Motivo);
        });
        return documento;
    }

    private static string Encabezado(string tipo, string? periodo) =>
        periodo is null ? $"CARGO /DEDICACIÓN\n{tipo}" : $"CARGO /DEDICACIÓN\n{tipo} {periodo}";

    private static void EscribirFilas(
        XDocument documento,
        int primeraFila,
        string ultimaColumna,
        int ultimaFilaEncabezado,
        int cantidad,
        Action<XElement, int> escribir)
    {
        var datos = documento.Descendants(Ns + "sheetData").Single();
        var plantilla = datos.Elements(Ns + "row").FirstOrDefault(r => NumeroFila(r) >= primeraFila)
            ?? throw new InvalidDataException("La plantilla no tiene fila de datos.");
        foreach (var fila in datos.Elements(Ns + "row").Where(r => NumeroFila(r) >= primeraFila).ToArray()) fila.Remove();
        for (var indice = 1; indice <= cantidad; indice++)
        {
            var fila = new XElement(plantilla);
            fila.SetAttributeValue("r", indice + primeraFila - 1);
            foreach (var celda in fila.Elements(Ns + "c"))
            {
                var columna = Columna(celda);
                celda.SetAttributeValue("r", $"{columna}{indice + primeraFila - 1}");
            }
            datos.Add(fila);
            escribir(fila, indice);
        }
        documento.Descendants(Ns + "dimension").Single().SetAttributeValue(
            "ref", $"A1:{ultimaColumna}{Math.Max(ultimaFilaEncabezado, cantidad + primeraFila - 1)}");
    }

    private static int NumeroFila(XElement fila) =>
        int.Parse(fila.Attribute("r")?.Value ?? throw new InvalidDataException("Fila sin número."), CultureInfo.InvariantCulture);

    private static string Columna(XElement celda) =>
        new(celda.Attribute("r")?.Value?.TakeWhile(char.IsLetter).ToArray() ?? []);

    private static XElement Celda(XElement fila, string columna)
    {
        var celda = fila.Elements(Ns + "c").FirstOrDefault(c => Columna(c) == columna);
        if (celda is not null) return celda;
        celda = new XElement(Ns + "c", new XAttribute("r", $"{columna}{NumeroFila(fila)}"));
        fila.Add(celda);
        return celda;
    }

    private static void PonerTexto(XDocument documento, string referencia, string? valor) =>
        PonerTexto(documento.Descendants(Ns + "c").Single(c => c.Attribute("r")?.Value == referencia), valor);

    private static void PonerTexto(XElement fila, string columna, string? valor) =>
        PonerTexto(Celda(fila, columna), valor);

    private static void PonerTexto(XElement celda, string? valor)
    {
        LimpiarCelda(celda);
        if (valor is null) return;
        celda.SetAttributeValue("t", "inlineStr");
        celda.Add(new XElement(
            Ns + "is",
            new XElement(Ns + "t", new XAttribute(XmlNs + "space", "preserve"), valor)));
    }

    private static void PonerNumero(XElement fila, string columna, int? valor) =>
        PonerNumero(Celda(fila, columna), valor);

    private static void PonerNumero(XElement celda, int? valor)
    {
        LimpiarCelda(celda);
        if (valor is null) return;
        celda.SetAttributeValue("t", "n");
        celda.Add(new XElement(Ns + "v", valor.Value.ToString(CultureInfo.InvariantCulture)));
    }

    private static void PonerFecha(XElement fila, string columna, DateOnly? valor) =>
        PonerFecha(Celda(fila, columna), valor);

    private static void PonerFecha(XElement celda, DateOnly? valor)
    {
        LimpiarCelda(celda);
        if (valor is null) return;
        celda.SetAttributeValue("t", "n");
        celda.Add(new XElement(
            Ns + "v",
            valor.Value.ToDateTime(TimeOnly.MinValue).ToOADate().ToString("0.##########", CultureInfo.InvariantCulture)));
    }

    private static void LimpiarCelda(XElement celda)
    {
        celda.Attribute("t")?.Remove();
        celda.Elements().Where(e => e.Name == Ns + "f" || e.Name == Ns + "v" || e.Name == Ns + "is").Remove();
    }

    private static XDocument LimpiarPeriodosModelo(XDocument documento)
    {
        foreach (var texto in documento.Descendants(Ns + "t"))
        {
            texto.Value = texto.Value
                .Replace("ENERO-ABRIL 2026", string.Empty, StringComparison.Ordinal)
                .Replace("MAYO-AGOSTO 2026", string.Empty, StringComparison.Ordinal)
                .Replace("MAYO AGOSTO 2026", string.Empty, StringComparison.Ordinal)
                .Replace("MAYO-AGOSTO 2025", string.Empty, StringComparison.Ordinal);
        }
        return documento;
    }

    private static XDocument ModificarPropiedades(XDocument documento)
    {
        const string titulo = "PROPUESTA COMPLETA";
        var tituloModelo = documento.Descendants().SingleOrDefault(e =>
            e.Name.LocalName == "lpstr" && e.Value.StartsWith("PROPUESTA COMPLETA", StringComparison.Ordinal));
        if (tituloModelo is not null) tituloModelo.Value = titulo;
        return documento;
    }

    private static XDocument LeerXml(ZipArchiveEntry entrada)
    {
        using var stream = entrada.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void EscribirXml(Stream stream, XDocument documento)
    {
        var configuracion = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
        using var escritor = XmlWriter.Create(stream, configuracion);
        documento.Save(escritor);
    }
}
