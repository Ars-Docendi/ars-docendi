import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PanelDePrueba } from "./test/PanelDePrueba";
import { LanzadorAsistente } from "./components/LanzadorAsistente";
import * as api from "./api/asistenteApi";
import type { CapacidadesDelAsistente, RespuestaDelAsistente } from "./types";

// ------------------------------------------------------------------- fixtures

const CAPACIDADES: CapacidadesDelAsistente = {
  cubre: [
    { nombre: "designaciones.pedidos", descripcion: "Los pedidos del trámite.", columnas: 12 },
    { nombre: "identity.personas", descripcion: "El padrón de personas.", columnas: 5 },
  ],
  tablas: 2,
  columnas: 17,
  ejemplos: ["¿Qué carreras están vigentes?", "¿Cuántos pedidos hay en cada estado?"],
  noPuede: ["No modifica nada: solo consulta."],
  alcance: "Ves los datos de todo el Departamento.",
};

function respuesta(parcial: Partial<RespuestaDelAsistente> = {}): RespuestaDelAsistente {
  return {
    estado: "respondida",
    respuesta: "Hay 4 docentes designados.",
    hilo: "11111111-1111-4111-8111-111111111111",
    opciones: [],
    sugerencias: [],
    columnas: [],
    filas: [],
    truncado: false,
    metricas: { llamadasAlModelo: 2 },
    ...parcial,
  };
}

function montar(nodo: React.ReactNode) {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });

  return render(<QueryClientProvider client={cliente}>{nodo}</QueryClientProvider>);
}

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
    // EL GATE ES EL PERMISO REAL, NO UNA LISTA DE ROLES. Una lista embebida acá
    // fallaría ABIERTA con cualquier rol nuevo, e `identity.roles` no es un catálogo
    // cerrado: Secretaría puede crear roles desde la aplicación.
    vi.spyOn(api, "obtenerCapacidades").mockRejectedValue(new Error("403"));

    montar(<LanzadorAsistente />);

    await waitFor(() => expect(screen.queryByRole("button", { name: "Preguntar" })).toBeNull());
  });

  it("no deja ningún botón deshabilitado con leyenda de próximamente", async () => {
    // El botón «Ayuda» estaba `disabled` con title="Próximamente", que es el fake UI
    // que el invariante #7 prohíbe. Este componente lo reemplaza: o hay asistente, o
    // no hay botón.
    montar(<LanzadorAsistente />);

    const boton = await screen.findByRole("button", { name: "Preguntar" });

    expect(boton).not.toBeDisabled();

    // No lleva `title`: el botón ahora tiene etiqueta visible, y un tooltip que
    // repite lo que ya dice el texto es ruido para un lector de pantalla. Lo que
    // este test cuida no es el atributo sino la propiedad: ninguna promesa de algo
    // que todavía no funciona.
    expect(boton).not.toHaveAttribute("title", "Próximamente");
    expect(boton).toHaveAccessibleName("Preguntar");
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
  it("arranca mostrando el catálogo real y sus ejemplos", async () => {
    montar(<PanelDePrueba />);

    expect(await screen.findByText(/2 áreas de datos del sistema/)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "¿Qué carreras están vigentes?" }),
    ).toBeInTheDocument();
    expect(screen.getByText("No modifica nada: solo consulta.")).toBeInTheDocument();
  });

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

/**
 * Tope de espera para un turno que NO llama al modelo.
 *
 * Esos turnos se retienen a propósito para que se sientan como uno que sí llamó
 * —ver `utils/esperaPareja.ts`—, y el sorteo llega hasta 2,5 s. El default de
 * `findBy*` es 1 s, así que sin este tope la espera deliberada se leería como una
 * falla intermitente.
 */
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

  it("una respuesta sin modelo se retiene para que el turno no se sienta vacío", async () => {
    // Un carril determinista contesta en milisegundos. Sin la espera pareja, la
    // respuesta aparece antes de que el usuario suelte la tecla y se lee como que
    // el asistente no hizo nada. Acá se afirma que NO está todavía a los 400 ms
    // —el umbral del indicador— y que llega después.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({ respuesta: "Hola.", metricas: { llamadasAlModelo: 0 } }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "hola{Enter}");
    await new Promise((r) => setTimeout(r, 400));

    expect(screen.queryByText("Hola.")).not.toBeInTheDocument();
    expect(await screen.findByText("Hola.", undefined, TOPE_DE_ESPERA_PAREJA)).toBeInTheDocument();
  });

  it("un error no se retiene: la mala noticia llega enseguida", async () => {
    // La espera pareja empareja RESPUESTAS. Hacer esperar a alguien para decirle
    // que algo falló es coherencia que no vale lo que cuesta.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockRejectedValue(new Error("se cayó"));
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await new Promise((r) => setTimeout(r, 400));

    expect(screen.getByText("No se pudo consultar")).toBeInTheDocument();
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

  it("un rechazo ofrece sugerencias, presentadas distinto de las opciones", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        estado: "no_contestable",
        respuesta: "No puedo responder eso.",
        sugerencias: ["¿Qué carreras están vigentes?"],
        metricas: { llamadasAlModelo: 1 },
      }),
    );
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    // Las sugerencias no bloquean; las opciones sí. El texto las distingue.
    expect(await screen.findByText("Probá con alguna de estas:")).toBeInTheDocument();
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

  it("una respuesta rápida no llega a mostrar el indicador", async () => {
    // Un indicador que parpadea es peor que ninguno: para un lector de pantalla es
    // un anuncio que aparece y desaparece antes de terminar de leerse.
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(respuesta());
    montar(<PanelDePrueba umbralDelIndicadorMs={10_000} />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(screen.getByRole("status").textContent).toBe("");
  });

  it("una respuesta lenta sí lo muestra, anunciado como estado", async () => {
    const user = userEvent.setup();
    let resolver: (valor: RespuestaDelAsistente) => void = () => {};
    vi.spyOn(api, "consultar").mockImplementation(
      () => new Promise<RespuestaDelAsistente>((r) => (resolver = r)),
    );
    montar(<PanelDePrueba umbralDelIndicadorMs={0} />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "algo{Enter}");

    await waitFor(() => expect(screen.getByRole("status").textContent).toBe("Consultando…"));

    resolver(respuesta());

    await screen.findByText("Hay 4 docentes designados.");
    await waitFor(() => expect(screen.getByRole("status").textContent).toBe(""));
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
