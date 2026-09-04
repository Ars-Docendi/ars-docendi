import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
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

describe("El estado inicial", () => {
  it("muestra el alcance, cuántas áreas hay y los límites", async () => {
    montar(<PanelDePrueba />);

    expect(
      await screen.findByRole("heading", { name: "¿Qué querés saber del sistema?" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Conozco 2 áreas de datos del sistema/)).toBeInTheDocument();
    expect(screen.getByText(/Ves los datos de todo el Departamento/)).toBeInTheDocument();

    expect(screen.getByText("No puedo:")).toBeInTheDocument();
    expect(screen.getByText("No modifica nada: solo consulta.")).toBeInTheDocument();
  });

  it("no muestra el comentario de las áreas: se escribió para el modelo, no para el usuario", async () => {
    // Lo que Secretaría veía en producción bajo «Puedo consultar:»: el `COMMENT ON
    // TABLE` que el backend le manda al modelo en el prefijo del prompt. Nombres
    // de tablas, sinónimos del dominio y advertencias para el modelo no son un
    // texto para el usuario (RNF-18), y el cliente no puede sanearlo.
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

    await screen.findByRole("heading", { name: "¿Qué querés saber del sistema?" });

    expect(screen.queryByText("Puedo consultar:")).toBeNull();
    expect(document.body.textContent).not.toMatch(/identity\./);
    expect(document.body.textContent).not.toMatch(/designaciones\./);
    expect(document.body.textContent).not.toMatch(/NO confundir/);
    // El conteo sí: es lo único de las áreas que se le dice al usuario.
    expect(screen.getByText(/Conozco 1 área de datos del sistema/)).toBeInTheDocument();
  });

  it("sin límites no queda ni el rótulo «No puedo:»", () => {
    // El mismo criterio que Opciones y Sugerencias: lista vacía, nada en pantalla.
    // Un rótulo sin lista debajo es un título que anuncia algo que no está.
    render(
      <EstadoInicial
        capacidades={{ ...CAPACIDADES, noPuede: [] }}
        onElegir={() => {}}
        deshabilitado={false}
      />,
    );

    expect(screen.queryByText(/No puedo/)).toBeNull();
    expect(document.body.textContent).not.toMatch(/No puedo/);
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
