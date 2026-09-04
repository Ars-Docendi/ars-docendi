import { describe, it, expect } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { MisPedidosPage } from "./MisPedidosPage";

// Integración: monta la página con el stack real de hooks → api mock → store,
// usando el usuario stub (Jefe de Cátedra). Verifica Loading → Success, el
// filtro estilo Usuarios, la fila clickeable (navega al detalle) y el botón
// "Editar" (solo en pedidos editables, sin disparar la navegación al detalle).
function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={["/designaciones/mis-pedidos"]}>
        <Routes>
          <Route path="/designaciones/mis-pedidos" element={<MisPedidosPage />} />
          <Route path="/designaciones/pedidos/:id" element={<p>Detalle del pedido</p>} />
          <Route path="/designaciones/pedidos/:id/editar" element={<p>Editar pedido</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("MisPedidosPage (integración)", () => {
  it("muestra Loading y luego los pedidos del seed, sin prefijo 'Prof.'", async () => {
    renderPage();

    expect(screen.getByText("Cargando tus pedidos…")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });
    expect(screen.getByText("Valeria Suárez")).toBeInTheDocument();
    expect(screen.queryByText(/Prof\./)).not.toBeInTheDocument();
  });

  it("la columna de tipo usa el header 'TIPO'", async () => {
    renderPage();
    await waitFor(() => {
      expect(screen.getByRole("columnheader", { name: "TIPO" })).toBeInTheDocument();
    });
    expect(screen.queryByRole("columnheader", { name: "NOVEDAD" })).not.toBeInTheDocument();
  });

  it("la columna LEGAJO muestra el legajo del docente", async () => {
    renderPage();
    await waitFor(() => {
      expect(screen.getByRole("columnheader", { name: "LEGAJO" })).toBeInTheDocument();
    });
    const filaLaura = screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" });
    expect(within(filaLaura).getByText("1002")).toBeInTheDocument();
  });

  it("click en la fila navega al detalle del pedido", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });
    await user.click(screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" }));

    expect(await screen.findByText("Detalle del pedido")).toBeInTheDocument();
  });

  it("todas las filas tienen el botón Ver", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });
    for (const nombre of ["Laura Giménez", "Pablo Herrera", "Valeria Suárez", "Brenda Ortiz"]) {
      const fila = screen.getByRole("row", { name: `Ver el pedido de ${nombre}` });
      expect(within(fila).getByRole("button", { name: "Ver" })).toBeInTheDocument();
    }
  });

  it("el botón Editar es fijo: habilitado en editables, deshabilitado (no oculto) en el resto", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });

    const filaLaura = screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" }); // borrador
    const filaPablo = screen.getByRole("row", { name: "Ver el pedido de Pablo Herrera" }); // devuelto
    const filaValeria = screen.getByRole("row", { name: "Ver el pedido de Valeria Suárez" }); // en revisión
    const filaBrenda = screen.getByRole("row", { name: "Ver el pedido de Brenda Ortiz" }); // rechazado

    expect(within(filaLaura).getByRole("button", { name: "Editar" })).toBeEnabled();
    expect(within(filaPablo).getByRole("button", { name: "Editar" })).toBeEnabled();
    expect(within(filaValeria).getByRole("button", { name: "Editar" })).toBeDisabled();
    expect(within(filaBrenda).getByRole("button", { name: "Editar" })).toBeDisabled();
  });

  it("la X de eliminar es fija: habilitada solo en borrador, deshabilitada (no oculta) en el resto", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });

    const filaLaura = screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" }); // borrador
    const filaPablo = screen.getByRole("row", { name: "Ver el pedido de Pablo Herrera" }); // devuelto

    expect(within(filaLaura).getByRole("button", { name: /Eliminar pedido/ })).toBeEnabled();
    expect(within(filaPablo).getByRole("button", { name: /Eliminar pedido/ })).toBeDisabled();
  });

  it("click en Editar navega a la edición sin disparar la navegación al detalle", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });
    const filaLaura = screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" });
    await user.click(within(filaLaura).getByRole("button", { name: "Editar" }));

    expect(await screen.findByText("Editar pedido")).toBeInTheDocument();
    expect(screen.queryByText("Detalle del pedido")).not.toBeInTheDocument();
  });

  it("click en Ver navega al detalle sin disparar el click de la fila dos veces", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });
    const filaLaura = screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" });
    await user.click(within(filaLaura).getByRole("button", { name: "Ver" }));

    expect(await screen.findByText("Detalle del pedido")).toBeInTheDocument();
  });

  it("eliminar un borrador pide confirmación y lo saca de la lista", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
    });
    const filaLaura = screen.getByRole("row", { name: "Ver el pedido de Laura Giménez" });
    await user.click(within(filaLaura).getByRole("button", { name: /Eliminar pedido/ }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/Laura Giménez/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole("button", { name: "Eliminar" }));

    await waitFor(() => {
      expect(screen.queryByText("Laura Giménez")).not.toBeInTheDocument();
    });
  });

  describe("filtro estilo Usuarios", () => {
    it("filtrar por Docente acota la lista", async () => {
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => {
        expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      });
      await user.type(screen.getByLabelText("Filtrar por docente"), "valeria");

      expect(screen.getByText("Valeria Suárez")).toBeInTheDocument();
      expect(screen.queryByText("Laura Giménez")).not.toBeInTheDocument();
    });

    it("filtrar por N° acota la lista", async () => {
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => {
        expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      });
      const numeroLaura = screen
        .getByRole("row", {
          name: "Ver el pedido de Laura Giménez",
        })
        .querySelector(".adoc-mp-num")!.textContent!;

      await user.type(screen.getByLabelText("Filtrar por N°"), numeroLaura);

      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      expect(screen.queryByText("Valeria Suárez")).not.toBeInTheDocument();
    });

    it("agregar y quitar el filtro opcional Legajo", async () => {
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => {
        expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      });

      await user.selectOptions(screen.getByLabelText("Añadir filtro"), "legajo");
      await user.type(screen.getByLabelText("Filtrar por legajo"), "1005");

      expect(screen.getByText("Valeria Suárez")).toBeInTheDocument();
      expect(screen.queryByText("Laura Giménez")).not.toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Quitar filtro de legajo" }));

      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      expect(screen.queryByLabelText("Filtrar por legajo")).not.toBeInTheDocument();
    });

    it("agregar y quitar el filtro opcional Estado", async () => {
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => {
        expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      });

      await user.selectOptions(screen.getByLabelText("Añadir filtro"), "estado");
      await user.selectOptions(screen.getByLabelText("Filtrar por estado"), "rechazado");

      expect(screen.getByText("Brenda Ortiz")).toBeInTheDocument();
      expect(screen.queryByText("Laura Giménez")).not.toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Quitar filtro de estado" }));

      expect(screen.getByText("Laura Giménez")).toBeInTheDocument();
      expect(screen.queryByLabelText("Filtrar por estado")).not.toBeInTheDocument();
    });
  });
});
