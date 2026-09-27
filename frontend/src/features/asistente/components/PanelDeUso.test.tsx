import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDeUso } from "./PanelDeUso";
import type {
  CupoDeRolPersistido,
  OverrideDeUsuarioPersistido,
  UsoAgregado,
  UsoDelAsistente,
} from "../types";

// ============================================================
// El detalle del panel de uso (asistente-panel-de-uso, sistema-seccion-
// unificada): pestañas Por usuario/Por rol, buscador, columnas ordenables y
// el cupo diario editado directo en la fila. `PanelAdministracionAsistente.
// test.tsx` cubre la integración (región viva compartida, KPIs, mantenimiento);
// acá va el comportamiento propio de esta pieza.
// ============================================================

function fila(parcial: Partial<UsoAgregado>): UsoAgregado {
  return {
    clave: "clave",
    turnos: 0,
    porEstado: {},
    llamadasAlModelo: 0,
    tokensDeEntrada: 0,
    tokensDeSalida: 0,
    tokensDeCache: 0,
    latenciaPromedioMs: 0,
    latenciaP95Ms: 0,
    proveedores: [],
    costoEstimado: 0,
    esEstimado: true,
    turnosSinPrecio: 0,
    ...parcial,
  };
}

const USO: UsoDelAsistente = {
  porUsuario: [
    fila({ clave: "u1", nombreParaMostrar: "Marina Díaz", turnos: 5, costoEstimado: 0.5 }),
    fila({ clave: "u2", nombreParaMostrar: "Bruno Paz", turnos: 20, costoEstimado: 2 }),
  ],
  porRol: [fila({ clave: "docente", turnos: 12, costoEstimado: 1, turnosSinPrecio: 3 })],
  organizacion: fila({ clave: "organizacion", turnos: 25 }),
};

function montar(
  usoAMontar: UsoDelAsistente | undefined = USO,
  cuposPorRol: CupoDeRolPersistido[] = [],
  overridesPorUsuario: OverrideDeUsuarioPersistido[] = [],
) {
  const onGuardarCupoDeRol = vi.fn().mockResolvedValue(undefined);
  const onGuardarCupoDeUsuario = vi.fn().mockResolvedValue(undefined);
  const onGuardado = vi.fn();
  const resultado = render(
    <PanelDeUso
      uso={usoAMontar}
      cargando={false}
      cuposPorRol={cuposPorRol}
      overridesPorUsuario={overridesPorUsuario}
      onGuardarCupoDeRol={onGuardarCupoDeRol}
      onGuardarCupoDeUsuario={onGuardarCupoDeUsuario}
      onGuardado={onGuardado}
    />,
  );
  return { ...resultado, onGuardarCupoDeRol, onGuardarCupoDeUsuario, onGuardado };
}

describe("Pestañas Por usuario / Por rol", () => {
  it("arranca en «Por usuario» y cada pestaña muestra el conteo real", () => {
    montar();

    expect(screen.getByRole("tab", { name: /Por usuario/ })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByText("Marina Díaz")).toBeInTheDocument();
    expect(
      within(screen.getByRole("tab", { name: /Por usuario/ })).getByText("2"),
    ).toBeInTheDocument();
    expect(within(screen.getByRole("tab", { name: /Por rol/ })).getByText("1")).toBeInTheDocument();
  });

  it("cambiar de pestaña muestra la otra tabla y limpia la búsqueda", async () => {
    const user = userEvent.setup();
    montar();

    await user.type(screen.getByLabelText("Buscar usuario o rol"), "marina");
    await user.click(screen.getByRole("tab", { name: /Por rol/ }));

    expect(screen.getByText("docente")).toBeInTheDocument();
    expect(screen.queryByText("Marina Díaz")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Buscar usuario o rol")).toHaveValue("");
  });

  it("un período sin datos muestra el aviso de vacío, por pestaña", async () => {
    const user = userEvent.setup();
    montar({ porUsuario: [], porRol: [], organizacion: fila({ clave: "organizacion" }) });

    expect(screen.getByText("No hay uso registrado en este período.")).toBeInTheDocument();
    await user.click(screen.getByRole("tab", { name: /Por rol/ }));
    expect(screen.getByText("No hay uso registrado en este período.")).toBeInTheDocument();
  });
});

describe("Buscador", () => {
  it("filtra las filas por nombre o clave", async () => {
    const user = userEvent.setup();
    montar();

    await user.type(screen.getByLabelText("Buscar usuario o rol"), "bruno");

    expect(screen.getByText("Bruno Paz")).toBeInTheDocument();
    expect(screen.queryByText("Marina Díaz")).not.toBeInTheDocument();
  });
});

describe("Columnas ordenables", () => {
  it("ordenar por Turnos alterna descendente/ascendente", async () => {
    const user = userEvent.setup();
    montar();

    const botonTurnos = within(screen.getByRole("columnheader", { name: /Turnos/ })).getByRole(
      "button",
    );

    await user.click(botonTurnos);
    let filas = screen.getAllByRole("row").slice(1); // sin el encabezado
    expect(within(filas[0]).getByText("Bruno Paz")).toBeInTheDocument(); // 20 turnos primero

    await user.click(botonTurnos);
    filas = screen.getAllByRole("row").slice(1);
    expect(within(filas[0]).getByText("Marina Díaz")).toBeInTheDocument(); // 5 turnos primero
  });
});

describe("El costo estimado nunca se ve como cero silencioso", () => {
  it("lleva «(estimado)» y muestra aparte los turnos sin precio", async () => {
    const user = userEvent.setup();
    montar();

    await user.click(screen.getByRole("tab", { name: /Por rol/ }));

    const filaDelRol = screen.getByText("docente").closest("tr")!;
    expect(within(filaDelRol).getByText(/\(estimado\)/)).toBeInTheDocument();
    expect(within(filaDelRol).getByText("3 turnos sin precio")).toBeInTheDocument();
  });
});

describe("Cupo diario editado en la fila", () => {
  it("guardar el cupo de un usuario llama a editarCupoDeUsuario con su clave", async () => {
    const user = userEvent.setup();
    const { onGuardarCupoDeUsuario, onGuardado } = montar();

    await user.click(screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }));
    await user.type(screen.getByLabelText("Cupo diario de Marina Díaz (turnos)"), "30");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(onGuardarCupoDeUsuario).toHaveBeenCalledWith("u1", 30));
    expect(onGuardado).toHaveBeenCalledWith("Cupo diario de Marina Díaz: 30 turnos.");
  });

  it("guardar el cupo de un rol llama a editarCupoDeRol con su código", async () => {
    const user = userEvent.setup();
    const { onGuardarCupoDeRol } = montar();

    await user.click(screen.getByRole("tab", { name: /Por rol/ }));
    await user.click(screen.getByRole("button", { name: "Editar cupo diario de docente" }));
    await user.type(screen.getByLabelText("Cupo diario de docente (turnos)"), "20");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(onGuardarCupoDeRol).toHaveBeenCalledWith("docente", 20));
  });
});

describe("El cupo mostrado en la fila viene de lo persistido (tarea 12.8)", () => {
  it("una fila de rol muestra su default persistido, etiquetado «(del rol)»", async () => {
    const user = userEvent.setup();
    montar(USO, [{ rol: "docente", cupoDiarioTurnos: 15 }]);

    // "Por rol" no es la pestaña inicial.
    await user.click(screen.getByRole("tab", { name: /Por rol/ }));

    expect(screen.getByText("15")).toBeInTheDocument();
    expect(screen.getByText("(del rol)")).toBeInTheDocument();
  });

  it("una fila de usuario con override la muestra etiquetada «(override)»", () => {
    montar(USO, [], [{ actorId: "u1", nombreParaMostrar: "Marina Díaz", cupoDiarioTurnos: 7 }]);

    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByText("(override)")).toBeInTheDocument();
  });

  it("una fila de usuario sin override no inventa el default de ningún rol", () => {
    montar(USO, [{ rol: "docente", cupoDiarioTurnos: 15 }], []);

    const filaDeMarina = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(filaDeMarina).getByText("sin override propio")).toBeInTheDocument();
  });
});
