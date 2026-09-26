import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError } from "axios";

import * as api from "./api/asistenteApi";
import * as mencionesApi from "./api/mencionesApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { ResultadoDeMencion } from "./types";

// ============================================================
// El popover de menciones «@materia» / «#docente» del composer
// (asistente-menciones, design.md D10/D11 de asistente-rediseno-v3).
//
// Cada `describe` cubre uno de los escenarios del spec funcional
// (openspec/changes/asistente-rediseno-v3/specs/asistente-menciones/spec.md,
// requisitos «The composer offers…», «Choosing a mention…» y «The mention
// popover is an accessible combobox»). Los escenarios del lado del backend
// —alcance por carrera, RLS de designaciones, el carril SQL, el historial—
// ya están cubiertos del otro lado; acá sólo lo que decide el cliente.
// ============================================================

const ALGORITMOS: ResultadoDeMencion = {
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  nombre: "Algoritmos y Estructuras de Datos",
  carrera: "Ingeniería Informática",
  codigo: "AED-101",
  cargo: null,
};

const ALGEBRA: ResultadoDeMencion = {
  id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
  nombre: "Álgebra",
  carrera: "Ingeniería Informática",
  codigo: "ALG-100",
  cargo: null,
};

const CALCULO: ResultadoDeMencion = {
  id: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
  nombre: "Cálculo I",
  carrera: "Ingeniería Informática",
  codigo: "CAL-100",
  cargo: null,
};

const DOCENTE: ResultadoDeMencion = {
  id: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
  nombre: "Juan Pérez",
  carrera: null,
  codigo: null,
  cargo: "Profesor Adjunto",
};

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

/** Monta el panel real y devuelve el campo de la pregunta, ya listo. */
async function abrirComposer() {
  const user = userEvent.setup();
  montar(<PanelDePrueba umbralDelIndicadorMs={0} />);
  const campo = await screen.findByLabelText("Tu pregunta");
  return { user, campo };
}

describe("La pista bajo 2 letras (no busca)", () => {
  it("«@a» muestra la pista de materias sin mandar ningún request", async () => {
    const buscar = vi.spyOn(mencionesApi, "buscarMenciones");
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@a");

    expect(
      await screen.findByText("Escribí al menos 2 letras para buscar materias."),
    ).toBeInTheDocument();
    expect(buscar).not.toHaveBeenCalled();
  });

  it("«#j» muestra la pista de docentes", async () => {
    const buscar = vi.spyOn(mencionesApi, "buscarMenciones");
    const { user, campo } = await abrirComposer();

    await user.type(campo, "#j");

    expect(
      await screen.findByText("Escribí al menos 2 letras para buscar docentes."),
    ).toBeInTheDocument();
    expect(buscar).not.toHaveBeenCalled();
  });

  it("un «@» a mitad de palabra no cuenta como mención", async () => {
    const buscar = vi.spyOn(mencionesApi, "buscarMenciones");
    const { user, campo } = await abrirComposer();

    await user.type(campo, "juan@algo.com");

    expect(screen.queryByText(/Escribí al menos 2 letras/)).toBeNull();
    expect(buscar).not.toHaveBeenCalled();
  });
});

describe("El popover con 2 letras o más", () => {
  it("lista materias con su carrera y su código", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");

    const opcion = await screen.findByRole("option", {
      name: /Algoritmos y Estructuras de Datos/,
    });
    expect(within(opcion).getByText("Ingeniería Informática")).toBeInTheDocument();
    expect(within(opcion).getByText("AED-101")).toBeInTheDocument();
  });

  it("lista docentes con su cargo", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [DOCENTE],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "#perez");

    const opcion = await screen.findByRole("option", { name: /Juan Pérez/ });
    expect(within(opcion).getByText("Profesor Adjunto")).toBeInTheDocument();
  });

  it("avisa que hay más coincidencias, sin contarlas", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: true,
    });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@al");

    expect(
      await screen.findByText("Hay más coincidencias. Seguí escribiendo para acotar."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/^\d+ coincidencias/)).toBeNull();
  });

  it("sin coincidencias muestra el texto vacío acotado a las carreras del actor", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({ resultados: [], hayMas: false });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@zzz");

    // El mismo texto se anuncia además por la región viva existente
    // (asistente-accesibilidad), así que hay que acotar al párrafo del
    // popover: no es una duplicación visual, las dos veces es un solo nodo
    // visible y uno `sr-only`.
    expect(
      await screen.findByText(
        "Sin materias que coincidan en las carreras a las que tenés acceso.",
        { selector: "p.adoc-asistente-menciones-vacio" },
      ),
    ).toBeInTheDocument();
  });

  it("el pie tiene el candado y los atajos de teclado", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");
    await screen.findByRole("option", { name: /Algoritmos/ });

    expect(
      screen.getByText(
        "Solo aparecen materias y docentes de las carreras a las que tu perfil tiene acceso.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("Enter elige · Esc cierra")).toBeInTheDocument();
  });

  it("junta las teclas de una sola tecleada en un único pedido, con el término final (debounce)", async () => {
    const buscar = vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");
    await screen.findByRole("option", { name: /Algoritmos/ });

    expect(buscar).toHaveBeenCalledTimes(1);
    expect(buscar).toHaveBeenCalledWith("materia", "algoritmos", expect.anything());
  });
});

describe("Elegir una mención inserta un chip cuya referencia viaja con la pregunta", () => {
  it("la mención elegida viaja como referencia, y la pregunta enviada la muestra como chip", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");
    await user.click(
      await screen.findByRole("option", { name: /Algoritmos y Estructuras de Datos/ }),
    );

    // El texto quedó insertado y el chip removible, aparte, en su propia fila.
    expect(campo).toHaveValue("@Algoritmos y Estructuras de Datos ");
    expect(
      screen.getByRole("button", { name: "Quitar la mención @Algoritmos y Estructuras de Datos" }),
    ).toBeInTheDocument();

    await user.type(campo, "¿Cuántos alumnos aprobaron?");
    await user.keyboard("{Enter}");

    await screen.findByText("Hay 4 docentes designados.");
    expect(consultar).toHaveBeenCalledOnce();
    expect(consultar.mock.calls[0][0].referencias).toEqual([
      { tipo: "materia", id: ALGORITMOS.id },
    ]);

    // La pregunta ya enviada muestra la mención como chip en la burbuja.
    const chipEnviado = screen.getByText("@Algoritmos y Estructuras de Datos", {
      selector: ".adoc-asistente-mencion-chip--enviada",
    });
    expect(chipEnviado).toBeInTheDocument();
  });

  it("quitar el chip borra su texto del campo y su referencia no viaja", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");
    await user.click(await screen.findByRole("option", { name: /Algoritmos/ }));
    await user.click(
      screen.getByRole("button", { name: "Quitar la mención @Algoritmos y Estructuras de Datos" }),
    );

    expect(campo).toHaveValue("");
    expect(screen.queryByRole("button", { name: /Quitar la mención/ })).toBeNull();

    await user.type(campo, "¿cuántas materias hay?");
    await user.keyboard("{Enter}");

    await screen.findByText("Hay 4 docentes designados.");
    expect(consultar.mock.calls[0][0].referencias).toBeUndefined();
  });

  it("borrar a mano el texto de la mención también le quita la referencia (spec: «Deleting the mention text drops its reference»)", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");
    await user.click(await screen.findByRole("option", { name: /Algoritmos/ }));

    // El chip QUEDA en su fila —no lo borra un cambio de texto, sólo su propio
    // botón «Quitar»— pero su referencia no viaja si el texto ya no está.
    await user.clear(campo);
    await user.type(campo, "¿cuántas materias hay?");
    await user.keyboard("{Enter}");

    await screen.findByText("Hay 4 docentes designados.");
    expect(consultar.mock.calls[0][0].referencias).toBeUndefined();
  });

  it("hasta 5 menciones: la sexta elección no se agrega", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    for (let vez = 0; vez < 5; vez += 1) {
      await user.type(campo, "@algoritmos");
      await user.click(await screen.findByRole("option", { name: /Algoritmos/ }));
    }
    expect(screen.getAllByRole("button", { name: /Quitar la mención/ })).toHaveLength(5);

    await user.type(campo, "@algoritmos");
    await screen.findByRole("option", { name: /Algoritmos/ });
    await user.click(screen.getByRole("option", { name: /Algoritmos/ }));

    expect(screen.getAllByRole("button", { name: /Quitar la mención/ })).toHaveLength(5);
  });
});

describe("El combobox accesible del popover", () => {
  it("las flechas y Enter eligen sin sacar el foco del campo", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS, ALGEBRA, CALCULO],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@al");
    await screen.findByRole("option", { name: /Algoritmos/ });

    await user.keyboard("{ArrowDown}{ArrowDown}");

    const tercera = screen.getByRole("option", { name: /Cálculo I/ });
    expect(tercera).toHaveAttribute("aria-selected", "true");
    expect(campo).toHaveAttribute("aria-activedescendant", tercera.id);

    await user.keyboard("{Enter}");

    expect(campo).toHaveFocus();
    expect(campo).toHaveValue("@Cálculo I ");
  });

  it("aria-expanded y aria-controls sólo mientras el popover está abierto", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const { user, campo } = await abrirComposer();

    expect(campo).toHaveAttribute("aria-expanded", "false");
    expect(campo).not.toHaveAttribute("aria-controls");

    await user.type(campo, "@algoritmos");
    const listbox = await screen.findByRole("listbox");

    expect(campo).toHaveAttribute("aria-expanded", "true");
    // El id (`useId`) puede llevar `:` — nunca como selector CSS, sólo comparado
    // como texto contra el ancestro que de verdad controla.
    const popover = listbox.closest(".adoc-asistente-menciones-popover");
    expect(popover?.id).toBe(campo.getAttribute("aria-controls"));
  });
});

describe("Escape cierra sólo el popover (spec: «Escape closes only the popover»)", () => {
  it("no cierra el modal, ni borra lo que ya se escribió", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    const cerrarModal = vi.fn();
    const user = userEvent.setup();
    montar(
      <div
        role="dialog"
        aria-label="Asistente"
        onKeyDown={(evento) => {
          if (evento.key === "Escape") cerrarModal();
        }}
      >
        <PanelDePrueba umbralDelIndicadorMs={0} />
      </div>,
    );
    const campo = await screen.findByLabelText("Tu pregunta");

    await user.type(campo, "@algoritmos");
    await screen.findByRole("option", { name: /Algoritmos/ });

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("option", { name: /Algoritmos/ })).toBeNull();
    expect(campo).toHaveValue("@algoritmos");
    expect(campo).toHaveFocus();
    // La capa frenó el Escape en captura: el `onKeyDown` del "modal" —que
    // escucha en burbujeo, más afuera— nunca lo recibió.
    expect(cerrarModal).not.toHaveBeenCalled();
  });

  it("también cierra sólo la pista, bajo 2 letras", async () => {
    const cerrarModal = vi.fn();
    const user = userEvent.setup();
    montar(
      <div
        role="dialog"
        aria-label="Asistente"
        onKeyDown={(evento) => {
          if (evento.key === "Escape") cerrarModal();
        }}
      >
        <PanelDePrueba umbralDelIndicadorMs={0} />
      </div>,
    );
    const campo = await screen.findByLabelText("Tu pregunta");

    await user.type(campo, "@a");
    await screen.findByText("Escribí al menos 2 letras para buscar materias.");

    await user.keyboard("{Escape}");

    expect(screen.queryByText(/Escribí al menos 2 letras/)).toBeNull();
    expect(cerrarModal).not.toHaveBeenCalled();
  });
});

describe("Una mención que dejó de estar disponible al enviar", () => {
  it("muestra el error del spec y deja el composer libre para elegir otra", async () => {
    vi.spyOn(mencionesApi, "buscarMenciones").mockResolvedValue({
      resultados: [ALGORITMOS],
      hayMas: false,
    });
    vi.spyOn(api, "consultar").mockRejectedValue(
      new AxiosError("Bad Request", "400", undefined, undefined, {
        status: 400,
        data: {
          title: "Mención no disponible",
          detail: "Una de las menciones ya no está disponible. Volvé a elegirla.",
        },
      } as never),
    );
    const { user, campo } = await abrirComposer();

    await user.type(campo, "@algoritmos");
    await user.click(await screen.findByRole("option", { name: /Algoritmos/ }));
    await user.type(campo, "algo");
    await user.keyboard("{Enter}");

    expect(
      await screen.findByText("Una de las menciones ya no está disponible. Volvé a elegirla."),
    ).toBeInTheDocument();

    // El composer no quedó bloqueado: se puede escribir y elegir una mención nueva.
    expect(campo).toHaveValue("");
    expect(campo).not.toBeDisabled();
  });
});
