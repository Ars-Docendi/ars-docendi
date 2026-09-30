import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { PanelDeUso } from "./PanelDeUso";
import type { CupoDeRolPersistido, UsoAgregado, UsoDelAsistente } from "../types";

// ============================================================
// El detalle del panel de uso (asistente-panel-de-uso, sistema-seccion-
// unificada), en su pasada de fidelidad con el canvas de Claude Design: una
// tarjeta con pestañas Por usuario/Por rol + selector de métrica, buscador,
// y la métrica elegida con una barra por fila (ordenada sola, descendente).
// `PanelAdministracionAsistente.test.tsx` cubre la integración (región viva
// compartida, KPIs, mantenimiento, CSV); acá va el comportamiento propio de
// esta pieza.
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
    codigosDeRol: [],
    cupoEfectivo: null,
    origenDeCupo: null,
    accesoEfectivo: true,
    origenDeAcceso: "rol",
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
) {
  const onGuardarCupoDeRol = vi.fn().mockResolvedValue(undefined);
  const onGuardarCupoDeUsuario = vi.fn().mockResolvedValue(undefined);
  const onRestablecerCupoDeUsuario = vi.fn().mockResolvedValue(undefined);
  const onCambiarAccesoDeRol = vi.fn().mockResolvedValue(undefined);
  const onCambiarAccesoDeUsuario = vi.fn().mockResolvedValue(undefined);
  const onGuardado = vi.fn();
  const resultado = render(
    <PanelDeUso
      uso={usoAMontar}
      cargando={false}
      cuposPorRol={cuposPorRol}
      onGuardarCupoDeRol={onGuardarCupoDeRol}
      onGuardarCupoDeUsuario={onGuardarCupoDeUsuario}
      onRestablecerCupoDeUsuario={onRestablecerCupoDeUsuario}
      onCambiarAccesoDeRol={onCambiarAccesoDeRol}
      onCambiarAccesoDeUsuario={onCambiarAccesoDeUsuario}
      onGuardado={onGuardado}
    />,
  );
  return {
    ...resultado,
    onGuardarCupoDeRol,
    onGuardarCupoDeUsuario,
    onRestablecerCupoDeUsuario,
    onCambiarAccesoDeRol,
    onCambiarAccesoDeUsuario,
    onGuardado,
  };
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

describe("Selector de métrica (Sesiones/Costo/Tokens/Latencia)", () => {
  it("arranca en «Sesiones» y las filas se ordenan solas, descendente por esa métrica", () => {
    montar();

    const switcher = within(screen.getByRole("radiogroup", { name: "Métrica" }));
    expect(switcher.getByRole("radio", { name: "Sesiones" })).toHaveAttribute(
      "aria-checked",
      "true",
    );
    const filas = screen.getAllByRole("row").slice(1); // sin el encabezado
    expect(within(filas[0]).getByText("Bruno Paz")).toBeInTheDocument(); // 20 turnos primero
  });

  it("cambiar a «Costo» reordena por costo y muestra el valor de esa métrica", async () => {
    const user = userEvent.setup();
    montar();

    const switcher = within(screen.getByRole("radiogroup", { name: "Métrica" }));
    await user.click(switcher.getByRole("radio", { name: "Costo" }));

    expect(switcher.getByRole("radio", { name: "Costo" })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByRole("columnheader", { name: "Costo estimado" })).toBeInTheDocument();
    const filas = screen.getAllByRole("row").slice(1);
    expect(within(filas[0]).getByText("Bruno Paz")).toBeInTheDocument(); // US$ 2 primero
  });

  it("en «Latencia» cada fila muestra el promedio como nota", async () => {
    const user = userEvent.setup();
    montar({
      porUsuario: [
        fila({
          clave: "u1",
          nombreParaMostrar: "Marina Díaz",
          turnos: 5,
          latenciaP95Ms: 1500,
          latenciaPromedioMs: 900,
        }),
      ],
      porRol: [],
      organizacion: fila({ clave: "organizacion" }),
    });

    await user.click(
      within(screen.getByRole("radiogroup", { name: "Métrica" })).getByRole("radio", {
        name: "Latencia",
      }),
    );

    const filaDeMarina = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(filaDeMarina).getByText(/promedio/)).toBeInTheDocument();
  });

  it("una fila sin uso muestra «Sin uso» en vez del valor de la métrica", () => {
    montar({
      porUsuario: [fila({ clave: "u1", nombreParaMostrar: "Sin Uso", turnos: 0 })],
      porRol: [],
      organizacion: fila({ clave: "organizacion" }),
    });

    const filaSinUso = screen.getByText("Sin Uso").closest("tr")!;
    expect(within(filaSinUso).getByText("Sin uso")).toBeInTheDocument();
  });
});

describe("El costo estimado nunca se ve como cero silencioso", () => {
  it("con la métrica en Costo, lleva «(estimado)» y muestra aparte los turnos sin precio", async () => {
    const user = userEvent.setup();
    montar();

    await user.click(screen.getByRole("tab", { name: /Por rol/ }));
    await user.click(
      within(screen.getByRole("radiogroup", { name: "Métrica" })).getByRole("radio", {
        name: "Costo",
      }),
    );

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
  it("una fila de rol muestra su default persistido, «N por día · del rol» (copia del canvas)", async () => {
    const user = userEvent.setup();
    montar(USO, [{ rol: "docente", cupoDiarioTurnos: 15, accesoHabilitado: true }]);

    // "Por rol" no es la pestaña inicial.
    await user.click(screen.getByRole("tab", { name: /Por rol/ }));

    expect(screen.getByText("15 por día · del rol")).toBeInTheDocument();
  });

  it("una fila de usuario con override la muestra «N por día · propio»", () => {
    montar({
      ...USO,
      porUsuario: [
        fila({
          clave: "u1",
          nombreParaMostrar: "Marina Díaz",
          turnos: 5,
          cupoEfectivo: 7,
          origenDeCupo: "override",
        }),
      ],
    });

    expect(screen.getByText("7 por día · propio")).toBeInTheDocument();
  });

  it("una fila de usuario sin override muestra el cupo efectivo del rol que ya resolvió el backend", () => {
    // Ya NO hace falta un mapa de roles aparte: `GET …/uso` trae, por fila de
    // usuario, el cupo efectivo y su origen — la misma regla que aplicaría
    // la ejecución real del turno (tarea «rol y cupo efectivo por usuario»).
    montar({
      ...USO,
      porUsuario: [
        fila({
          clave: "u1",
          nombreParaMostrar: "Marina Díaz",
          turnos: 5,
          codigosDeRol: ["docente"],
          cupoEfectivo: 15,
          origenDeCupo: "rol",
        }),
      ],
    });

    const filaDeMarina = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(filaDeMarina).getByText("15 por día · del rol")).toBeInTheDocument();
  });

  it("una fila de usuario sin override y sin ningún rol de sistema dice «sin dato»", () => {
    // El único caso legítimo de «sin dato»: ni override ni rol del que
    // heredar un default (`ReglaDeCupoEfectivo` en el backend).
    montar({
      ...USO,
      porUsuario: [fila({ clave: "u1", nombreParaMostrar: "Marina Díaz", turnos: 5 })],
    });

    const filaDeMarina = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(filaDeMarina).getByText("sin dato")).toBeInTheDocument();
  });
});

describe("Subtítulo de rol bajo el nombre (tarea «rol y cupo efectivo por usuario»)", () => {
  it("una fila de usuario con roles vigentes muestra sus nombres legibles como subtítulo", () => {
    montar({
      ...USO,
      porUsuario: [
        fila({
          clave: "u1",
          nombreParaMostrar: "Marina Díaz",
          turnos: 5,
          codigosDeRol: ["decanato", "secretaria"],
        }),
      ],
    });

    const filaDeMarina = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(filaDeMarina).getByText("Decanato · Secretaría Académica")).toBeInTheDocument();
  });

  it("una fila de rol nunca muestra un subtítulo (ella misma ya es el rol)", async () => {
    const user = userEvent.setup();
    montar();

    await user.click(screen.getByRole("tab", { name: /Por rol/ }));

    const filaDelRol = screen.getByText("docente").closest("tr")!;
    expect(within(filaDelRol).queryByText(/·/)).not.toBeInTheDocument();
  });
});

describe("El botón de editar cupo está presente en cada fila (fidelidad con el canvas)", () => {
  it("cada fila de usuario expone su botón de editar con el aria-label correcto", () => {
    montar();

    expect(
      screen.getByRole("button", { name: "Editar cupo diario de Marina Díaz" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Editar cupo diario de Bruno Paz" }),
    ).toBeInTheDocument();
  });

  it("cada fila de rol también expone su botón de editar", async () => {
    const user = userEvent.setup();
    montar();

    await user.click(screen.getByRole("tab", { name: /Por rol/ }));

    expect(
      screen.getByRole("button", { name: "Editar cupo diario de docente" }),
    ).toBeInTheDocument();
  });
});

describe("Columna «Acceso» (asistente-acceso-granular)", () => {
  function usuario(parcial: Partial<UsoAgregado>) {
    return {
      ...USO,
      porUsuario: [fila({ clave: "u1", nombreParaMostrar: "Marina Díaz", turnos: 5, ...parcial })],
    };
  }

  it("un usuario con acceso heredado lo muestra «Con acceso · del rol» y se le puede quitar", async () => {
    const user = userEvent.setup();
    const { onCambiarAccesoDeUsuario, onGuardado } = montar(usuario({}));

    const fila = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(fila).getByText("Con acceso")).toBeInTheDocument();
    expect(within(fila).getByText("del rol")).toBeInTheDocument();

    await user.click(
      within(fila).getByRole("switch", { name: "Acceso de Marina Díaz al asistente" }),
    );

    await waitFor(() => expect(onCambiarAccesoDeUsuario).toHaveBeenCalledWith("u1", false));
    expect(onGuardado).toHaveBeenCalledWith("Acceso de Marina Díaz: quitado.");
  });

  it("un usuario revocado se ve «Sin acceso · propio», con restablecer, y su cupo no se edita", async () => {
    const user = userEvent.setup();
    const { onCambiarAccesoDeUsuario } = montar(
      usuario({
        accesoEfectivo: false,
        origenDeAcceso: "propio",
        cupoEfectivo: 10,
        origenDeCupo: "rol",
      }),
    );

    const fila = screen.getByText("Marina Díaz").closest("tr")!;
    expect(within(fila).getByText("Sin acceso")).toBeInTheDocument();
    expect(within(fila).getByText("propio")).toBeInTheDocument();
    expect(
      within(fila).getByRole("button", { name: "Editar cupo diario de Marina Díaz" }),
    ).toBeDisabled();

    await user.click(
      within(fila).getByRole("button", { name: "Restablecer el acceso de Marina Díaz al del rol" }),
    );

    await waitFor(() => expect(onCambiarAccesoDeUsuario).toHaveBeenCalledWith("u1", true));
  });

  it("a un usuario sin acceso por su rol no se le puede dar: el interruptor está deshabilitado", () => {
    montar(usuario({ accesoEfectivo: false, origenDeAcceso: "rol" }));

    const fila = screen.getByText("Marina Díaz").closest("tr")!;
    expect(
      within(fila).getByRole("switch", { name: "Acceso de Marina Díaz al asistente" }),
    ).toBeDisabled();
    expect(
      within(fila).queryByRole("button", { name: /Restablecer el acceso/ }),
    ).not.toBeInTheDocument();
  });

  it("un cupo propio se restablece al del rol desde la fila", async () => {
    const user = userEvent.setup();
    const { onRestablecerCupoDeUsuario, onGuardado } = montar(
      usuario({ cupoEfectivo: 60, origenDeCupo: "override" }),
    );

    await user.click(
      screen.getByRole("button", { name: "Restablecer el cupo de Marina Díaz al del rol" }),
    );

    await waitFor(() => expect(onRestablecerCupoDeUsuario).toHaveBeenCalledWith("u1"));
    expect(onGuardado).toHaveBeenCalledWith("Cupo diario de Marina Díaz: restablecido al del rol.");
  });

  it("un cupo heredado del rol no ofrece restablecer", () => {
    montar(usuario({ cupoEfectivo: 15, origenDeCupo: "rol" }));

    expect(screen.queryByRole("button", { name: /Restablecer el cupo/ })).not.toBeInTheDocument();
  });

  it("en «Por rol» el acceso del rol se prende y apaga libremente", async () => {
    const user = userEvent.setup();
    const { onCambiarAccesoDeRol } = montar(USO, [
      { rol: "docente", cupoDiarioTurnos: 15, accesoHabilitado: false },
    ]);

    await user.click(screen.getByRole("tab", { name: /Por rol/ }));
    const interruptor = screen.getByRole("switch", { name: /al asistente/ });
    expect(interruptor).not.toBeChecked();
    expect(interruptor).toBeEnabled();

    await user.click(interruptor);

    await waitFor(() => expect(onCambiarAccesoDeRol).toHaveBeenCalledWith("docente", true));
  });

  it("el pie explica la herencia: el acceso se puede quitar, no dar", () => {
    montar();

    expect(
      screen.getByText(/a un usuario se le puede quitar el acceso, no darlo/),
    ).toBeInTheDocument();
  });
});
