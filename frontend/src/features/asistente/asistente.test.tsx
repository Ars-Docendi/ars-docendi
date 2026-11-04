import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDePrueba } from "./test/PanelDePrueba";
import { LanzadorAsistente } from "./components/LanzadorAsistente";
import * as api from "./api/asistenteApi";

// Las fixtures y el montaje son los de `test/soporte`, y no una copia local.
import { CAPACIDADES, montar, respuesta } from "./test/soporte";

// La línea de métricas sólo se monta en modo debug (decisión 14 del PO, 2026-09-26): este archivo
// no prueba ESE gate —lo hace `FranjaDeEstado.test.tsx`—, sino el contrato de accesibilidad de la
// línea una vez montada (fuera de la región viva), así que se fuerza el modo debug acá en vez de
// threadearlo como prop por todo `PanelAsistente` (que hoy no lo expone, a diferencia de
// `umbralDelIndicadorMs`).
vi.mock("./utils/modoDebug", () => ({ modoDebugAsistente: true }));

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

// --------------------------------------------------------------- el lanzador

describe("El lanzador del asistente", () => {
  it("aparece cuando el backend concede acceso", async () => {
    montar(<LanzadorAsistente />);

    expect(await screen.findByRole("button", { name: "Preguntar" })).toBeEnabled();
  });

  it("no aparece cuando el backend niega el acceso", async () => {
    // EL GATE ES EL PERMISO REAL, NO UNA LISTA DE ROLES.
    vi.spyOn(api, "obtenerCapacidades").mockRejectedValue(new Error("403"));

    montar(<LanzadorAsistente />);

    await waitFor(() => expect(screen.queryByRole("button", { name: "Preguntar" })).toBeNull());
  });

  it("abre el panel con la misma vista que la ruta", async () => {
    const user = userEvent.setup();
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));

    expect(
      await screen.findByRole("region", { name: "Asistente conversacional" }),
    ).toBeInTheDocument();
  });
});

// ----------------------------------------------------------- la conversación

describe("El panel del asistente", () => {
  it("manda la pregunta y muestra la respuesta", async () => {
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "¿cuántos docentes hay?");
    // «Enviar» y no «Preguntar»: con el modal abierto habría dos botones con el
    // mismo nombre en el DOM, el lanzador y éste.
    await user.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText("Hay 4 docentes designados.")).toBeInTheDocument();
    expect(consultar).toHaveBeenCalledOnce();
  });

  it("manda una clave de idempotencia distinta por turno", async () => {
    // Una clave por conversación haría que el segundo turno recibiera la respuesta
    // del primero, que es justo lo contrario de lo que la idempotencia busca.
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");

    await user.type(entrada, "primera{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(1));

    await user.type(entrada, "segunda{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(2));

    const [, primeraClave] = consultar.mock.calls[0];
    const [, segundaClave] = consultar.mock.calls[1];

    expect(primeraClave).not.toEqual(segundaClave);
  });

  it("arrastra el hilo al turno siguiente", async () => {
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");

    await user.type(entrada, "primera{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(1));
    expect(consultar.mock.calls[0][0].hilo).toBeNull();

    await user.type(entrada, "segunda{Enter}");
    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(2));
    expect(consultar.mock.calls[1][0].hilo).toBe("11111111-1111-4111-8111-111111111111");
  });
});

// ------------------------------------------------------------ los cuatro estados

/** Tope de espera para un turno que NO llama al modelo. */
const TOPE_DE_ESPERA_PAREJA = { timeout: 4000 };

describe("Los cuatro estados", () => {
  it("el degradado se muestra como aviso y no como error", async () => {
    // Un banner rojo le diría al usuario que hizo algo mal. Su pregunta no tiene
    // nada de malo: el proveedor se cayó.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        estado: "servicio_degradado",
        respuesta: "El asistente no está disponible en este momento.",
        metricas: { llamadasAlModelo: 0 },
      }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    expect(await screen.findByText("El asistente no está disponible ahora")).toBeInTheDocument();
  });

  it("una aclaración ofrece sus opciones para continuar", async () => {
    const user = userEvent.setup();
    const consultar = vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        estado: "necesita_aclaracion",
        respuesta: "¿Cuál de estas?",
        opciones: [
          { etiqueta: "Bases de Datos (Informática)", preguntaResuelta: "..." },
          { etiqueta: "Bases de Datos (Industrial)", preguntaResuelta: "..." },
        ],
        metricas: { llamadasAlModelo: 0 },
      }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    expect(
      await screen.findByText("Elegí una para continuar:", undefined, TOPE_DE_ESPERA_PAREJA),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Bases de Datos (Informática)" }));

    await waitFor(() => expect(consultar).toHaveBeenCalledTimes(2));
    expect(consultar.mock.calls[1][0].mensaje).toBe("Bases de Datos (Informática)");
  });

  it("un rechazo no ofrece ninguna sugerencia (ARS-149)", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        estado: "no_contestable",
        respuesta: "No puedo responder eso.",
        metricas: { llamadasAlModelo: 1 },
      }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    await screen.findByText("No puedo responder eso.");

    // Ya no hay un segundo campo de "próximos pasos" para un rechazo: el texto
    // del rechazo es toda la respuesta.
    expect(screen.queryByText("Probá con alguna de estas:")).toBeNull();
    expect(screen.queryByText("Elegí una para continuar:")).toBeNull();
  });

  it("muestra la tabla y avisa del truncado sin decir cuántas filas faltan", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        respuesta: "Encontré varios docentes.",
        columnas: [
          { nombre: "apellido", sensible: false },
          { nombre: "documento", sensible: true },
        ],
        filas: [["Gómez", "28341567"]],
        truncado: true,
      }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    // El valor sensible SÍ llega al usuario: nunca viajó al modelo, viene del motor.
    expect(await screen.findByText("28341567")).toBeInTheDocument();

    const aviso = screen.getByText(/Hay más resultados/);
    expect(aviso).toBeInTheDocument();
    // Sin números: «ves 3 de 124» es un canal de inferencia sobre datos que el
    // usuario no puede ver.
    expect(aviso.textContent).not.toMatch(/\d/);
  });

  it("un fallo de red se muestra en español y sin códigos técnicos", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockRejectedValue(new Error("Network Error"));
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    const alerta = await screen.findByText(/No pude completar la consulta/);

    expect(alerta).toBeInTheDocument();
    expect(alerta.textContent).not.toMatch(/50\d|Network|Error:/);
  });
});

// -------------------------------------------------------------- accesibilidad

describe("Accesibilidad de la conversación", () => {
  it("la lista de mensajes es una región viva con rol de registro", async () => {
    montar(<PanelDePrueba />);

    const log = await screen.findByRole("log", { name: "Conversación con el asistente" });

    expect(log).toHaveAttribute("aria-live", "polite");
  });

  it("la línea de métricas queda FUERA de la región viva", async () => {
    // ES EL DEFECTO VERIFICADO DEL PROTOTIPO PREVIO: la región envolvía el
    // contenedor entero, así que cada re-render hacía que el lector leyera todo de
    // nuevo, métricas incluidas — y las métricas cambian en cada turno.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    const log = screen.getByRole("log");
    const metricas = screen.getByText(/consultas al modelo/);

    expect(metricas).toBeInTheDocument();
    expect(within(log).queryByText(/consultas al modelo/)).toBeNull();
    expect(log.contains(metricas)).toBe(false);
  });

  it("el indicador de proceso queda fuera de la región viva y es un estado", async () => {
    montar(<PanelDePrueba />);

    const estado = await screen.findByRole("status");
    const log = screen.getByRole("log");

    expect(log.contains(estado)).toBe(false);
  });

  it("el foco vuelve al campo de entrada cuando llega la respuesta", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(entrada).toHaveFocus();
  });

  it("el foco vuelve al campo también después de elegir una opción", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "consultar")
      .mockResolvedValueOnce(
        respuesta({
          estado: "necesita_aclaracion",
          respuesta: "¿Cuál?",
          opciones: [{ etiqueta: "Una", preguntaResuelta: "..." }],
          metricas: { llamadasAlModelo: 0 },
        }),
      )
      .mockResolvedValue(respuesta());
    montar(<PanelDePrueba />);

    const entrada = await screen.findByLabelText("Tu pregunta");
    await user.type(entrada, "algo{Enter}");

    await user.click(await screen.findByRole("button", { name: "Una" }, TOPE_DE_ESPERA_PAREJA));
    await screen.findByText("Hay 4 docentes designados.");

    expect(entrada).toHaveFocus();
  });
});
