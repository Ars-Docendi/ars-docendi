using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

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
        var usuarios = (await identity.ListarUsuariosAsync(ct)).ToDictionary(u => u.Id);
        var datos = consulta!;
        var finalizados = datos.PedidosDelPeriodo
            .Where(p => p.Estado == EstadosPedido.EnLote)
            .Select(p => MapearPedido(p, personas, materias, usuarios))
            .ToArray();
        var pedidosPorId = datos.PedidosDelPeriodo
            .Where(p => p.Estado == EstadosPedido.EnLote)
            .ToDictionary(p => p.Id);
        var resultantes = datos.DesignacionesVigentes
            .Select(d => MapearDesignacion(d, datos.Periodo, personas, materias, pedidosPorId))
            .ToArray();

        return new ArchivoLoteDesignaciones(
            EscritorXlsxLote.Escribir(datos.Periodo, finalizados, resultantes),
            "lote-designaciones.xlsx");
    }

    private static FilaPedidoLote MapearPedido(
        Pedido pedido,
        IReadOnlyDictionary<Guid, Persona> personas,
        IReadOnlyDictionary<Guid, Materia> materias,
        IReadOnlyDictionary<Guid, Usuario> usuarios)
    {
        personas.TryGetValue(pedido.PersonaId, out var persona);
        materias.TryGetValue(pedido.MateriaId, out var materia);
        var crear = pedido.Historial
            .Where(h => h.Accion == AccionesHistorial.Crear)
            .OrderBy(h => h.CreadoEn).ThenBy(h => h.Id)
            .FirstOrDefault();
        var enviar = pedido.Historial
            .Where(h => h.Accion == AccionesHistorial.Enviar)
            .OrderBy(h => h.CreadoEn).ThenBy(h => h.Id)
            .FirstOrDefault();
        var aprobacion = pedido.Historial
            .Where(h => h.Accion == AccionesHistorial.Aceptar && h.Etapa == EstadosPedido.EnLote)
            .OrderBy(h => h.CreadoEn).ThenBy(h => h.Id)
            .LastOrDefault();

        return new FilaPedidoLote(
            pedido.Numero,
            pedido.Periodo?.Nombre ?? string.Empty,
            Nombre(persona),
            persona?.Legajo,
            materia?.Carrera?.Nombre,
            materia?.Nombre,
            pedido.Novedad,
            pedido.CargoSolicitado?.Nombre,
            pedido.DedicacionSolicitadaCatalogo?.Nombre ?? pedido.DedicacionSolicitada,
            pedido.Horas,
            pedido.HorasInvestigacion,
            pedido.HorasExternas,
            crear?.ActorId is { } actorId && usuarios.TryGetValue(actorId, out var usuario)
                ? usuario.NombreParaMostrar
                : null,
            enviar?.CreadoEn,
            aprobacion?.CreadoEn);
    }

    private static FilaDesignacionLote MapearDesignacion(
        Designacion designacion,
        Periodo periodo,
        IReadOnlyDictionary<Guid, Persona> personas,
        IReadOnlyDictionary<Guid, Materia> materias,
        IReadOnlyDictionary<Guid, Pedido> pedidosPorId)
    {
        personas.TryGetValue(designacion.PersonaId, out var persona);
        materias.TryGetValue(designacion.MateriaId, out var materia);
        var numeroPedido = designacion.OrigenPedidoId is { } origen
            && pedidosPorId.TryGetValue(origen, out var pedido)
                ? pedido.Numero
                : "Continuidad";

        return new FilaDesignacionLote(
            periodo.Nombre,
            periodo.ImpactoDesde,
            periodo.ImpactoHasta,
            Nombre(persona),
            persona?.Legajo,
            materia?.Carrera?.Nombre,
            materia?.Nombre,
            designacion.Cargo?.Nombre,
            designacion.DedicacionCatalogo?.Nombre ?? designacion.Dedicacion,
            designacion.Horas,
            designacion.HorasInvestigacion,
            designacion.HorasExternas,
            numeroPedido);
    }

    private static string? Nombre(Persona? persona) =>
        persona is null ? null : $"{persona.Apellido}, {persona.Nombre}";
}

internal sealed record FilaPedidoLote(
    string Numero,
    string Periodo,
    string? Docente,
    string? Legajo,
    string? Carrera,
    string? Materia,
    string Novedad,
    string? Cargo,
    string? Dedicacion,
    int? Horas,
    int? HorasInvestigacion,
    int? HorasExternas,
    string? Solicitante,
    DateTimeOffset? Inicio,
    DateTimeOffset? Aprobacion);

internal sealed record FilaDesignacionLote(
    string Periodo,
    DateOnly ImpactoDesde,
    DateOnly ImpactoHasta,
    string? Docente,
    string? Legajo,
    string? Carrera,
    string? Materia,
    string? Cargo,
    string? Dedicacion,
    int Horas,
    int? HorasInvestigacion,
    int? HorasExternas,
    string NumeroPedido);

/// <summary>Escritor concreto del libro fijo de dos hojas del lote.</summary>
internal static class EscritorXlsxLote
{
    private const string NamespaceSpreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string NamespaceRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly TimeZoneInfo ZonaArgentina = TimeZoneInfo.FindSystemTimeZoneById(
        "America/Argentina/Buenos_Aires");

    public static byte[] Escribir(
        Periodo periodo,
        IReadOnlyList<FilaPedidoLote> pedidos,
        IReadOnlyList<FilaDesignacionLote> designaciones)
    {
        using var archivo = new MemoryStream();
        using (var zip = new ZipArchive(archivo, ZipArchiveMode.Create, true))
        {
            var generadoEn = DateTimeOffset.UtcNow;
            Agregar(zip, "[Content_Types].xml", ContentTypes());
            Agregar(zip, "_rels/.rels", RelacionesRaiz());
            Agregar(zip, "xl/workbook.xml", Libro());
            Agregar(zip, "xl/_rels/workbook.xml.rels", RelacionesLibro());
            Agregar(zip, "xl/styles.xml", Estilos());
            Agregar(zip, "xl/worksheets/sheet1.xml", HojaPedidos(periodo, pedidos, generadoEn));
            Agregar(zip, "xl/worksheets/sheet2.xml", HojaDesignaciones(periodo, designaciones, generadoEn));
        }
        return archivo.ToArray();
    }

    private static string HojaPedidos(
        Periodo periodo,
        IReadOnlyList<FilaPedidoLote> filas,
        DateTimeOffset generadoEn)
    {
        var encabezados = new[]
        {
            "N° trámite", "Período", "Docente", "Legajo", "Carrera", "Materia", "Novedad",
            "Cargo solicitado", "Dedicación solicitada", "Horas materia", "Horas investigación",
            "Horas externas", "Solicitante", "Inicio", "Aprobación final",
        };
        var contenido = new StringBuilder();
        FilaTexto(contenido, 1, "A", "Período configurado");
        FilaTexto(contenido, 1, "B", periodo.Nombre);
        FilaTexto(contenido, 2, "A", "Generado en");
        FilaFecha(contenido, 2, "B", generadoEn, true);
        FilaTexto(contenido, 3, "A", "Impacto");
        FilaTexto(contenido, 3, "B", $"{periodo.ImpactoDesde:dd/MM/yyyy} a {periodo.ImpactoHasta:dd/MM/yyyy}");
        FilaEncabezados(contenido, 5, encabezados);

        var fila = 6;
        foreach (var pedido in filas)
        {
            AbrirFila(contenido, fila);
            FilaTexto(contenido, fila, "A", pedido.Numero);
            FilaTexto(contenido, fila, "B", pedido.Periodo);
            FilaTexto(contenido, fila, "C", pedido.Docente);
            FilaTexto(contenido, fila, "D", pedido.Legajo);
            FilaTexto(contenido, fila, "E", pedido.Carrera);
            FilaTexto(contenido, fila, "F", pedido.Materia);
            FilaTexto(contenido, fila, "G", pedido.Novedad);
            FilaTexto(contenido, fila, "H", pedido.Cargo);
            FilaTexto(contenido, fila, "I", pedido.Dedicacion);
            FilaNumero(contenido, fila, "J", pedido.Horas);
            FilaNumero(contenido, fila, "K", pedido.HorasInvestigacion);
            FilaNumero(contenido, fila, "L", pedido.HorasExternas);
            FilaTexto(contenido, fila, "M", pedido.Solicitante);
            FilaFecha(contenido, fila, "N", pedido.Inicio, true);
            FilaFecha(contenido, fila, "O", pedido.Aprobacion, true);
            CerrarFila(contenido);
            fila++;
        }

        return DocumentoHoja($"A1:O{Math.Max(fila - 1, 5)}", contenido);
    }

    private static string HojaDesignaciones(
        Periodo periodo,
        IReadOnlyList<FilaDesignacionLote> filas,
        DateTimeOffset generadoEn)
    {
        var encabezados = new[]
        {
            "Período destino", "Impacto desde", "Impacto hasta", "Docente", "Legajo", "Carrera",
            "Materia", "Cargo", "Dedicación", "Horas materia", "Horas investigación", "Horas externas",
            "Pedido de origen",
        };
        var contenido = new StringBuilder();
        FilaTexto(contenido, 1, "A", "Período configurado");
        FilaTexto(contenido, 1, "B", periodo.Nombre);
        FilaTexto(contenido, 2, "A", "Generado en");
        FilaFecha(contenido, 2, "B", generadoEn, true);
        FilaTexto(contenido, 3, "A", "Impacto");
        FilaTexto(contenido, 3, "B", $"{periodo.ImpactoDesde:dd/MM/yyyy} a {periodo.ImpactoHasta:dd/MM/yyyy}");
        FilaEncabezados(contenido, 5, encabezados);

        var fila = 6;
        foreach (var designacion in filas)
        {
            AbrirFila(contenido, fila);
            FilaTexto(contenido, fila, "A", designacion.Periodo);
            FilaFecha(contenido, fila, "B", designacion.ImpactoDesde);
            FilaFecha(contenido, fila, "C", designacion.ImpactoHasta);
            FilaTexto(contenido, fila, "D", designacion.Docente);
            FilaTexto(contenido, fila, "E", designacion.Legajo);
            FilaTexto(contenido, fila, "F", designacion.Carrera);
            FilaTexto(contenido, fila, "G", designacion.Materia);
            FilaTexto(contenido, fila, "H", designacion.Cargo);
            FilaTexto(contenido, fila, "I", designacion.Dedicacion);
            FilaNumero(contenido, fila, "J", designacion.Horas);
            FilaNumero(contenido, fila, "K", designacion.HorasInvestigacion);
            FilaNumero(contenido, fila, "L", designacion.HorasExternas);
            FilaTexto(contenido, fila, "M", designacion.NumeroPedido);
            CerrarFila(contenido);
            fila++;
        }

        return DocumentoHoja($"A1:M{Math.Max(fila - 1, 5)}", contenido);
    }

    private static string DocumentoHoja(string dimension, StringBuilder contenido) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="{NamespaceSpreadsheet}" xmlns:r="{NamespaceRelationships}">
          <sheetViews><sheetView workbookViewId="0"/></sheetViews>
          <dimension ref="{dimension}"/>
          <sheetData>{contenido}</sheetData>
        </worksheet>
        """;

    private static void FilaEncabezados(StringBuilder contenido, int fila, IReadOnlyList<string> encabezados)
    {
        AbrirFila(contenido, fila);
        for (var i = 0; i < encabezados.Count; i++) FilaTexto(contenido, fila, Columna(i), encabezados[i]);
        CerrarFila(contenido);
    }

    private static void AbrirFila(StringBuilder contenido, int fila) => contenido.Append($"<row r=\"{fila}\">");
    private static void CerrarFila(StringBuilder contenido) => contenido.Append("</row>");

    private static void FilaTexto(StringBuilder contenido, int fila, string columna, string? valor)
    {
        if (valor is null)
        {
            contenido.Append($"<c r=\"{columna}{fila}\"/>");
            return;
        }
        contenido.Append($"<c r=\"{columna}{fila}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escapar(valor)}</t></is></c>");
    }

    private static void FilaNumero(StringBuilder contenido, int fila, string columna, int? valor)
    {
        if (valor is null)
        {
            contenido.Append($"<c r=\"{columna}{fila}\"/>");
            return;
        }
        contenido.Append($"<c r=\"{columna}{fila}\" t=\"n\"><v>{valor.Value}</v></c>");
    }

    private static void FilaFecha(
        StringBuilder contenido,
        int fila,
        string columna,
        DateTimeOffset? valor,
        bool incluyeHora = false)
    {
        if (valor is null)
        {
            contenido.Append($"<c r=\"{columna}{fila}\"/>");
            return;
        }
        var fecha = TimeZoneInfo.ConvertTime(valor.Value, ZonaArgentina).DateTime;
        FilaFechaNumerica(contenido, fila, columna, fecha, incluyeHora);
    }

    private static void FilaFecha(StringBuilder contenido, int fila, string columna, DateOnly valor) =>
        FilaFechaNumerica(contenido, fila, columna, valor.ToDateTime(TimeOnly.MinValue), false);

    private static void FilaFechaNumerica(
        StringBuilder contenido,
        int fila,
        string columna,
        DateTime valor,
        bool incluyeHora)
    {
        var estilo = incluyeHora ? 2 : 1;
        var numero = valor.ToOADate().ToString("0.##########", CultureInfo.InvariantCulture);
        contenido.Append($"<c r=\"{columna}{fila}\" s=\"{estilo}\" t=\"n\"><v>{numero}</v></c>");
    }

    private static string Columna(int indice)
    {
        var resultado = string.Empty;
        do
        {
            resultado = (char)('A' + indice % 26) + resultado;
            indice = indice / 26 - 1;
        } while (indice >= 0);
        return resultado;
    }

    private static string Escapar(string texto) => SecurityElement.Escape(texto) ?? string.Empty;

    private static void Agregar(ZipArchive zip, string nombre, string contenido)
    {
        var entrada = zip.CreateEntry(nombre, CompressionLevel.Fastest);
        using var escritor = new StreamWriter(entrada.Open(), new UTF8Encoding(false));
        escritor.Write(contenido);
    }

    private static string ContentTypes() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private static string RelacionesRaiz() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string Libro() => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="{NamespaceSpreadsheet}" xmlns:r="{NamespaceRelationships}">
          <bookViews><workbookView activeTab="0"/></bookViews>
          <sheets>
            <sheet name="Pedidos finalizados" sheetId="1" r:id="rId1"/>
            <sheet name="Designaciones resultantes" sheetId="2" r:id="rId2"/>
          </sheets>
        </workbook>
        """;

    private static string RelacionesLibro() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string Estilos() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="2">
            <numFmt numFmtId="164" formatCode="dd/mm/yyyy"/>
            <numFmt numFmtId="165" formatCode="dd/mm/yyyy hh:mm"/>
          </numFmts>
          <fonts count="1"><font><sz val="11"/><name val="Arial"/></font></fonts>
          <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="3">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="0" applyNumberFormat="1"/>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="0" applyNumberFormat="1"/>
          </cellXfs>
        </styleSheet>
        """;
}
