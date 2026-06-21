import { describe, it, expect } from "vitest";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { DetallePedidoPage } from "./DetallePedidoPage";
import { MisPedidosPage } from "./MisPedidosPage";
import { listarPedidosPorAmbito } from "../api/pedidosApi";
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

function nuevoClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderDetalle(id: string) {
  return render(
    <QueryClientProvider client={nuevoClient()}>
      <MemoryRouter initialEntries={[`/designaciones/pedidos/${id}`]}>
        <Routes>
          <Route path="/designaciones/pedidos/:id" element={<DetallePedidoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function renderMisPedidos() {
  return render(
    <QueryClientProvider client={nuevoClient()}>
      <MemoryRouter>
        <MisPedidosPage />
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

    // El Jefe de Cátedra reenvía desde "Mis pedidos" (acción del menú kebab).
    act(() => setRolActivo("Jefe de Cátedra"));
    renderMisPedidos();
    await user.click(
      await screen.findByRole("button", { name: "Acciones del pedido de Valeria Suárez" }),
    );
    await user.click(await screen.findByRole("menuitem", { name: /Reenviar a revisión/ }));

    // Tras reenviar vuelve a revisión: su kebab ya no ofrece la acción de reenvío.
    await user.click(
      await screen.findByRole("button", { name: "Acciones del pedido de Valeria Suárez" }),
    );
    await waitFor(() => {
      expect(
        screen.queryByRole("menuitem", { name: /Reenviar a revisión/ }),
      ).not.toBeInTheDocument();
    });
  });
});
