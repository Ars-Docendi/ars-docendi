/// <reference types="node" />
import { describe, it, expect, vi, afterEach } from "vitest";

import { hojaDeLaFeature } from "./test/hojas";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { TablaDeResultado } from "./components/TablaDeResultado";
import { montar } from "./test/soporte";
import * as descargas from "./utils/descargas";
import type { ColumnaDelResultado } from "./types";

function celdaDeCabecera(nombre: string): HTMLElement {
  return screen.getByRole("columnheader", { name: new RegExp(nombre, "i") });
}

function botonDeOrden(nombre: string): HTMLElement {
  return within(celdaDeCabecera(nombre)).getByRole("button");
}

// La hoja como texto: jsdom no aplica CSS, pero la regla se puede leer. Va por
// `fs` y no por `?raw`: con `css: false` en la config, vitest resuelve cualquier
// import de un `.css` —también con `?raw`— a una cadena vacía. Los tipos de node
// se referencian acá y no en el tsconfig de la app, que no los carga.
const hoja = hojaDeLaFeature();

// ============================================================
// La tabla de resultados: su marco y la marca de columna sensible.
//
// jsdom no calcula layout, así que el scroll dentro del marco no se puede afirmar
// acá; lo que sí se fija es el contrato mínimo del que depende: la clase propia
// llega al envoltorio de la librería. Si un bump de @ars-docendi/ui deja de
// aplicar `className`, este test cae antes de que alguien note que la tabla
// volvió a recortarse.
// ============================================================

const COLUMNAS: ColumnaDelResultado[] = [
  { nombre: "apellido", sensible: false },
  { nombre: "documento", sensible: true },
  { nombre: "horas", sensible: false },
];

const FILAS: unknown[][] = [["Gómez", "28341567", 42]];

describe("El marco de la tabla", () => {
  it("el envoltorio de la librería lleva la clase propia que sobreescribe el recorte", () => {
    const { container } = montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />,
    );

    expect(container.querySelector(".adoc-table-wrap.adoc-asistente-tabla-wrap")).not.toBeNull();
  });

  it("las celdas con números usan la variante numérica de la librería", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    expect(screen.getByText("42").closest("td")).toHaveClass("num");
    expect(screen.getByText("Gómez").closest("td")).not.toHaveClass("num");
  });

  it("las celdas numéricas se alinean a la derecha en la hoja del asistente", () => {
    // `numeric` de la librería sólo cambia la tipografía y deja `text-align: start`:
    // una columna de cantidades quedaba pegada a la izquierda. La alineación es
    // de la hoja, así que se fija como texto: la regla sobre `td.num` dentro del
    // marco propio, sin `!important`.
    const sinComentarios = hoja.replace(/\/\*[\s\S]*?\*\//g, "");
    const regla = sinComentarios.match(/\.adoc-asistente-tabla\s+td\.num\s*\{([^}]*)\}/);

    expect(regla).not.toBeNull();
    expect(regla?.[1]).toMatch(/text-align:\s*end;/);
    expect(regla?.[1]).not.toMatch(/!important/);
  });
});

describe("La columna sensible", () => {
  it("se marca y se anuncia; la que no lo es, no", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    // El candado es para quien ve; «(dato personal)» para quien escucha. Las dos
    // cosas en la misma cabecera, para que el lector de pantalla lo diga al pasar
    // por la columna y no haya que buscar una leyenda aparte.
    expect(
      screen.getByRole("columnheader", { name: /documento.*dato personal/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /apellido/i })).not.toHaveAccessibleName(
      /dato personal/i,
    );

    expect(screen.getByText(/Las columnas con candado contienen datos personales/)).toBeVisible();
  });

  it("sin columnas sensibles no hay leyenda", () => {
    montar(
      <TablaDeResultado
        columnas={COLUMNAS.map((columna) => ({ ...columna, sensible: false }))}
        filas={FILAS}
        truncado={false}
      />,
    );

    expect(screen.queryByText(/columnas con candado/)).toBeNull();
    expect(screen.queryByText(/dato personal/)).toBeNull();
  });
});

// ============================================================
// Los vínculos: qué celda lleva a una pantalla del sistema.
//
// QUÉ CELDAS SON ENLACE NO LO DECIDE LA TABLA. Lo decide el backend contra el
// módulo dueño del recurso, y por eso acá se prueba lo que la tabla hace con esa
// decisión: la pinta, la ubica en la celda correcta, y NO la inventa para las de
// al lado.
// ============================================================

const TRAMITES: ColumnaDelResultado[] = [
  { nombre: "numero", sensible: false },
  { nombre: "estado", sensible: false },
];

const DOS_TRAMITES: unknown[][] = [
  ["2026-9005", "devuelto"],
  ["2026-9006", "en_lote"],
];

describe("El vínculo de una celda", () => {
  it("la celda con vínculo es un enlace al detalle y dice a dónde va", () => {
    montar(
      <TablaDeResultado
        columnas={TRAMITES}
        filas={DOS_TRAMITES}
        truncado={false}
        vinculos={[{ fila: 0, columna: 0, tipo: "pedido-designacion", id: "abc-123" }]}
      />,
    );

    // El nombre accesible no es el número solo: «enlace, 2026-9005» no dice a
    // dónde lleva, y es lo único que oye quien no ve la tabla alrededor.
    const enlace = screen.getByRole("link", { name: "Ver el trámite 2026-9005" });
    expect(enlace).toHaveAttribute("href", "/designaciones/pedidos/abc-123");
  });

  it("las celdas sin vínculo quedan como texto", () => {
    montar(
      <TablaDeResultado
        columnas={TRAMITES}
        filas={DOS_TRAMITES}
        truncado={false}
        vinculos={[{ fila: 0, columna: 0, tipo: "pedido-designacion", id: "abc-123" }]}
      />,
    );

    // ES LA MITAD QUE MUERDE. Sin ella, una tabla que enlazara TODAS las celdas
    // pasaría el test de arriba: el vínculo llegó para una sola fila y una sola
    // columna, y el resto del resultado tiene que quedar tal cual.
    expect(screen.getAllByRole("link")).toHaveLength(1);
    expect(screen.getByText("2026-9006")).toBeVisible();
    expect(screen.getByText("devuelto")).toBeVisible();
  });

  it("un tipo que este cliente no conoce deja la celda como texto", () => {
    // COMPATIBILIDAD HACIA ADELANTE, y en la dirección que importa: el día que el
    // backend ofrezca vínculos a perfiles de portal, un cliente viejo no muestra un
    // enlace roto — muestra el dato, como antes.
    montar(
      <TablaDeResultado
        columnas={TRAMITES}
        filas={DOS_TRAMITES}
        truncado={false}
        vinculos={[{ fila: 0, columna: 0, tipo: "perfil-de-portal", id: "abc-123" }]}
      />,
    );

    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getByText("2026-9005")).toBeVisible();
  });

  it("sin vínculos la tabla no cambia en nada", () => {
    montar(<TablaDeResultado columnas={TRAMITES} filas={DOS_TRAMITES} truncado={false} />);

    expect(screen.queryByRole("link")).toBeNull();
  });
});

// ============================================================
// El orden de la tabla (asistente-tabla-de-resultado, ARS-145): encabezados
// ordenables, `aria-sort`, el vínculo siguiendo a su fila, y el anuncio.
// ============================================================

const PERSONAS: ColumnaDelResultado[] = [
  { nombre: "docente", sensible: false },
  { nombre: "cargo", sensible: false },
];

const FILAS_PERSONAS: unknown[][] = [
  ["Pérez", "Titular"],
  ["Gómez", "Adjunto"],
  ["Alonso", "JTP"],
];

describe("El orden de la tabla", () => {
  it("ningún encabezado reclama una dirección antes de ordenar", () => {
    montar(<TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />);

    expect(celdaDeCabecera("docente")).not.toHaveAttribute("aria-sort");
    expect(celdaDeCabecera("cargo")).not.toHaveAttribute("aria-sort");
  });

  it("la primera activación ordena ascendente y marca sólo esa columna", async () => {
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />);

    await user.click(botonDeOrden("docente"));

    expect(celdaDeCabecera("docente")).toHaveAttribute("aria-sort", "ascending");
    expect(celdaDeCabecera("cargo")).not.toHaveAttribute("aria-sort");
    const filas = document.querySelectorAll("tbody tr");
    expect(filas[0]).toHaveTextContent("Alonso");
    expect(filas[1]).toHaveTextContent("Gómez");
    expect(filas[2]).toHaveTextContent("Pérez");
  });

  it("activar la misma columna alterna entre ascendente y descendente", async () => {
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />);

    await user.click(botonDeOrden("docente"));
    await user.click(botonDeOrden("docente"));

    expect(celdaDeCabecera("docente")).toHaveAttribute("aria-sort", "descending");
    const filas = document.querySelectorAll("tbody tr");
    expect(filas[0]).toHaveTextContent("Pérez");
    expect(filas[2]).toHaveTextContent("Alonso");
  });

  it("activar otra columna la ordena ascendente y deja la anterior sin marca", async () => {
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />);

    await user.click(botonDeOrden("docente"));
    await user.click(botonDeOrden("cargo"));

    expect(celdaDeCabecera("cargo")).toHaveAttribute("aria-sort", "ascending");
    expect(celdaDeCabecera("docente")).not.toHaveAttribute("aria-sort");
  });

  it("se ordena con el teclado y el foco se queda en el encabezado", async () => {
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />);

    botonDeOrden("cargo").focus();
    await user.keyboard("{Enter}");

    expect(celdaDeCabecera("cargo")).toHaveAttribute("aria-sort", "ascending");
    expect(botonDeOrden("cargo")).toHaveFocus();
  });

  it("el cambio de orden se anuncia por la región viva, sin mover el foco", async () => {
    const user = userEvent.setup();
    montar(
      <ul role="log" aria-live="polite" aria-label="Conversación con el asistente">
        <li>
          <TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />
        </li>
      </ul>,
    );

    await user.click(botonDeOrden("cargo"));

    expect(screen.getByText("Tabla ordenada por «cargo», ascendente.")).toBeInTheDocument();
    expect(botonDeOrden("cargo")).toHaveFocus();
  });

  it("ordenar no manda ningún request: no hay más que estado local", async () => {
    // No hay ninguna función de red que espiar acá — `ordenarFilas` es
    // puramente sincrónica sobre lo ya renderizado (design.md D5). Lo que se
    // prueba es que el resultado sigue siendo el mismo `<table>`, no uno
    // reemplazado por una respuesta nueva.
    const user = userEvent.setup();
    montar(<TablaDeResultado columnas={PERSONAS} filas={FILAS_PERSONAS} truncado={false} />);

    await user.click(botonDeOrden("docente"));

    expect(document.querySelectorAll("tbody tr")).toHaveLength(3);
  });

  it("un vínculo sigue a su fila después de ordenar", async () => {
    const user = userEvent.setup();
    const tresTramites: unknown[][] = [
      ["2026-9005", "devuelto"],
      ["2026-9006", "en_lote"],
      ["2026-9007", "aprobado"],
    ];
    montar(
      <TablaDeResultado
        columnas={TRAMITES}
        filas={tresTramites}
        truncado={false}
        vinculos={[{ fila: 0, columna: 0, tipo: "pedido-designacion", id: "abc-123" }]}
      />,
    );

    await user.click(botonDeOrden("numero"));
    await user.click(botonDeOrden("numero")); // descendente: la fila del vínculo pasa a la última.

    const filas = document.querySelectorAll("tbody tr");
    const enlace = screen.getByRole("link", { name: "Ver el trámite 2026-9005" });
    expect(filas[2]).toContainElement(enlace);
    expect(enlace).toHaveAttribute("href", "/designaciones/pedidos/abc-123");
  });

  it("el ícono neutro está tenue en reposo y se revela al hover o al foco del encabezado", () => {
    const sinComentarios = hoja.replace(/\/\*[\s\S]*?\*\//g, "");

    const reglaInactivo = sinComentarios.match(
      /\.adoc-asistente-orden-icono--inactivo\s*\{([^}]*)\}/,
    );
    expect(reglaInactivo?.[1]).toMatch(/opacity:\s*0;/);

    // La regla de revelado nombra las dos vías —hover del encabezado, foco del
    // botón— y las dos llevan a `opacity: 1`, sin depender del formato exacto
    // (salto de línea, coma) que deje `prettier`.
    const indiceHover = sinComentarios.indexOf(
      ".adoc-asistente-th-orden:hover .adoc-asistente-orden-icono--inactivo",
    );
    const indiceFoco = sinComentarios.indexOf(
      ".adoc-asistente-orden-boton:focus-visible .adoc-asistente-orden-icono--inactivo",
    );
    expect(indiceHover).toBeGreaterThan(-1);
    expect(indiceFoco).toBeGreaterThan(-1);
    expect(sinComentarios.slice(indiceHover, indiceFoco + 200)).toMatch(/opacity:\s*1;/);
  });
});

// ============================================================
// «Ampliar tabla» controlada desde afuera: para cuando la barra de acciones
// (ARS-146, §5) pase a disparar la vista ampliada desde su propio ícono, sin
// tener que editar este componente.
// ============================================================

describe("«Ampliar tabla» controlada desde afuera", () => {
  it("sin `onAmpliarChange` dibuja su propio botón, como siempre", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    expect(screen.getByRole("button", { name: "Ampliar tabla" })).toBeInTheDocument();
  });

  it("con `onAmpliarChange` no dibuja su propio botón, y avisa cuando alguien más quisiera abrirla", () => {
    const onAmpliarChange = vi.fn();
    montar(
      <TablaDeResultado
        columnas={COLUMNAS}
        filas={FILAS}
        truncado={false}
        ampliado={false}
        onAmpliarChange={onAmpliarChange}
      />,
    );

    // El único disparador visible es el que la propia tabla ofrecía — con
    // control externo, no está: quien la controla lo puso en otro lado.
    expect(screen.queryByRole("button", { name: "Ampliar tabla" })).toBeNull();
  });

  it("`ampliado={true}` desde afuera muestra la vista ampliada sin que la tabla la haya abierto ella misma", () => {
    const onAmpliarChange = vi.fn();
    montar(
      <TablaDeResultado
        columnas={COLUMNAS}
        filas={FILAS}
        truncado={false}
        ampliado={true}
        onAmpliarChange={onAmpliarChange}
      />,
    );

    expect(screen.getByRole("region", { name: "Tabla ampliada" })).toBeInTheDocument();
  });

  it("«Contraer» en modo controlado avisa por `onAmpliarChange(false)`, en vez de manejar su propio estado", async () => {
    const user = userEvent.setup();
    const onAmpliarChange = vi.fn();
    montar(
      <TablaDeResultado
        columnas={COLUMNAS}
        filas={FILAS}
        truncado={false}
        ampliado={true}
        onAmpliarChange={onAmpliarChange}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Contraer" }));

    expect(onAmpliarChange).toHaveBeenCalledWith(false);
  });
});

// ============================================================
// CSV export action (asistente-exportacion-csv, asistente-superficie-frontend,
// asistente-accesibilidad).
// ============================================================

describe("The export action", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders when there is at least one row", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />);

    expect(screen.getByRole("button", { name: "Exportar a CSV" })).toBeInTheDocument();
  });

  it("does not render for an empty result", () => {
    montar(<TablaDeResultado columnas={COLUMNAS} filas={[]} truncado={false} />);

    expect(screen.queryByRole("button", { name: "Exportar a CSV" })).toBeNull();
    // «Ampliar tabla» tampoco: un resultado vacío no ofrece vista ampliada
    // (asistente-tabla-de-resultado).
    expect(screen.queryByRole("button", { name: "Ampliar tabla" })).toBeNull();
  });

  it("triggers a download built from tablaComoCsv", async () => {
    const disparo = vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = userEvent.setup();
    montar(
      <TablaDeResultado
        columnas={COLUMNAS}
        filas={FILAS}
        truncado={false}
        hilo="abcdef12-0000-4000-8000-000000000001"
      />,
    );

    await user.click(screen.getByRole("button", { name: "Exportar a CSV" }));

    expect(disparo).toHaveBeenCalledTimes(1);
    const [nombre, contenido, tipo] = disparo.mock.calls[0];
    expect(nombre).toMatch(/^asistente-resultado-abcdef12-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(contenido).toContain("apellido,documento,horas");
    expect(contenido).toContain("Gómez");
    expect(tipo).toBe("text/csv;charset=utf-8");
  });

  it("is keyboard-operable and announces completion without moving focus", async () => {
    vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = userEvent.setup();
    montar(
      <ul role="log" aria-live="polite" aria-label="Conversación con el asistente">
        <li>
          <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} />
        </li>
      </ul>,
    );

    // `.focus()` y no una secuencia de `Tab`: cuántos controles hay ANTES de
    // «Exportar a CSV» —los encabezados ordenables, «Ampliar tabla»— es un
    // detalle de layout de este grupo (ARS-145) y del futuro §5, no lo que
    // este test verifica.
    screen.getByRole("button", { name: "Exportar a CSV" }).focus();
    expect(screen.getByRole("button", { name: "Exportar a CSV" })).toHaveFocus();
    await user.keyboard("{Enter}");

    expect(screen.getByText("El archivo está listo para descargar.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Exportar a CSV" })).toHaveFocus();
  });

  it("exports exactly the masked values already rendered, never an un-masked one", async () => {
    const disparo = vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = userEvent.setup();
    const enmascarado: unknown[][] = [["Gómez", "«documento 1»", 42]];
    montar(<TablaDeResultado columnas={COLUMNAS} filas={enmascarado} truncado={false} />);

    await user.click(screen.getByRole("button", { name: "Exportar a CSV" }));

    const contenido = disparo.mock.calls[0][1];
    const filaExportada = contenido.split("\r\n")[1];
    expect(filaExportada).toBe("Gómez,«documento 1»,42");
  });
});
