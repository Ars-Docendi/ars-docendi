import { describe, it, expect } from "vitest";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { DetallePedidoPage } from "./DetallePedidoPage";
import { MisPedidosPage } from "./MisPedidosPage";
import { PedidoFormPage } from "./PedidoFormPage";
import { listarMisPedidos, listarPedidosPorAmbito } from "../api/pedidosApi";
import { setMockUser, setRolActivo } from "../../../shared/auth/dev/mockSession";
import type { ActorContexto } from "../types";

// Integración del circuito completo: usa el stack real (hooks → api mock →
// store → maquinaEstados) y cambia el ROL ACTIVO del usuario "Demo (todos los
// roles)" para que el mismo pedido viaje entre vistas, tal como en la defensa.
// Las acciones se disparan desde el panel del detalle y se CONFIRMAN en el
// modal de confirmación (donde también se valida el justificativo [BR-005]).
const DEMO_ID = "a0000000-0000-4000-8000-000000000007";
const COORD: ActorContexto = {
  rol: "Coordinador",
  nombre: "Demo",
  carrera: "Ingeniería en Informática",
};
const JC: ActorContexto = {
  rol: "Jefe de Cátedra",
  nombre: "Demo",
  carrera: "Ingeniería en Informática",
  catedra: "Ingeniería de Software",
};

function nuevoClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

// El botón Volver usa `navigate(-1)` (vuelve a la pantalla anterior real, no a
// una ruta fija) — sembramos un origen en el historial para poder probarlo.
function renderDetalle(id: string, origen = "/designaciones/revision") {
  return render(
    <QueryClientProvider client={nuevoClient()}>
      <MemoryRouter initialEntries={[origen, `/designaciones/pedidos/${id}`]} initialIndex={1}>
        <Routes>
          <Route path="/designaciones/pedidos/:id" element={<DetallePedidoPage />} />
          <Route path="/designaciones/pedidos/:id/editar" element={<p>Editar pedido</p>} />
          <Route path="/designaciones/revision" element={<p>Superficie de revisión</p>} />
          <Route path="/designaciones/mis-pedidos" element={<p>Mis pedidos</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** App mínima con las rutas de Mis pedidos + edición, para el flujo de reenvío vía form. */
function renderMisPedidos() {
  return render(
    <QueryClientProvider client={nuevoClient()}>
      <MemoryRouter initialEntries={["/designaciones/mis-pedidos"]}>
        <Routes>
          <Route path="/designaciones/mis-pedidos" element={<MisPedidosPage />} />
          <Route path="/designaciones/pedidos/:id/editar" element={<PedidoFormPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Id del pedido sembrado en `en_revision_coordinador` dentro del ámbito del Coordinador. */
async function idEnCoordinador(): Promise<string> {
  const lista = await listarPedidosPorAmbito(COORD);
  const objetivo = lista.find((p) => p.estado === "en_revision_coordinador");
  if (!objetivo)
    throw new Error("El seed no tiene un pedido en_revision_coordinador en Informática.");
  return objetivo.id;
}

/** Id de un pedido sembrado en `borrador` para el Jefe de Cátedra (Mis pedidos). */
async function idBorradorDelJC(): Promise<string> {
  const lista = await listarMisPedidos(JC);
  const objetivo = lista.find((p) => p.estado === "borrador");
  if (!objetivo) throw new Error("El seed no tiene un pedido en borrador para el JC.");
  return objetivo.id;
}

/** Id de un pedido sembrado en `devuelto` cuyo propietario actual es el Jefe de Cátedra. */
async function idDevueltoDelJC(): Promise<string> {
  const lista = await listarMisPedidos(JC);
  const objetivo = lista.find(
    (p) => p.estado === "devuelto" && p.propietarioActual === "Jefe de Cátedra",
  );
  if (!objetivo) throw new Error("El seed no tiene un pedido devuelto al JC.");
  return objetivo.id;
}

/** Abre el modal de aprobación desde el panel y confirma con "Aprobar y enviar". */
async function aprobarViaModal(user: ReturnType<typeof userEvent.setup>) {
  // Con el modal cerrado hay un único botón "Aprobar …" (el del panel).
  await user.click(await screen.findByRole("button", { name: /^Aprobar/ }));
  const dialog = await screen.findByRole("dialog");
  await user.click(within(dialog).getByRole("button", { name: "Aprobar y enviar" }));
}

describe("Flujo de aprobación (integración)", () => {
  it("happy-path: Coordinador → Secretaría → Decanato → en_lote", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id);

    // Coordinador aprueba (el comentario es opcional) → confirma → avanza a Secretaría.
    await aprobarViaModal(user);
    expect(await screen.findByText("En revisión · Secretaría")).toBeInTheDocument();

    // Cambio de rol: Secretaría aprueba → avanza a Decanato.
    act(() => setRolActivo("Secretaría"));
    await aprobarViaModal(user);
    expect(await screen.findByText("En revisión · Decanato")).toBeInTheDocument();

    // Cambio de rol: Decanato aprueba → en_lote (terminal-prototipo).
    act(() => setRolActivo("Decanato"));
    await aprobarViaModal(user);
    expect((await screen.findAllByText("En lote")).length).toBeGreaterThan(0);
  });

  it("devolución: Coordinador devuelve → JC reenvía → vuelve a en_revision_coordinador", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    const { unmount } = renderDetalle(id);

    // Devolver abre el modal; sin justificativo, el confirmar está bloqueado [BR-005].
    await user.click(await screen.findByRole("button", { name: "Devolver" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Devolver a Borrador" })).toBeDisabled();

    // Con justificativo en el modal sí: pasa a "devuelto" y el panel desaparece.
    await user.type(within(dialog).getByRole("textbox"), "Revisar la justificación adjunta.");
    await user.click(within(dialog).getByRole("button", { name: "Devolver a Borrador" }));
    await waitFor(() => {
      expect(screen.queryByRole("button", { name: "Devolver" })).not.toBeInTheDocument();
    });
    expect((await screen.findAllByText("Devuelto")).length).toBeGreaterThan(0);
    unmount();

    // El Jefe de Cátedra reenvía editando el pedido devuelto: "Editar" en la
    // fila de Mis Pedidos abre el form, que ofrece "Guardar y reenviar".
    act(() => setRolActivo("Jefe de Cátedra"));
    renderMisPedidos();
    const filaValeria = await screen.findByRole("row", {
      name: "Ver el pedido de Valeria Suárez",
    });
    await user.click(within(filaValeria).getByRole("button", { name: "Editar" }));

    await screen.findByRole("button", { name: "Guardar y reenviar" });
    await user.click(screen.getByRole("button", { name: "Guardar y reenviar" }));

    // Tras reenviar (editar → reenviar, dos mutaciones encadenadas con demora
    // simulada cada una) vuelve a Mis pedidos; el default de `waitFor` (1000ms)
    // puede no alcanzar para las dos mutations en cadena.
    await waitFor(
      () => {
        expect(
          screen.queryByRole("button", { name: "Guardar y reenviar" }),
        ).not.toBeInTheDocument();
      },
      { timeout: 3000 },
    );
    const filaTrasReenviar = await screen.findByRole("row", {
      name: "Ver el pedido de Valeria Suárez",
    });
    expect(within(filaTrasReenviar).queryByRole("button", { name: "Editar" })).toBeNull();
  });

  it("prioridad: marcar y quitar prioritario en el mismo flujo (tema E)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id);

    // Marcar exige justificativo [BR-017].
    await user.click(await screen.findByRole("button", { name: "Marcar prioritario" }));
    let dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Guardar prioridad" })).toBeDisabled();
    await user.type(
      within(dialog).getByRole("textbox"),
      "Caso urgente por inicio de cuatrimestre.",
    );
    await user.click(within(dialog).getByRole("button", { name: "Guardar prioridad" }));

    // Una vez marcado, el botón cambia a "Quitar prioritario" — nunca conviven ambos.
    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Quitar prioritario" })).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: "Marcar prioritario" })).not.toBeInTheDocument();

    // Quitar prioritario no exige comentario.
    await user.click(screen.getByRole("button", { name: "Quitar prioritario" }));
    dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Quitar prioridad" })).toBeEnabled();
    await user.click(within(dialog).getByRole("button", { name: "Quitar prioridad" }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Marcar prioritario" })).toBeInTheDocument();
    });
  });

  it("rechazo: el motivo queda destacado en el detalle (tema E)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id);

    await user.click(await screen.findByRole("button", { name: "Rechazar" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByRole("textbox"), "No cumple los requisitos de antigüedad.");
    await user.click(within(dialog).getByRole("button", { name: "Rechazar novedad" }));

    expect(
      await screen.findByText("“No cumple los requisitos de antigüedad.”"),
    ).toBeInTheDocument();
  });

  it("el botón Volver navega a la pantalla anterior (Revisión, tema E)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id, "/designaciones/revision");

    await user.click(await screen.findByRole("button", { name: "Volver" }));
    expect(await screen.findByText("Superficie de revisión")).toBeInTheDocument();
  });

  it("el botón Volver navega a la pantalla anterior (Mis pedidos, mis-pedidos-simplificado)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id, "/designaciones/mis-pedidos");

    await user.click(await screen.findByRole("button", { name: "Volver" }));
    expect(await screen.findByText("Mis pedidos")).toBeInTheDocument();
  });

  it("un borrador se puede eliminar desde su propio detalle (mis-pedidos-simplificado)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Jefe de Cátedra"));
    const id = await idBorradorDelJC();
    renderDetalle(id, "/designaciones/mis-pedidos");

    await user.click(await screen.findByRole("button", { name: "Eliminar" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Eliminar" }));

    expect(await screen.findByText("Mis pedidos")).toBeInTheDocument();
  });

  it("el botón Editar aparece en un borrador propio y navega a la edición (mis-pedidos-simplificado)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Jefe de Cátedra"));
    const id = await idBorradorDelJC();
    renderDetalle(id, "/designaciones/mis-pedidos");

    await user.click(await screen.findByRole("button", { name: "Editar" }));

    expect(await screen.findByText("Editar pedido")).toBeInTheDocument();
  });

  it("el botón Editar aparece en un devuelto propio del JC (mis-pedidos-simplificado)", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Jefe de Cátedra"));
    const id = await idDevueltoDelJC();
    renderDetalle(id, "/designaciones/mis-pedidos");

    await user.click(await screen.findByRole("button", { name: "Editar" }));

    expect(await screen.findByText("Editar pedido")).toBeInTheDocument();
  });

  it("el botón Editar no aparece en un pedido en revisión (mis-pedidos-simplificado)", async () => {
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id);

    await screen.findByRole("button", { name: "Volver" });
    expect(screen.queryByRole("button", { name: "Editar" })).not.toBeInTheDocument();
  });
});
