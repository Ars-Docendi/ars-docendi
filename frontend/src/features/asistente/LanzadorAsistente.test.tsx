import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CanceledError } from "axios";

import { LanzadorAsistente } from "./components/LanzadorAsistente";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import type { RespuestaDelAsistente } from "./types";

// ============================================================
// El lanzador de la barra y el modal que abre. Los tests de que aparece o no
// según el permiso siguen en `asistente.test.tsx`; acá va el modal en sí.
// ============================================================

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("El modal del asistente", () => {
  it("se titula «Asistente», visible y como nombre del diálogo", async () => {
    // Sin título, el Modal de la librería pinta un encabezado con sólo la «×» y
    // el nombre accesible sale de un `aria-label` que nadie ve. Con título hay un
    // encabezado que dice qué es esto, y el diálogo se nombra por él.
    const user = userEvent.setup();
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));

    const dialogo = await screen.findByRole("dialog", { name: "Asistente" });

    expect(within(dialogo).getByRole("heading", { name: "Asistente" })).toBeVisible();
    expect(
      within(dialogo).getByRole("region", { name: "Asistente conversacional" }),
    ).toBeInTheDocument();
  });

  it("abierto, hay un solo «Preguntar» —el lanzador— y el envío se llama «Enviar»", async () => {
    // Dos botones con el mismo nombre en el DOM son dos candidatos iguales para
    // quien navega por lista de controles; el que manda la pregunta dice qué hace.
    const user = userEvent.setup();
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));
    const dialogo = await screen.findByRole("dialog", { name: "Asistente" });

    expect(screen.getAllByRole("button", { name: "Preguntar" })).toHaveLength(1);
    expect(within(dialogo).getByRole("button", { name: "Enviar" })).toBeInTheDocument();
  });
});

// ------------------------------------------------------------------- el foco

describe("El foco del modal", () => {
  it("vuelve al lanzador al cerrar con Escape", async () => {
    // Sin esto el foco se pierde en `body` al cerrar, y quien navega con el
    // teclado vuelve a empezar desde arriba de la página.
    const user = userEvent.setup();
    montar(<LanzadorAsistente />);
    const lanzador = await screen.findByRole("button", { name: "Preguntar" });

    await user.click(lanzador);
    // Al abrir, el foco está en el campo: desde ahí sale el Escape.
    expect(await screen.findByLabelText("Tu pregunta")).toHaveFocus();

    await user.keyboard("{Escape}");

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(lanzador).toHaveFocus();
  });

  it("deja inerte la aplicación de atrás mientras está abierto y la restaura al cerrar", async () => {
    // El Modal de la librería no contiene el foco: Tab se escapa hacia la página
    // de atrás. Se portalea a `body`, hermano de `#root`, así que `inert` sobre la
    // raíz contiene Tab en el diálogo sin implementar un trap a mano.
    const user = userEvent.setup();
    const raiz = document.createElement("div");
    raiz.id = "root";
    document.body.appendChild(raiz);
    montar(<LanzadorAsistente />, raiz);

    const lanzador = await screen.findByRole("button", { name: "Preguntar" });
    expect(raiz).not.toHaveAttribute("inert");

    await user.click(lanzador);
    const dialogo = await screen.findByRole("dialog", { name: "Asistente" });

    expect(raiz).toHaveAttribute("inert");
    // El diálogo queda FUERA de la raíz inerte; si no, tampoco se podría usar.
    expect(raiz.contains(dialogo)).toBe(false);

    await user.keyboard("{Escape}");

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(raiz).not.toHaveAttribute("inert");
  });
});

// ------------------------------------------------------------ la conversación

describe("La conversación del modal", () => {
  it("sobrevive a cerrar y volver a abrir", async () => {
    // Esc y un clic afuera cierran, así que un clic sin querer destruía el hilo.
    // La conversación vive en el lanzador, que vive con la barra.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));
    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user.click(screen.getByRole("button", { name: "Preguntar" }));

    expect(await screen.findByText("Hay 4 docentes designados.")).toBeInTheDocument();
    // Y nada quedó en el navegador: las filas traen datos personales.
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it("cerrar no aborta el turno en vuelo: la respuesta espera al reabrir", async () => {
    const user = userEvent.setup();
    let señal: AbortSignal | undefined;
    let resolver: (valor: RespuestaDelAsistente) => void = () => {};
    vi.spyOn(api, "consultar").mockImplementation(
      (_consulta, _clave, opciones) =>
        new Promise<RespuestaDelAsistente>((r, rechazar) => {
          señal = opciones?.signal;
          resolver = r;
          señal?.addEventListener("abort", () => rechazar(new CanceledError("canceled")));
        }),
    );
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));
    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await waitFor(() => expect(señal).toBeDefined());

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    expect(señal?.aborted).toBe(false);

    resolver(respuesta());
    await user.click(screen.getByRole("button", { name: "Preguntar" }));

    expect(await screen.findByText("Hay 4 docentes designados.")).toBeInTheDocument();
  });
});
