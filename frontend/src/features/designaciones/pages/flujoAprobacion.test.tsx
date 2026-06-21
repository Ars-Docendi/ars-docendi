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

async function confirmar(user: ReturnType<typeof userEvent.setup>, etiqueta: string) {
  const dialog = await screen.findByRole("dialog");
  await user.click(within(dialog).getByRole("button", { name: etiqueta }));
}

describe("Flujo de aprobación (integración)", () => {
  it("happy-path: Coordinador → Secretaría → Decanato → en_lote", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    renderDetalle(id);

    // Coordinador acepta → avanza a Secretaría.
    await user.click(await screen.findByRole("button", { name: "Aceptar" }));
    await confirmar(user, "Aceptar");
    expect(await screen.findByText("En revisión · Secretaría")).toBeInTheDocument();

    // Cambio de rol: Secretaría acepta → avanza a Decanato.
    act(() => setRolActivo("Secretaría"));
    await user.click(await screen.findByRole("button", { name: "Aceptar" }));
    await confirmar(user, "Aceptar");
    expect(await screen.findByText("En revisión · Decanato")).toBeInTheDocument();

    // Cambio de rol: Decanato acepta → en_lote (terminal-prototipo).
    act(() => setRolActivo("Decanato"));
    await user.click(await screen.findByRole("button", { name: "Aceptar" }));
    await confirmar(user, "Aceptar");
    expect(await screen.findByText("En lote")).toBeInTheDocument();
  });

  it("devolución: Coordinador devuelve → JC reenvía → vuelve a en_revision_coordinador", async () => {
    const user = userEvent.setup();
    setMockUser(DEMO_ID);
    act(() => setRolActivo("Coordinador"));
    const id = await idEnCoordinador();
    const { unmount } = renderDetalle(id);

    // Devolver sin comentario: la UI lo bloquea [BR-005] (no muta el store).
    await user.click(await screen.findByRole("button", { name: "Devolver" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Devolver" }));
    expect(within(dialog).getByRole("alert")).toBeInTheDocument();

    // Con comentario sí: pasa a "devuelto".
    await user.type(within(dialog).getByRole("textbox"), "Revisar la justificación adjunta.");
    await user.click(within(dialog).getByRole("button", { name: "Devolver" }));
    expect(await screen.findByText("Devuelto")).toBeInTheDocument();
    unmount();

    // El Jefe de Cátedra reenvía desde "Mis pedidos".
    act(() => setRolActivo("Jefe de Cátedra"));
    renderMisPedidos();
    const reenviar = await screen.findByRole("button", {
      name: /Reenviar a revisión el pedido de Valeria Suárez/,
    });
    await user.click(reenviar);

    // Tras reenviar vuelve a revisión: ya no ofrece la acción de reenvío.
    await waitFor(() => {
      expect(
        screen.queryByRole("button", { name: /Reenviar a revisión el pedido de Valeria Suárez/ }),
      ).not.toBeInTheDocument();
    });
  });
});
