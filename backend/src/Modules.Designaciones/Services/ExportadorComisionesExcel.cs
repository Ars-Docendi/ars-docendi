using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Modules.Designaciones.Api;

namespace Modules.Designaciones.Services;

public sealed record ArchivoComisiones(byte[] Contenido, string NombreArchivo);

/// <summary>
/// Arma un libro OOXML mínimo con una fila por comisión y las columnas de la grilla. Los textos
/// van como <c>inlineStr</c>: Excel nunca los evalúa como fórmulas aunque Guaraní traiga un "=".
/// </summary>
public static class ExportadorComisionesExcel
{
    private const string Vacio = "—";
    private const int EstiloTexto = 1;
    private const int EstiloEncabezado = 2;

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace NsRelacion = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace NsPaquete = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace NsTipos = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace NsXml = "http://www.w3.org/XML/1998/namespace";

    private static readonly (string Titulo, int Ancho)[] Columnas =
    [
        ("Comisión", 14),
        ("Materia", 40),
        ("Carreras", 40),
        ("Aula", 18),
        ("Bandas horarias", 36),
        ("Docentes", 40),
        ("Modalidad", 24),
        ("Ubicación", 18),
    ];

    private static readonly string[] Dias =
        ["", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

    public static byte[] Generar(IReadOnlyList<ComisionDto> comisiones)
    {
        using var salida = new MemoryStream();
        using (var zip = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true))
        {
            Agregar(zip, "[Content_Types].xml", TiposDeContenido());
            Agregar(zip, "_rels/.rels", RelacionesDelPaquete());
            Agregar(zip, "xl/workbook.xml", Libro());
            Agregar(zip, "xl/_rels/workbook.xml.rels", RelacionesDelLibro());
            Agregar(zip, "xl/styles.xml", Estilos());
            Agregar(zip, "xl/worksheets/sheet1.xml", Hoja(comisiones));
        }
        return salida.ToArray();
    }

    /// <summary>"comisiones-2026-1er-cuatrimestre.xlsx"; el año no se repite si el nombre ya lo incluye.</summary>
    public static string NombreArchivo(int anioAcademico, string periodoNombre)
    {
        var anio = anioAcademico.ToString(CultureInfo.InvariantCulture);
        var etiqueta = periodoNombre.Contains(anio, StringComparison.Ordinal) ? periodoNombre : $"{anio} {periodoNombre}";
        return $"comisiones-{Slug(etiqueta)}.xlsx";
    }

    private static string[] Celdas(ComisionDto c) =>
    [
        c.Nombre,
        $"{c.Materia.Nombre} ({c.Materia.Codigo})",
        Lista(c.Carreras.Select(k => $"{k.Nombre} ({k.Codigo})")),
        Lista(c.Horarios.Select(h => h.Aula).OfType<string>().Distinct(StringComparer.Ordinal)),
        Lista(c.Horarios.Select(Banda)),
        Lista(c.Docentes.Select(d => d.Responsabilidad is null ? d.Nombre : $"{d.Nombre} ({d.Responsabilidad})")),
        c.Modalidad ?? c.ModalidadCodigo ?? Vacio,
        c.Ubicacion ?? Vacio,
    ];

    private static string Lista(IEnumerable<string> valores)
    {
        var texto = string.Join('\n', valores);
        return texto.Length == 0 ? Vacio : texto;
    }

    private static string Banda(HorarioComisionDto h)
    {
        var banda = $"{Dias[h.Dia]} {h.Desde}–{h.Hasta}";
        return h.Aula is null ? banda : $"{banda} · {h.Aula}";
    }

    private static XDocument Hoja(IReadOnlyList<ComisionDto> comisiones)
    {
        var ultimaColumna = Columna(Columnas.Length - 1);
        var filas = new List<XElement> { Fila(1, Columnas.Select(c => c.Titulo).ToArray(), EstiloEncabezado) };
        filas.AddRange(comisiones.Select((c, i) => Fila(i + 2, Celdas(c), EstiloTexto)));

        return new XDocument(new XElement(Ns + "worksheet",
            new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView",
                new XAttribute("workbookViewId", 0),
                new XElement(Ns + "pane",
                    new XAttribute("ySplit", 1),
                    new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"),
                    new XAttribute("state", "frozen")))),
            new XElement(Ns + "cols", Columnas.Select((c, i) => new XElement(Ns + "col",
                new XAttribute("min", i + 1),
                new XAttribute("max", i + 1),
                new XAttribute("width", c.Ancho),
                new XAttribute("customWidth", 1)))),
            new XElement(Ns + "sheetData", filas),
            new XElement(Ns + "autoFilter", new XAttribute("ref", $"A1:{ultimaColumna}{comisiones.Count + 1}"))));
    }

    private static XElement Fila(int numero, string[] valores, int estilo) =>
        new(Ns + "row",
            new XAttribute("r", numero),
            valores.Select((valor, i) => new XElement(Ns + "c",
                new XAttribute("r", $"{Columna(i)}{numero}"),
                new XAttribute("s", estilo),
                new XAttribute("t", "inlineStr"),
                new XElement(Ns + "is", new XElement(Ns + "t",
                    new XAttribute(NsXml + "space", "preserve"),
                    SinCaracteresInvalidos(valor))))));

    private static string Columna(int indice) => ((char)('A' + indice)).ToString();

    /// <summary>Un carácter de control de Guaraní no puede volver inválido el XML del libro.</summary>
    private static string SinCaracteresInvalidos(string valor) =>
        new(valor.Where(c => XmlConvert.IsXmlChar(c) || char.IsSurrogate(c)).ToArray());

    private static XDocument Estilos() => new(new XElement(Ns + "styleSheet",
        new XElement(Ns + "fonts", new XAttribute("count", 2),
            new XElement(Ns + "font", new XElement(Ns + "sz", new XAttribute("val", 11)), new XElement(Ns + "name", new XAttribute("val", "Calibri"))),
            new XElement(Ns + "font", new XElement(Ns + "b"), new XElement(Ns + "sz", new XAttribute("val", 11)), new XElement(Ns + "name", new XAttribute("val", "Calibri")))),
        new XElement(Ns + "fills", new XAttribute("count", 2),
            new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "none"))),
            new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "gray125")))),
        new XElement(Ns + "borders", new XAttribute("count", 1),
            new XElement(Ns + "border", new XElement(Ns + "left"), new XElement(Ns + "right"), new XElement(Ns + "top"), new XElement(Ns + "bottom"), new XElement(Ns + "diagonal"))),
        new XElement(Ns + "cellStyleXfs", new XAttribute("count", 1), Formato(0, conAlineacion: false)),
        new XElement(Ns + "cellXfs", new XAttribute("count", 3),
            Formato(0, conAlineacion: false),
            Formato(0, conAlineacion: true),
            Formato(1, conAlineacion: true))));

    private static XElement Formato(int fuente, bool conAlineacion)
    {
        var formato = new XElement(Ns + "xf",
            new XAttribute("numFmtId", 0),
            new XAttribute("fontId", fuente),
            new XAttribute("fillId", 0),
            new XAttribute("borderId", 0));
        if (conAlineacion)
        {
            formato.Add(new XAttribute("applyAlignment", 1));
            formato.Add(new XElement(Ns + "alignment", new XAttribute("vertical", "top"), new XAttribute("wrapText", 1)));
        }
        return formato;
    }

    private static XDocument Libro() => new(new XElement(Ns + "workbook",
        new XAttribute(XNamespace.Xmlns + "r", NsRelacion),
        new XElement(Ns + "sheets", new XElement(Ns + "sheet",
            new XAttribute("name", "Comisiones"),
            new XAttribute("sheetId", 1),
            new XAttribute(NsRelacion + "id", "rId1")))));

    private static XDocument RelacionesDelLibro() => new(new XElement(NsPaquete + "Relationships",
        Relacion("rId1", NsRelacion + "worksheet", "worksheets/sheet1.xml"),
        Relacion("rId2", NsRelacion + "styles", "styles.xml")));

    private static XDocument RelacionesDelPaquete() => new(new XElement(NsPaquete + "Relationships",
        Relacion("rId1", NsRelacion + "officeDocument", "xl/workbook.xml")));

    private static XElement Relacion(string id, XName tipo, string destino) =>
        new(NsPaquete + "Relationship",
            new XAttribute("Id", id),
            new XAttribute("Type", $"{tipo.NamespaceName}/{tipo.LocalName}"),
            new XAttribute("Target", destino));

    private static XDocument TiposDeContenido() => new(new XElement(NsTipos + "Types",
        new XElement(NsTipos + "Default",
            new XAttribute("Extension", "rels"),
            new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
        new XElement(NsTipos + "Default",
            new XAttribute("Extension", "xml"),
            new XAttribute("ContentType", "application/xml")),
        Reemplazo("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
        Reemplazo("/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
        Reemplazo("/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));

    private static XElement Reemplazo(string parte, string tipo) =>
        new(NsTipos + "Override", new XAttribute("PartName", parte), new XAttribute("ContentType", tipo));

    private static void Agregar(ZipArchive zip, string ruta, XDocument documento)
    {
        using var flujo = zip.CreateEntry(ruta, CompressionLevel.Optimal).Open();
        using var escritor = XmlWriter.Create(flujo, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        documento.Save(escritor);
    }

    private static string Slug(string texto)
    {
        var sinTildes = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var partes = new string(sinTildes.Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('-', partes);
    }
}
