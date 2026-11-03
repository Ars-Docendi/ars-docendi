import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { Mensaje } from "./components/Mensaje";
import * as api from "./api/asistenteApi";
import * as descargas from "./utils/descargas";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { RespuestaDelAsistente, TurnoDeLaConversacion } from "./types";

// ============================================================
// Un mensaje de la conversación: el razonamiento colapsado y lo que nunca se
// muestra.
//
// El backend redacta `razonamiento` para el usuario final —una o dos oraciones en
// español, sin nombres de tablas ni de columnas— y lo omite cuando está vacío. Va
// dentro del mensaje como un `<details>` cerrado: es parte de la respuesta, así
// que vive en la región viva, pero el contenido de una disclosure cerrada no se
// anuncia hasta abrirla, y por eso no le agrega ruido al lector.
// `preguntaInterpretada` queda visible y afuera: es el aviso de que la pregunta se
// reinterpretó, y esconderlo derrota su razón de ser (RF-10).
// ============================================================

const RAZONAMIENTO = "Busqué los docentes con designación vigente.";

function turno(parcial: Partial<RespuestaDelAsistente> = {}): TurnoDeLaConversacion {
  return { id: "t-1", pregunta: "¿cuántos docentes hay?", respuesta: respuesta(parcial) };
}

function montarMensaje(unTurno: TurnoDeLaConversacion, opciones: { debug?: boolean } = {}) {
  const { debug = false } = opciones;
  return montar(
    <ul>
      <Mensaje
        turno={unTurno}
        onElegir={() => {}}
        onReintentar={() => {}}
        enVuelo={false}
        debug={debug}
      />
    </ul>,
  );
}

describe("El razonamiento", () => {
  // Sólo se muestra con el modo debug prendido (`VITE_ASISTENTE_DEBUG=true`,
  // asistente-razonamiento-solo-en-debug): el backend lo sigue mandando en la
  // respuesta igual, pero el cliente decide si lo renderiza.
  it("con razonamiento y debug prendido hay una disclosure «Cómo lo interpreté», cerrada", () => {
    montarMensaje(turno({ razonamiento: RAZONAMIENTO }), { debug: true });

    const resumen = screen.getByText("Cómo lo interpreté");
    expect(resumen.tagName).toBe("SUMMARY");

    const disclosure = resumen.closest("details");
    expect(disclosure).not.toBeNull();
    expect(disclosure).not.toHaveAttribute("open");
    // Cerrada: el texto está en el DOM, pero no se ve ni se anuncia hasta abrirla.
    expect(screen.getByText(RAZONAMIENTO)).not.toBeVisible();
  });

  it("al abrirla se lee el razonamiento", async () => {
    const user = userEvent.setup();
    montarMensaje(turno({ razonamiento: RAZONAMIENTO }), { debug: true });

    await user.click(screen.getByText("Cómo lo interpreté"));

    expect(screen.getByText(RAZONAMIENTO)).toBeVisible();
  });

  it("con razonamiento pero debug apagado no hay disclosure", () => {
    montarMensaje(turno({ razonamiento: RAZONAMIENTO }), { debug: false });

    expect(screen.queryByText("Cómo lo interpreté")).toBeNull();
  });

  it("sin razonamiento no hay disclosure, aunque el debug esté prendido", () => {
    montarMensaje(turno(), { debug: true });

    expect(screen.queryByText("Cómo lo interpreté")).toBeNull();
  });

  it("«Entendí:» queda visible, fuera de la disclosure", () => {
    montarMensaje(
      turno({
        preguntaInterpretada: "¿Cuántos docentes tienen designación vigente?",
        razonamiento: RAZONAMIENTO,
      }),
      { debug: true },
    );

    const entendi = screen.getByText(/Entendí:/);
    expect(entendi).toBeVisible();
    expect(screen.getByText("¿Cuántos docentes tienen designación vigente?")).toBeVisible();

    const disclosure = screen.getByText("Cómo lo interpreté").closest("details");
    expect(disclosure?.contains(entendi)).toBe(false);
  });

  it("«Entendí:» queda visible aunque el debug esté apagado y no haya disclosure", () => {
    montarMensaje(
      turno({
        preguntaInterpretada: "¿Cuántos docentes tienen designación vigente?",
        razonamiento: RAZONAMIENTO,
      }),
      { debug: false },
    );

    expect(screen.getByText(/Entendí:/)).toBeVisible();
    expect(screen.queryByText("Cómo lo interpreté")).toBeNull();
  });
});

describe("Lo que nunca se muestra", () => {
  beforeEach(() => {
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("ninguna etiqueta interna llega al texto de la página", async () => {
    // Lo que el backend manda de verdad: `categoria` sigue viajando aunque el tipo del cliente ya no la declare.
    const METRICAS_DEL_BACKEND = { llamadasAlModelo: 2, categoria: "consulta_simple" };
    const user = userEvent.setup();
    vi.spyOn(api, "consultar").mockResolvedValue(
      respuesta({
        estado: "respondida",
        razonamiento: "Conté las designaciones vigentes.",
        metricas: METRICAS_DEL_BACKEND,
      }),
    );
    montar(<PanelDePrueba />);

    // CON LA AYUDA ABIERTA. El conteo y las áreas viven ahí desde que la pantalla vacía se limpió,
    // y con ella cerrada esta aserción pasaría sin verificar nada: los nombres internos no
    // aparecerían porque no se renderizó el bloque, no porque se los haya excluido.
    await user.click(
      await screen.findByRole("button", {
        name: "Qué puede y qué no puede hacer el asistente",
      }),
    );
    await screen.findByText(/áreas de datos del sistema/);
    expect(document.body.textContent).not.toMatch(/designaciones\.|identity\./);

    await user.type(screen.getByLabelText("Tu pregunta"), "algo{Enter}");
    await screen.findByText("Hay 4 docentes designados.");

    expect(document.body.textContent).not.toMatch(
      /consulta_simple|respondida|designaciones\.|identity\./,
    );
    // Tampoco como clase: `estado-respondida` era la etiqueta cruda en el DOM sin
    // que ninguna regla de la hoja la usara.
    expect(document.querySelector('[class*="estado-"]')).toBeNull();
  });
});

// ============================================================
// Sin sugerencias en ningún estado (ARS-140, ARS-149).
//
// El turno ya no lleva un campo de sugerencias — `Sugerencias.tsx` y su bloque
// se borraron con el rediseño v3. Este test no depende de ese componente, que
// ya no existe: cubre que ningún estado deje una sección de "próximos pasos"
// donde antes vivían las sugerencias, sea cual sea el texto del rechazo o de
// la respuesta.
// ============================================================

describe("Sin sugerencias en ningún estado", () => {
  it.each<RespuestaDelAsistente["estado"]>([
    "respondida",
    "no_contestable",
    "necesita_aclaracion",
    "servicio_degradado",
  ])("el estado %s no ofrece ninguna sugerencia", (estado) => {
    montarMensaje(turno({ estado, respuesta: "Un texto cualquiera." }));

    expect(screen.queryByText("Probá con alguna de estas:")).toBeNull();
    expect(document.querySelector(".adoc-asistente-sugerencias")).toBeNull();
  });
});

// ------------------------------------------------------------------- copiar

const TABLA: Partial<RespuestaDelAsistente> = {
  columnas: [
    { nombre: "apellido", sensible: false },
    { nombre: "horas", sensible: false },
  ],
  filas: [["Gómez", 42]],
};

describe("Copiar", () => {
  // `userEvent.setup()` instala un portapapeles de mentira en `navigator`: lo
  // que se escribe se puede leer después.
  it("«Copiar respuesta» deja el texto en el portapapeles y confirma con un tilde", async () => {
    const user = userEvent.setup();
    montarMensaje(turno());

    await user.click(screen.getByRole("button", { name: "Copiar respuesta" }));

    expect(await navigator.clipboard.readText()).toBe("Hay 4 docentes designados.");
    // El nombre accesible pasa a «Copiado» —el ícono también cambia a un tilde, pero eso no se
    // puede afirmar por accesibilidad— durante la confirmación (asistente-rediseno-v3, design.md D6).
    expect(await screen.findByRole("button", { name: "Copiado" })).toBeInTheDocument();
  });

  // «Copiar tabla» de la barra de acciones se retira en v3: el juego de íconos de la barra es
  // «Copiar respuesta» / «Ampliar tabla» / «Exportar a CSV» | «Sirvió» / «No sirvió»
  // (asistente-superficie-frontend, design.md D6).

  it("montar con tabla no descarga nada por su cuenta, y «Exportar a CSV» sí exporta lo mostrado", async () => {
    // REGRESIÓN: `TablaDeResultado` expone su función de exportar a `Mensaje` vía
    // `onExportarDisponible`, y `Mensaje` la guarda con `useState`.
    const disparo = vi.spyOn(descargas, "descargarArchivo").mockImplementation(() => {});
    const user = userEvent.setup();
    montarMensaje(turno(TABLA));

    expect(disparo).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Exportar a CSV" }));

    expect(disparo).toHaveBeenCalledTimes(1);
    const contenido = disparo.mock.calls[0][1];
    expect(contenido).toContain("Gómez");

    disparo.mockRestore();
  });

  it("sin portapapeles no hay botón de copiar, pero la tabla sigue con sus propios íconos", () => {
    // Un contexto sin portapapeles —http sin TLS, un navegador viejo— no puede copiar nada: un
    // botón que falla al pulsarlo es fake UI, así que no se renderiza ninguno.
    const original = Object.getOwnPropertyDescriptor(navigator, "clipboard");
    Object.defineProperty(navigator, "clipboard", { value: undefined, configurable: true });

    try {
      montarMensaje(turno(TABLA));

      expect(screen.queryByRole("button", { name: /Copiar/ })).toBeNull();
      expect(screen.getByRole("button", { name: "Ampliar tabla" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Exportar a CSV" })).toBeInTheDocument();
      expect(screen.getByText("Hay 4 docentes designados.")).toBeVisible();
    } finally {
      if (original) Object.defineProperty(navigator, "clipboard", original);
      else Reflect.deleteProperty(navigator, "clipboard");
    }
  });
});

// ============================================================
// La etiqueta de quién habla.
//
// «Vos» y «Asistente» son etiquetas para lectores de pantalla: la burbuja y la
// alineación dicen quién habló a quien ve, y un lector de pantalla no las tiene.
// Están ocultas con `clip-path`, así que existen en el DOM aunque no se vean —y
// eso las mete en dos caminos que nadie miró.
// ============================================================
