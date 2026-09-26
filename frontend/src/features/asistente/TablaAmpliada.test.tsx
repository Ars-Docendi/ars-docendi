/// <reference types="node" />
import { describe, it, expect, vi, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { TablaDeResultado } from "./components/TablaDeResultado";
import { montar } from "./test/soporte";
import * as descargas from "./utils/descargas";
import type { ColumnaDelResultado } from "./types";

// ============================================================
// «Ampliar tabla» (asistente-tabla-de-resultado): la capa que cubre el modal
// entero. Se prueba a través de `TablaDeResultado`, su único punto de
// entrada real — `TablaAmpliada` sola no decide nada sobre cuándo mostrarse.
// ============================================================

const COLUMNAS: ColumnaDelResultado[] = [
  { nombre: "docente", sensible: false },
  { nombre: "documento", sensible: true },
  { nombre: "horas", sensible: false },
];

const FILAS: unknown[][] = [
  ["Pérez", "28341567", 42],
  ["Gómez", "30111222", 8],
];

const PREGUNTA = "¿Qué docentes están designados en Algoritmos y Estructuras de Datos?";

function abrir() {
  return userEvent.setup();
}

async function ampliar(user: ReturnType<typeof abrir>) {
  await user.click(screen.getByRole("button", { name: "Ampliar tabla" }));
}

/** El botón de orden dentro de una cabecera, buscado dentro de `ambito` (la
 * vista ampliada u otro contenedor), para no depender de cuál de las dos
 * copias de la tabla —en línea o ampliada— `screen` encontraría primero. */
function botonDeOrdenEn(ambito: HTMLElement, nombreDeColumna: RegExp): HTMLElement {
  const cabecera = within(ambito).getByRole("columnheader", { name: nombreDeColumna });
  return within(cabecera).getByRole("button");
}

describe("Ampliar tabla", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("cubre el modal con «Tabla ampliada» y la pregunta como título", async () => {
    const user = abrir();
    montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} pregunta={PREGUNTA} />,
    );

    await ampliar(user);

    const region = screen.getByRole("region", { name: "Tabla ampliada" });
    expect(within(region).getByText("Tabla ampliada")).toBeInTheDocument();
    expect(within(region).getByText(PREGUNTA)).toBeInTheDocument();
    expect(within(region).getByRole("button", { name: "Exportar a CSV" })).toBeInTheDocument();
    expect(within(region).getByRole("button", { name: "Contraer" })).toBeInTheDocument();
  });

  it("mantiene las mismas columnas, marcas de sensible, enlaces y leyenda que la vista en línea", async () => {
    const user = abrir();
    montar(
      <TablaDeResultado
        columnas={COLUMNAS}
        filas={FILAS}
        truncado={false}
        pregunta={PREGUNTA}
        vinculos={[{ fila: 0, columna: 0, tipo: "pedido-designacion", id: "abc-123" }]}
      />,
    );

    await ampliar(user);

    const region = screen.getByRole("region", { name: "Tabla ampliada" });
    expect(
      within(region).getByRole("columnheader", { name: /documento.*dato personal/i }),
    ).toBeInTheDocument();
    expect(within(region).getByRole("link", { name: "Ver el trámite Pérez" })).toHaveAttribute(
      "href",
      "/designaciones/pedidos/abc-123",
    );
    expect(
      within(region).getByText("Las columnas con candado contienen datos personales."),
    ).toBeVisible();
  });

  it("un resultado truncado mantiene el aviso, sin ningún número", async () => {
    const user = abrir();
    montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={true} pregunta={PREGUNTA} />,
    );

    await ampliar(user);

    const region = screen.getByRole("region", { name: "Tabla ampliada" });
    const aviso = within(region).getByText(
      "Hay más resultados de los que se muestran. Acotá la pregunta para verlos.",
    );
    expect(aviso).toBeVisible();
    expect(aviso).not.toHaveTextContent(/\d+ fila/);
  });

  it("el orden es compartido: ordenar en una vista deja la otra ordenada igual", async () => {
    const user = abrir();
    montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} pregunta={PREGUNTA} />,
    );

    await ampliar(user);
    const region = screen.getByRole("region", { name: "Tabla ampliada" });
    await user.click(botonDeOrdenEn(region, /docente/i));

    // Cerrar y reabrir para verificar la vista en línea con el mismo orden.
    await user.click(within(region).getByRole("button", { name: "Contraer" }));

    const filasEnLinea = document.querySelectorAll(
      ".adoc-asistente-tabla > .adoc-table-wrap tbody tr",
    );
    expect(filasEnLinea[0]).toHaveTextContent("Gómez");
    expect(filasEnLinea[1]).toHaveTextContent("Pérez");
  });

  it("Escape contrae la vista y devuelve el foco a «Ampliar tabla», sin cerrar el modal", async () => {
    const user = abrir();
    const cerrarModal = vi.fn();
    montar(
      <div
        role="dialog"
        aria-label="Asistente"
        onKeyDown={(evento) => {
          if (evento.key === "Escape") cerrarModal();
        }}
      >
        <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} pregunta={PREGUNTA} />
      </div>,
    );

    await ampliar(user);
    expect(screen.getByRole("region", { name: "Tabla ampliada" })).toBeInTheDocument();

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("region", { name: "Tabla ampliada" })).toBeNull();
    expect(screen.getByRole("button", { name: "Ampliar tabla" })).toHaveFocus();
    // La capa frenó el Escape en captura: el `onKeyDown` del "modal" —que
    // escucha en burbujeo, más afuera— nunca lo recibió.
    expect(cerrarModal).not.toHaveBeenCalled();
  });

  it("«Contraer» cierra la vista y devuelve el foco a «Ampliar tabla»", async () => {
    const user = abrir();
    montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} pregunta={PREGUNTA} />,
    );

    await ampliar(user);
    await user.click(screen.getByRole("button", { name: "Contraer" }));

    expect(screen.queryByRole("region", { name: "Tabla ampliada" })).toBeNull();
    expect(screen.getByRole("button", { name: "Ampliar tabla" })).toHaveFocus();
  });

  it("«Copiar tabla» copia la tabla en el orden mostrado, separada por tabulaciones, y confirma", async () => {
    // `userEvent.setup()` instala un portapapeles de mentira en `navigator`
    // (mismo patrón que `Mensaje.test.tsx`): lo que se escribe se puede leer
    // después.
    const user = abrir();
    montar(
      <TablaDeResultado columnas={COLUMNAS} filas={FILAS} truncado={false} pregunta={PREGUNTA} />,
    );

    await ampliar(user);
    const region = screen.getByRole("region", { name: "Tabla ampliada" });
    await user.click(botonDeOrdenEn(region, /docente/i));
    await user.click(within(region).getByRole("button", { name: "Copiar tabla" }));

    expect(await navigator.clipboard.readText()).toBe(
      "docente\tdocumento\thoras\nGómez\t30111222\t8\nPérez\t28341567\t42",
    );
    expect(await within(region).findByRole("button", { name: "Copiado" })).toBeInTheDocument();
  });

  it("«Exportar a CSV» exporta la tabla en el orden mostrado", async () => {
    const disparo = vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = abrir();
    montar(
      <TablaDeResultado
        columnas={COLUMNAS}
        filas={FILAS}
        truncado={false}
        pregunta={PREGUNTA}
        hilo="abcdef12-0000-4000-8000-000000000001"
      />,
    );

    await ampliar(user);
    const region = screen.getByRole("region", { name: "Tabla ampliada" });
    await user.click(botonDeOrdenEn(region, /docente/i));
    await user.click(within(region).getByRole("button", { name: "Exportar a CSV" }));

    const contenido = disparo.mock.calls[0][1];
    expect(contenido).toContain("Gómez,30111222,8\r\nPérez,28341567,42");
  });
});
