import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { useCurrentUser } from "../../shared/auth/useCurrentUser";
import type { CurrentUserState } from "../../shared/auth/useCurrentUser";
import * as api from "./api/administracionAsistenteApi";
import { PanelDeCalidad } from "./components/PanelDeCalidad";
import { etiquetaDeCausa } from "./utils/calidad";
import type {
  CalidadAgregada,
  PaginaDeProblemas,
  ProblemaDeCalidad,
  ResumenDeCalidad,
} from "./types";

vi.mock("../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

function conPermisos(permissions: string[]) {
  const estado: CurrentUserState = {
    user: {
      id: "00000000-0000-4000-8000-000000000001",
      name: "x",
      initials: "X",
      upn: "x@unlam.edu.ar",
      role: "r",
      roleCode: "r",
      permissions,
    },
    isLoading: false,
    error: null,
    retry: () => undefined,
  };
  vi.mocked(useCurrentUser).mockReturnValue(estado);
}

function resumen(parcial: Partial<ResumenDeCalidad> = {}): ResumenDeCalidad {
  return {
    turnos: 200,
    votos: 80,
    positivos: 60,
    negativos: 20,
    respondidas: 180,
    conProblemas: 30,
    fallosDetectables: 10,
    tasaNegativa: 0.25,
    tasaConProblemas: 0.15,
    tasaDeFallos: 0.05,
    cobertura: 0.4444,
    ...parcial,
  };
}

function calidad(parcial: Partial<CalidadAgregada> = {}): CalidadAgregada {
  return {
    periodoDias: 30,
    modelo: null,
    desde: "2026-09-25",
    hasta: "2026-10-24",
    porSemana: false,
    resumen: resumen(),
    tendencia: { tasaAnterior: 0.2, variacion: 0.05, votosAnteriores: 70 },
    motivoSinTendencia: null,
    negativosPorMotivo: [
      { clave: "datos_incorrectos", cantidad: 12 },
      { clave: "sin_motivo", cantidad: 3 },
    ],
    fallosPorCausa: [
      { clave: "timeout_proveedor", cantidad: 6, familia: "proveedor" },
      { clave: "resultado_vacio", cantidad: 9, familia: "senal" },
    ],
    denegacionesDePolitica: [{ clave: "cupo_agotado", cantidad: 4, familia: "politica" }],
    porModelo: [
      { modelo: "local/gpt-oss:20b", resumen: resumen() },
      { modelo: "anthropic/claude-sonnet-5", resumen: resumen({ turnos: 40, tasaNegativa: null }) },
    ],
    modelosDisponibles: ["anthropic/claude-sonnet-5", "local/gpt-oss:20b"],
    serie: [
      { dia: "2026-10-23", turnos: 10, votos: 4, negativos: 1, tasaNegativa: 0.25 },
      { dia: "2026-10-24", turnos: 12, votos: 5, negativos: 2, tasaNegativa: 0.4 },
    ],
    ...parcial,
  };
}

function fila(parcial: Partial<ProblemaDeCalidad> = {}): ProblemaDeCalidad {
  return {
    tipo: "negativa",
    pregunta: "¿Cuántas aulas hay?",
    motivo: "faltan_datos,otro",
    modelo: "local/gpt-oss:20b",
    dia: "2026-10-24",
    estado: "Respondida",
    ...parcial,
  };
}

function pagina(parcial: Partial<PaginaDeProblemas> = {}): PaginaDeProblemas {
  return { pagina: 1, tamanoDePagina: 25, total: 1, filas: [fila()], ...parcial };
}

function montar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={cliente}>
      <PanelDeCalidad />
    </QueryClientProvider>,
  );
}

const PERMISOS_COMPLETOS = ["asistente.administrar", "asistente.revisar_calidad"];

let espiaCalidad: MockInstance<typeof api.obtenerCalidad>;
let espiaProblemas: MockInstance<typeof api.obtenerProblemasDeCalidad>;

beforeEach(() => {
  conPermisos(PERMISOS_COMPLETOS);
  espiaCalidad = vi.spyOn(api, "obtenerCalidad").mockResolvedValue(calidad());
  espiaProblemas = vi.spyOn(api, "obtenerProblemasDeCalidad").mockResolvedValue(pagina());
});

afterEach(() => vi.restoreAllMocks());

describe("PanelDeCalidad — KPIs", () => {
  it("«Respuestas negativas» va primero, con el énfasis, y las demás tarjetas siguen en orden", async () => {
    montar();

    const fila = await screen.findByRole("region", { name: "Indicadores de calidad" });
    const etiquetas = Array.from(fila.querySelectorAll(".adoc-asistente-admin-kpi-etiqueta")).map(
      (e) => e.textContent,
    );
    expect(etiquetas).toEqual([
      "Respuestas negativas",
      "Con problemas",
      "Fallos detectables",
      "Positivas",
      "Cobertura de voto",
    ]);
    const primera = fila.querySelector(".adoc-asistente-admin-kpi") as HTMLElement;
    expect(primera).toHaveClass("calidad-kpi--negativa");
    expect(within(primera).getByText("25,0%")).toBeInTheDocument();
    expect(within(primera).getByText(/20 de 80 valoraciones/)).toBeInTheDocument();
    // Es-AR, one decimal; coverage 0.4444 rounds to 44,4%.
    expect(within(fila).getByText("44,4%")).toBeInTheDocument();
    expect(within(fila).getByText("75,0%")).toBeInTheDocument();
  });

  it("tendencia en alza: nota ámbar con los puntos de diferencia", async () => {
    montar();

    const nota = await screen.findByTestId("tendencia-negativas");
    expect(nota).toHaveTextContent("▲ 5,0 pts contra el período anterior");
    expect(nota).toHaveClass("adoc-asistente-admin-kpi-nota--advertencia");
  });

  it("tendencia a la baja: nota sin el énfasis de advertencia", async () => {
    espiaCalidad.mockResolvedValue(
      calidad({ tendencia: { tasaAnterior: 0.3, variacion: -0.052, votosAnteriores: 70 } }),
    );
    montar();

    const nota = await screen.findByTestId("tendencia-negativas");
    expect(nota).toHaveTextContent("▼ 5,2 pts contra el período anterior");
    expect(nota).not.toHaveClass("adoc-asistente-admin-kpi-nota--advertencia");
  });

  it.each([
    ["pocos_votos", "Pocas valoraciones para comparar"],
    ["fuera_de_retencion", "El período anterior ya no se conserva"],
  ] as const)("sin tendencia (%s) muestra el motivo", async (motivo, texto) => {
    espiaCalidad.mockResolvedValue(calidad({ tendencia: null, motivoSinTendencia: motivo }));
    montar();

    const nota = await screen.findByTestId("tendencia-negativas");
    expect(nota).toHaveTextContent(texto);
    expect(nota).not.toHaveClass("adoc-asistente-admin-kpi-nota--advertencia");
  });

  it("una tasa sin denominador se ve como guion, no como 0%", async () => {
    espiaCalidad.mockResolvedValue(
      calidad({
        resumen: resumen({
          votos: 0,
          positivos: 0,
          negativos: 0,
          tasaNegativa: null,
          cobertura: null,
        }),
        tendencia: null,
        motivoSinTendencia: "pocos_votos",
      }),
    );
    montar();

    const primera = (await screen.findByRole("region", { name: "Indicadores de calidad" }))
      .firstElementChild as HTMLElement;
    expect(within(primera).getByText("—")).toBeInTheDocument();
    expect(within(primera).queryByText("0,0%")).not.toBeInTheDocument();
  });
});

describe("PanelDeCalidad — filtros de período y modelo", () => {
  it("arranca en 30 días y pide el período elegido, reiniciando a «todos los modelos»", async () => {
    const user = userEvent.setup();
    montar();

    await screen.findByRole("region", { name: "Indicadores de calidad" });
    expect(espiaCalidad).toHaveBeenLastCalledWith(30, undefined);
    expect(screen.getByRole("button", { name: "30 días" })).toHaveAttribute("aria-pressed", "true");

    await user.click(screen.getByRole("button", { name: "90 días" }));
    await waitFor(() => expect(espiaCalidad).toHaveBeenLastCalledWith(90, undefined));
  });

  it("el selector lista los modelos disponibles y filtra ambos pedidos", async () => {
    const user = userEvent.setup();
    montar();
    await screen.findByRole("region", { name: "Indicadores de calidad" });

    const selector = screen.getByRole("combobox", { name: "Modelo" });
    expect(
      within(selector)
        .getAllByRole("option")
        .map((o) => o.textContent),
    ).toEqual(["Todos los modelos", "anthropic/claude-sonnet-5", "local/gpt-oss:20b"]);

    await user.selectOptions(selector, "local/gpt-oss:20b");

    await waitFor(() => expect(espiaCalidad).toHaveBeenLastCalledWith(30, "local/gpt-oss:20b"));
    await waitFor(() =>
      expect(espiaProblemas).toHaveBeenLastCalledWith(30, 1, "local/gpt-oss:20b", undefined),
    );
  });
});

describe("PanelDeCalidad — gráfico", () => {
  it("tiene etiqueta accesible con el resumen y una tabla equivalente", async () => {
    const user = userEvent.setup();
    montar();

    const grafico = await screen.findByRole("img", {
      name: /Porcentaje de respuestas negativas por día, del 23\/10 al 24\/10\. Promedio 25,0%\./,
    });
    expect(grafico).toBeInTheDocument();
    expect(screen.queryByRole("table", { name: /Porcentaje de respuestas negativas/ })).toBeNull();

    await user.click(screen.getByRole("button", { name: "Ver como tabla" }));

    const tabla = screen.getByRole("table", { name: /Porcentaje de respuestas negativas por día/ });
    const filas = within(tabla).getAllByRole("row");
    // Header + one row per point.
    expect(filas).toHaveLength(3);
    expect(within(filas[2]).getByText("24/10")).toBeInTheDocument();
    expect(within(filas[2]).getByText("40,0%")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ver como gráfico" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });

  it("los días sin valoraciones son huecos: sin barra y «sin valoraciones» en la tabla y el resumen", async () => {
    const user = userEvent.setup();
    espiaCalidad.mockResolvedValue(
      calidad({
        serie: [
          { dia: "2026-10-22", turnos: 0, votos: 0, negativos: 0, tasaNegativa: null },
          { dia: "2026-10-23", turnos: 10, votos: 4, negativos: 1, tasaNegativa: 0.25 },
          { dia: "2026-10-24", turnos: 0, votos: 0, negativos: 0, tasaNegativa: null },
        ],
      }),
    );
    const { container } = montar();

    const grafico = await screen.findByRole("img", {
      name: /del 22\/10 al 24\/10\..*1 de 3 días con valoraciones/,
    });
    expect(grafico).toBeInTheDocument();
    expect(container.querySelectorAll(".calidad-barra")).toHaveLength(1);
    expect(container.querySelectorAll(".calidad-barra-celda")).toHaveLength(3);

    await user.click(screen.getByRole("button", { name: "Ver como tabla" }));
    const tabla = screen.getByRole("table", { name: /Porcentaje de respuestas negativas por día/ });
    const filas = within(tabla).getAllByRole("row");
    expect(filas).toHaveLength(4);
    expect(within(filas[1]).getByText("sin valoraciones")).toBeInTheDocument();
    expect(within(filas[3]).getByText("sin valoraciones")).toBeInTheDocument();
    expect(within(filas[2]).getByText("25,0%")).toBeInTheDocument();
  });

  it("con 90 días la serie es semanal y la tabla titula «Semana del»", async () => {
    const user = userEvent.setup();
    espiaCalidad.mockResolvedValue(calidad({ periodoDias: 90, porSemana: true }));
    montar();

    expect(await screen.findByText("Respuestas negativas por semana")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Ver como tabla" }));
    expect(screen.getByRole("columnheader", { name: "Semana del" })).toBeInTheDocument();
  });
});

describe("PanelDeCalidad — motivos, causas y denegaciones", () => {
  it("«Por qué no sirvió» aclara que un voto puede tener varios motivos", async () => {
    montar();

    const tarjeta = (await screen.findByText("Por qué no sirvió")).closest("section")!;
    expect(within(tarjeta).getByText("Datos incorrectos")).toBeInTheDocument();
    expect(within(tarjeta).getByText("Sin motivo (omitió)")).toBeInTheDocument();
    expect(within(tarjeta).getByText(/Un voto puede tener varios motivos/)).toBeInTheDocument();
  });

  it("«Fallos por causa» usa etiquetas en español y marca resultado_vacio como señal", async () => {
    montar();

    const tarjeta = (await screen.findByText("Fallos por causa")).closest("section")!;
    expect(within(tarjeta).getByText("El modelo tardó demasiado")).toBeInTheDocument();
    const vacia = within(tarjeta)
      .getByText(/Consulta sin resultados/)
      .closest("li")!;
    expect(within(vacia).getByText("Señal")).toBeInTheDocument();
    const fallo = within(tarjeta).getByText("El modelo tardó demasiado").closest("li")!;
    expect(within(fallo).queryByText("Señal")).toBeNull();
  });

  it("sin votos negativos los motivos muestran «0 · —», nunca NaN", async () => {
    espiaCalidad.mockResolvedValue(
      calidad({
        negativosPorMotivo: [
          { clave: "datos_incorrectos", cantidad: 0 },
          { clave: "sin_motivo", cantidad: 0 },
        ],
      }),
    );
    montar();

    const tarjeta = (await screen.findByText("Por qué no sirvió")).closest("section")!;
    expect(within(tarjeta).getAllByText("0 · —")).toHaveLength(2);
    expect(tarjeta).not.toHaveTextContent("NaN");
  });

  it("las denegaciones de política van aparte, con su propia explicación", async () => {
    montar();

    const tarjeta = (await screen.findByText("Denegaciones de política")).closest("section")!;
    expect(within(tarjeta).getByText("Cupo diario agotado")).toBeInTheDocument();
    expect(within(tarjeta).getByText(/no entran en ninguna tasa/)).toBeInTheDocument();
  });

  it("todas las causas tienen etiqueta en español (22 códigos + sin clasificar)", () => {
    const codigos = [
      "proveedor_no_disponible",
      "proveedor_saturado",
      "timeout_proveedor",
      "credencial_rechazada",
      "modelo_no_encontrado",
      "error_del_proveedor",
      "techo_de_llamadas",
      "presupuesto_del_turno",
      "generacion_cortada",
      "respuesta_ininteligible",
      "validador_rechazo",
      "plan_invalido",
      "sin_privilegio_de_datos",
      "error_del_motor",
      "timeout_de_sentencia",
      "excepcion",
      "resultado_vacio",
      "cupo_agotado",
      "tope_organizacional",
      "mantenimiento",
      "acceso_revocado",
      "turno_concurrente",
      "sin_clasificar",
    ];
    expect(codigos).toHaveLength(23);
    for (const codigo of codigos) {
      expect(etiquetaDeCausa(codigo)).not.toBe(codigo);
    }
    // Saturation reads as busy, not as an outage.
    expect(etiquetaDeCausa("proveedor_saturado")).toMatch(/ocupado/);
  });

  it("«Por modelo» muestra los turnos de cada modelo", async () => {
    montar();

    const tarjeta = (await screen.findByText("Por modelo")).closest("section")!;
    const filaLocal = within(tarjeta).getByText("local/gpt-oss:20b").closest("tr")!;
    expect(within(filaLocal).getByText("200")).toBeInTheDocument();
    const filaNube = within(tarjeta).getByText("anthropic/claude-sonnet-5").closest("tr")!;
    expect(within(filaNube).getByText("40")).toBeInTheDocument();
    expect(within(filaNube).getByText("—")).toBeInTheDocument();
  });
});

describe("PanelDeCalidad — respuestas con problemas", () => {
  it("lista las filas con etiqueta de tipo, motivos en español y modelo", async () => {
    montar();

    const tarjeta = (await screen.findByText("Respuestas con problemas")).closest("section")!;
    expect(await within(tarjeta).findByText("¿Cuántas aulas hay?")).toBeInTheDocument();
    expect(within(tarjeta).getByText("Faltan datos, Otro")).toBeInTheDocument();
    expect(within(tarjeta).getByText("24/10")).toBeInTheDocument();
    expect(
      within(tarjeta).getByText(/Sin nombre de quien preguntó y sin el comentario libre/),
    ).toBeInTheDocument();
  });

  it("los filtros cambian el tipo pedido y las filas visibles, y vuelven a la página 1", async () => {
    const user = userEvent.setup();
    espiaProblemas.mockImplementation(async (_p, numero, _m, tipo) =>
      tipo === "fallo"
        ? pagina({
            filas: [
              fila({ tipo: "fallo", pregunta: "¿Qué aulas hay libres?", motivo: "excepcion" }),
            ],
          })
        : pagina({ total: 60, pagina: numero }),
    );
    montar();
    await screen.findByText("¿Cuántas aulas hay?");

    await user.click(screen.getByRole("button", { name: "Siguiente" }));
    await waitFor(() =>
      expect(espiaProblemas).toHaveBeenLastCalledWith(30, 2, undefined, undefined),
    );

    await user.click(screen.getByRole("button", { name: "Fallos" }));

    await waitFor(() => expect(espiaProblemas).toHaveBeenLastCalledWith(30, 1, undefined, "fallo"));
    expect(await screen.findByText("¿Qué aulas hay libres?")).toBeInTheDocument();
    expect(screen.queryByText("¿Cuántas aulas hay?")).not.toBeInTheDocument();
    expect(screen.getByText("Error inesperado")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Fallos" })).toHaveAttribute("aria-pressed", "true");
  });

  it("la paginación pide la página siguiente y deshabilita los extremos", async () => {
    const user = userEvent.setup();
    espiaProblemas.mockImplementation(async (_p, numero) => pagina({ total: 60, pagina: numero }));
    montar();
    await screen.findByText("Página 1 de 3");

    expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();
    expect(screen.getByText("1–25 de 60")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Siguiente" }));
    await screen.findByText("Página 2 de 3");
    expect(espiaProblemas).toHaveBeenLastCalledWith(30, 2, undefined, undefined);

    await user.click(screen.getByRole("button", { name: "Siguiente" }));
    await screen.findByText("Página 3 de 3");
    expect(screen.getByRole("button", { name: "Siguiente" })).toBeDisabled();
    expect(screen.getByText("51–60 de 60")).toBeInTheDocument();
  });

  it("sin filas muestra el vacío", async () => {
    espiaProblemas.mockResolvedValue(pagina({ total: 0, filas: [] }));
    montar();

    expect(
      await screen.findByText("No hay respuestas con este problema en el período."),
    ).toBeInTheDocument();
  });

  it("sin asistente.revisar_calidad muestra el aviso y nunca pide la lista", async () => {
    conPermisos(["asistente.administrar"]);
    montar();

    expect(
      await screen.findByText("Necesitás el permiso «Revisar calidad del asistente»."),
    ).toBeInTheDocument();
    // The aggregates still render.
    expect(
      await screen.findByRole("region", { name: "Indicadores de calidad" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Tipo de problema" })).not.toBeInTheDocument();
    expect(screen.queryByRole("table", { name: /problemas/i })).not.toBeInTheDocument();
    expect(espiaProblemas).not.toHaveBeenCalled();
  });

  it("si los agregados fallan avisa en vez de quedarse cargando", async () => {
    espiaCalidad.mockRejectedValue(new Error("boom"));
    montar();

    expect(
      await screen.findByText("No se pudieron cargar las métricas de calidad."),
    ).toBeInTheDocument();
  });
});
