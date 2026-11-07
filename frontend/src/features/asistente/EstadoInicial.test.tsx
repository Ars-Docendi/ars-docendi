import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { EstadoInicial } from "./components/EstadoInicial";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { RespuestaDelAsistente } from "./types";

// ============================================================
// La pantalla vacía y el panel sin acceso.
//
// El estado inicial se arma SÓLO con lo que devuelve `GET /capacidades`: el
// alcance, los ejemplos, cuántas áreas hay y los límites. De cada área no se pinta
// nada más: `cubre[].nombre` es `schema.tabla` —una etiqueta interna que RNF-18
// prohíbe— y `cubre[].descripcion` es el comentario de la tabla que se le manda al
// modelo, jerga incluida. La fixture trae los dos como llegan para que estos tests
// muerdan si alguien los pinta.
// ============================================================

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

/** Abre la ayuda del «?», que es donde viven la presentación, el alcance y los límites. */
async function abrirAyuda() {
  await userEvent.setup().click(
    await screen.findByRole("button", {
      name: "Qué puede y qué no puede hacer el asistente",
    }),
  );
}

describe("El estado inicial", () => {
  it("la ayuda se cierra con Escape", async () => {
    // Se abre con clic y se cierra sola: un panel que sólo se cierra volviendo al
    // botón obliga a buscarlo con el puntero para seguir leyendo lo de atrás.
    const user = userEvent.setup();
    montar(<PanelDePrueba />);
    await abrirAyuda();

    await user.keyboard("{Escape}");

    expect(screen.queryByText("No puedo:")).toBeNull();
  });

  it("no muestra el comentario de las áreas: se escribió para el modelo, no para el usuario", async () => {
    // Lo que Secretaría veía en producción bajo «Puedo consultar:»: el `COMMENT ON TABLE` que el
    // backend le manda al modelo en el prefijo del prompt.
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue({
      ...CAPACIDADES,
      tablas: 1,
      cubre: [
        {
          nombre: "identity.roles",
          descripcion: "Roles. NO confundir con identity.roles ni con designaciones.designaciones.",
          columnas: 3,
        },
      ],
    });
    montar(<PanelDePrueba />);

    // CON LA AYUDA ABIERTA, que es donde vive el conteo.
    await abrirAyuda();

    expect(screen.queryByText("Puedo consultar:")).toBeNull();
    expect(document.body.textContent).not.toMatch(/identity\./);
    expect(document.body.textContent).not.toMatch(/designaciones\./);
    expect(document.body.textContent).not.toMatch(/NO confundir/);
    // El conteo sí: es lo único de las áreas que se le dice al usuario.
    expect(screen.getByText(/Conozco 1 área de datos del sistema/)).toBeInTheDocument();
  });

  it("un chip manda su pregunta tal cual", async () => {
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    await user.click(await screen.findByRole("button", { name: "¿Qué carreras están vigentes?" }));

    await waitFor(() => expect(consultar).toHaveBeenCalledOnce());
    expect(consultar.mock.calls[0][0].mensaje).toBe("¿Qué carreras están vigentes?");
  });

  it("desaparece con el primer turno", async () => {
    const user = userEvent.setup();
    let resolver: (valor: RespuestaDelAsistente) => void = () => {};
    vi.spyOn(api, "consultar").mockImplementation(
      () => new Promise<RespuestaDelAsistente>((r) => (resolver = r)),
    );
    montar(<PanelDePrueba />);

    await user.click(await screen.findByRole("button", { name: "¿Qué carreras están vigentes?" }));

    // Ya con la pregunta en el hilo, antes de que llegue la respuesta.
    expect(screen.queryByRole("heading", { name: "¿Qué querés saber del sistema?" })).toBeNull();

    resolver(respuesta());
    await screen.findByText("Hay 4 docentes designados.");

    expect(screen.queryByRole("heading", { name: "¿Qué querés saber del sistema?" })).toBeNull();
  });

  it("los chips se deshabilitan en vuelo", () => {
    // El panel lo quita con el primer turno, así que la única forma de tener chips y un turno en
    // vuelo a la vez es que un montaje futuro los deje.
    render(<EstadoInicial capacidades={CAPACIDADES} onElegir={() => {}} deshabilitado />);

    // Acotado a la lista de ejemplos y no a todos los botones de la pantalla: el «?» de la ayuda
    // también es un botón, y contarlo mezclaba dos cosas.
    const chips = within(screen.getByRole("list", { name: "Preguntas de ejemplo" })).getAllByRole(
      "button",
    );

    expect(chips).toHaveLength(CAPACIDADES.ejemplos.length);
    for (const chip of chips) expect(chip).toBeDisabled();
  });
});

describe("Sin acceso", () => {
  it("no hay formulario: sólo el aviso, sin campo ni botón", async () => {
    // Con 403 el panel dejaba el campo y el botón activos y el rechazo recién
    // aparecía al enviar: un formulario que aparenta funcionar (invariante #7).
    vi.spyOn(api, "obtenerCapacidades").mockRejectedValue(
      Object.assign(new Error("Forbidden"), { isAxiosError: true, response: { status: 403 } }),
    );
    montar(<PanelDePrueba />);

    expect(
      await screen.findByText("No tenés acceso al asistente con tus permisos actuales."),
    ).toBeInTheDocument();

    expect(screen.queryByLabelText("Tu pregunta")).toBeNull();
    expect(screen.queryByRole("button", { name: "Enviar" })).toBeNull();
    expect(screen.queryByRole("log")).toBeNull();
    // Es un aviso, no un error: el usuario no hizo nada mal.
    expect(screen.queryByRole("alert")).toBeNull();
  });
});
