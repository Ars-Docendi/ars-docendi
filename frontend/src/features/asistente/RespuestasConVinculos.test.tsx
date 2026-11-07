import { describe, it, expect, vi, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useLocation } from "react-router-dom";

import { Mensaje } from "./components/Mensaje";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar, respuesta } from "./test/soporte";
import { PanelDePrueba } from "./test/PanelDePrueba";
import type { PrincipalDeLaRespuesta, RespuestaDelAsistente, TurnoDeLaConversacion } from "./types";

// ============================================================
// Respuestas que llevan al sistema (asistente-respuestas-con-vinculos):
// chips en el texto, ficha del elemento principal, procedencia y
// «Repetir consulta».
// ============================================================

afterEach(() => {
  vi.restoreAllMocks();
});

const TEXTO = "El pedido 2026-9005 está en revisión.";
const DESDE = TEXTO.indexOf("2026-9005");

const PRINCIPAL: PrincipalDeLaRespuesta = {
  tipo: "pedido-designacion",
  id: "abc-123",
  datos: {
    numero: "2026-9005",
    novedad: "Cambio de cargo o dedicación",
    materia: "Ingeniería de Software",
    estado: "en_revision_coordinador",
    estadoEtiqueta: "En Coordinación",
    periodo: "2026 – 1.er cuatrimestre",
    cargo: "Profesor adjunto",
    ultimaActividad: "2026-03-01T15:00:00Z",
  },
};

const CON_TABLA: Partial<RespuestaDelAsistente> = {
  respuesta: TEXTO,
  columnas: [{ nombre: "numero", sensible: false }],
  filas: [["2026-9005"]],
  vinculos: [{ fila: 0, columna: 0, tipo: "pedido-designacion", id: "abc-123" }],
};

function Ubicacion() {
  return <p data-testid="ubicacion">{useLocation().pathname}</p>;
}

function montarRespuesta(parcial: Partial<RespuestaDelAsistente>) {
  const turno: TurnoDeLaConversacion = {
    id: "t-1",
    pregunta: "¿cómo está el 9005?",
    respuesta: respuesta(parcial),
  };
  return montar(
    <>
      <ul>
        <Mensaje
          turno={turno}
          onElegir={() => {}}
          onReintentar={() => {}}
          enVuelo={false}
          esUltimo
        />
      </ul>
      <Ubicacion />
    </>,
  );
}

describe("Los chips del texto", () => {
  it("pintan el valor como enlace con su nombre accesible, y navegan", async () => {
    const user = userEvent.setup();
    montarRespuesta({
      respuesta: TEXTO,
      vinculosDelTexto: [{ desde: DESDE, largo: 9, tipo: "pedido-designacion", id: "abc-123" }],
    });

    const chip = screen.getByRole("link", { name: "Ver el trámite 2026-9005" });
    expect(chip).toHaveAttribute("data-vinculo-del-asistente");
    // El texto de alrededor queda tal cual.
    expect(chip.closest("p")).toHaveTextContent(TEXTO);

    await user.click(chip);
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/designaciones/pedidos/abc-123");
  });

  it("un segmento que no entra en el texto o se superpone se pinta como texto", () => {
    montarRespuesta({
      respuesta: TEXTO,
      vinculosDelTexto: [
        { desde: DESDE, largo: 9, tipo: "pedido-designacion", id: "a" },
        { desde: DESDE + 3, largo: 4, tipo: "pedido-designacion", id: "b" },
        { desde: TEXTO.length - 2, largo: 10, tipo: "pedido-designacion", id: "c" },
        { desde: -1, largo: 3, tipo: "pedido-designacion", id: "d" },
      ],
    });

    expect(screen.getAllByRole("link")).toHaveLength(1);
    expect(screen.getByText(/está en revisión\./).closest("p")).toHaveTextContent(TEXTO);
  });

  it("un tipo que el cliente no conoce se pinta como texto", () => {
    montarRespuesta({
      respuesta: TEXTO,
      vinculosDelTexto: [{ desde: DESDE, largo: 9, tipo: "perfil-de-portal", id: "x" }],
    });

    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getByText(TEXTO)).toBeInTheDocument();
  });
});

describe("La ficha del elemento principal", () => {
  it("muestra la ficha, sin chips ni tabla debajo, y la tabla sigue en «Ampliar tabla»", async () => {
    const user = userEvent.setup();
    montarRespuesta({
      ...CON_TABLA,
      principal: PRINCIPAL,
      // Con principal los chips no se pintan aunque el backend mande posiciones.
      vinculosDelTexto: [{ desde: DESDE, largo: 9, tipo: "pedido-designacion", id: "abc-123" }],
    });

    expect(screen.getByText("Pedido de designación")).toBeInTheDocument();
    expect(screen.getByText("2026-9005 · Ingeniería de Software")).toBeInTheDocument();
    expect(screen.getByText("En Coordinación")).toBeInTheDocument();
    expect(screen.getByText("Cambio de cargo o dedicación")).toBeInTheDocument();
    expect(screen.getByText("Profesor adjunto")).toBeInTheDocument();
    expect(screen.getByText("01/03/2026")).toBeInTheDocument();
    // Dedicación no llegó: no hay fila.
    expect(screen.queryByText("Dedicación")).toBeNull();

    // El único enlace es el de la ficha; el texto no tiene chips.
    const enlaces = screen.getAllByRole("link");
    expect(enlaces).toHaveLength(1);
    expect(enlaces[0]).toHaveTextContent("Abrir pedido");
    expect(screen.queryByRole("table")).toBeNull();

    await user.click(screen.getByRole("button", { name: "Ampliar tabla" }));
    expect(await screen.findByRole("table")).toBeInTheDocument();
  });

  it("«Abrir pedido» navega al detalle", async () => {
    const user = userEvent.setup();
    montarRespuesta({ ...CON_TABLA, principal: PRINCIPAL });

    const abrir = screen.getByRole("link", { name: /Abrir pedido/ });
    expect(abrir).toHaveAttribute("data-vinculo-del-asistente");
    await user.click(abrir);

    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/designaciones/pedidos/abc-123");
  });

  // Dentro del modal, `.adoc-asistente-modal header` colapsa a alto 0 cualquier
  // `<header>` y `.adoc-modal footer` pinta cualquier `<footer>` como el pie del
  // modal: con esos elementos la cabecera de la ficha desaparecía en la app real.

  it("un principal de un tipo desconocido cae a la presentación de hoy: tabla y chips", () => {
    montarRespuesta({
      ...CON_TABLA,
      principal: { tipo: "perfil-de-portal", id: "x", datos: {} },
      vinculosDelTexto: [{ desde: DESDE, largo: 9, tipo: "pedido-designacion", id: "abc-123" }],
    });

    expect(screen.queryByText("Pedido de designación")).toBeNull();
    expect(screen.getByRole("table")).toBeInTheDocument();
    // El chip del texto y la celda de la tabla.
    expect(screen.getAllByRole("link", { name: "Ver el trámite 2026-9005" })).toHaveLength(2);
  });
});

describe("La procedencia de los datos", () => {
  it("nombra el área y nunca muestra cantidades", () => {
    montarRespuesta({ ...CON_TABLA, origenes: ["designaciones"] });

    const linea = screen.getByText("Datos de Designaciones");
    expect(linea.textContent).not.toMatch(/\d/);
  });
});

describe("«Repetir consulta»", () => {
  function montarTurno(props: Partial<React.ComponentProps<typeof Mensaje>> = {}) {
    const alRepetir = vi.fn();
    userEvent.setup();
    montar(
      <ul>
        <Mensaje
          turno={{ id: "t-1", pregunta: "¿cuántos docentes hay?", respuesta: respuesta() }}
          onElegir={() => {}}
          onReintentar={() => {}}
          enVuelo={false}
          esUltimo
          onEditarYReenviar={alRepetir}
          {...props}
        />
      </ul>,
    );
    return alRepetir;
  }

  it("reenvía la pregunta tal cual", async () => {
    const user = userEvent.setup();
    const alRepetir = montarTurno();

    await user.click(screen.getByRole("button", { name: "Repetir consulta" }));

    expect(alRepetir).toHaveBeenCalledWith("¿cuántos docentes hay?");
  });

  it("no aparece en un turno que no es el último, con cupo agotado, en vuelo ni sin respuesta", () => {
    montarTurno({ esUltimo: false, onEditarYReenviar: undefined });
    expect(screen.queryByRole("button", { name: "Repetir consulta" })).toBeNull();
  });

  it("no aparece con el asistente bloqueado", () => {
    montarTurno({ bloqueado: true });
    expect(screen.queryByRole("button", { name: "Repetir consulta" })).toBeNull();
  });

  it("no aparece con un turno en vuelo", () => {
    montarTurno({ enVuelo: true });
    expect(screen.queryByRole("button", { name: "Repetir consulta" })).toBeNull();
  });

  it("no aparece en un turno restaurado del historial ni en uno con error", () => {
    montarTurno({
      turno: {
        id: "t-1",
        pregunta: "¿cuántos docentes hay?",
        historico: { estado: "respondida" },
      } as TurnoDeLaConversacion,
    });
    expect(screen.queryByRole("button", { name: "Repetir consulta" })).toBeNull();
  });

  it("de punta a punta reemplaza el turno: la misma pregunta con una clave nueva", async () => {
    const user = userEvent.setup();
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
    const consultar = vi
      .spyOn(api, "consultar")
      .mockResolvedValueOnce(respuesta({ respuesta: "Hay 4 docentes." }))
      .mockResolvedValueOnce(respuesta({ respuesta: "Ahora hay 5 docentes." }));
    montar(<PanelDePrueba />);

    await user.type(await screen.findByLabelText("Tu pregunta"), "¿cuántos docentes hay?{Enter}");
    await screen.findByText("Hay 4 docentes.");

    await user.click(screen.getByRole("button", { name: "Repetir consulta" }));
    await screen.findByText("Ahora hay 5 docentes.");

    expect(screen.queryByText("Hay 4 docentes.")).toBeNull();
    const [primerPedido, primeraClave] = consultar.mock.calls[0];
    const [segundoPedido, segundaClave] = consultar.mock.calls[1];
    expect(segundoPedido.mensaje).toBe(primerPedido.mensaje);
    expect(segundoPedido.reemplaza).toBe(primeraClave);
    expect(segundaClave).not.toBe(primeraClave);
    // Al ser el único turno que queda, no se acumulan preguntas.
    expect(within(screen.getByRole("log")).getAllByText(/¿cuántos docentes hay\?/)).toHaveLength(1);
  });
});

describe("Docentes y solicitudes de aulas", () => {
  const DOCENTES: Partial<RespuestaDelAsistente> = {
    respuesta: "Encontré 2 docentes.",
    columnas: [
      { nombre: "apellido", sensible: false },
      { nombre: "nombre", sensible: false },
    ],
    filas: [
      ["Lovelace", "Ada"],
      ["Hopper", "Grace"],
    ],
  };

  it("una lista enlaza cada nombre a su perfil y sugiere tocarlo", async () => {
    const user = userEvent.setup();
    montarRespuesta({
      ...DOCENTES,
      vinculos: [
        { fila: 0, columna: 0, tipo: "persona", id: "p-1" },
        { fila: 1, columna: 0, tipo: "persona", id: "p-2" },
      ],
    });

    expect(screen.getByText("Tocá un nombre para ver su perfil")).toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "Ver el perfil de Hopper" }));
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/personas/p-2");
  });

  it("sin vínculos los nombres son texto y no hay sugerencia", () => {
    montarRespuesta({ ...DOCENTES, vinculos: [] });

    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.queryByText("Tocá un nombre para ver su perfil")).toBeNull();
  });

  it("un único docente se muestra en su tarjeta, con «Ver perfil»", async () => {
    const user = userEvent.setup();
    montarRespuesta({
      ...DOCENTES,
      principal: {
        tipo: "persona",
        id: "p-1",
        datos: {
          apellido: "Lovelace",
          nombre: "Ada",
          designaciones: "Algoritmos · Titular · Ingeniería en Informática\nÁlgebra · Adjunto",
        },
      },
    });

    expect(screen.getByText("Lovelace, Ada")).toBeInTheDocument();
    expect(
      screen.getByText("Algoritmos · Titular · Ingeniería en Informática"),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: /Ver perfil/ }));
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/personas/p-1");
  });

  it("el identificador de una solicitud enlaza a su detalle", async () => {
    const user = userEvent.setup();
    montarRespuesta({
      respuesta: "Hay una solicitud.",
      columnas: [{ nombre: "comision", sensible: false }],
      filas: [["Comisión 02"]],
      vinculos: [{ fila: 0, columna: 0, tipo: "solicitud-reserva-aula", id: "s-9" }],
    });

    await user.click(screen.getByRole("link", { name: /Ver el detalle de la solicitud/ }));
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/aulas/solicitudes/s-9");
  });
});
