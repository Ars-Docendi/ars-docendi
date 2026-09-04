import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { EstadoInicial } from "./components/EstadoInicial";
import { PanelAsistente } from "./components/PanelAsistente";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import type { RespuestaDelAsistente } from "./types";

// ============================================================
// La pantalla vacía y el panel sin acceso.
//
// El estado inicial se arma SÓLO con lo que devuelve `GET /capacidades`: el
// alcance, los ejemplos, la descripción de cada área y los límites. `cubre[].nombre`
// es `schema.tabla` —una etiqueta interna que RNF-18 prohíbe— y la fixture lo trae
// a propósito para que estos tests muerdan si alguien lo pinta.
// ============================================================

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("El estado inicial", () => {
  it("presenta las áreas por su descripción y nunca por su nombre interno", async () => {
    montar(<PanelAsistente />);

    expect(
      await screen.findByRole("heading", { name: "¿Qué querés saber del sistema?" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Ves los datos de todo el Departamento/)).toBeInTheDocument();

    expect(screen.getByText("Puedo consultar:")).toBeInTheDocument();
    expect(screen.getByText("Los pedidos del trámite.")).toBeInTheDocument();
    expect(screen.getByText("El padrón de personas.")).toBeInTheDocument();
    expect(screen.queryByText("designaciones.pedidos")).toBeNull();
    expect(screen.queryByText("identity.personas")).toBeNull();

    expect(screen.getByText("No puedo:")).toBeInTheDocument();
    expect(screen.getByText("No modifica nada: solo consulta.")).toBeInTheDocument();
  });

  it("sin descripciones no hay lista de áreas, y el nombre interno tampoco la reemplaza", async () => {
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue({
      ...CAPACIDADES,
      cubre: [{ nombre: "designaciones.pedidos", descripcion: null, columnas: 12 }],
    });
    montar(<PanelAsistente />);

    await screen.findByRole("heading", { name: "¿Qué querés saber del sistema?" });

    expect(screen.queryByText("Puedo consultar:")).toBeNull();
    expect(document.body.textContent).not.toMatch(/designaciones\./);
  });

  it("un chip manda su pregunta tal cual", async () => {
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelAsistente />);

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
    montar(<PanelAsistente />);

    await user.click(await screen.findByRole("button", { name: "¿Qué carreras están vigentes?" }));

    // Ya con la pregunta en el hilo, antes de que llegue la respuesta.
    expect(screen.queryByRole("heading", { name: "¿Qué querés saber del sistema?" })).toBeNull();

    resolver(respuesta());
    await screen.findByText("Hay 4 docentes designados.");

    expect(screen.queryByRole("heading", { name: "¿Qué querés saber del sistema?" })).toBeNull();
  });

  it("los chips se deshabilitan en vuelo", () => {
    // El panel lo quita con el primer turno, así que la única forma de tener chips
    // y un turno en vuelo a la vez es que un montaje futuro los deje. El contrato
    // vive en el componente, como en las opciones y las sugerencias.
    render(<EstadoInicial capacidades={CAPACIDADES} onElegir={() => {}} deshabilitado />);

    const chips = screen.getAllByRole("button");

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
    montar(<PanelAsistente />);

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
