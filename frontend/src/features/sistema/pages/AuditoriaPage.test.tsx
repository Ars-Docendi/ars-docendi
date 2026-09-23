import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { listarAuditoria } from "../api/sistemaApi";
import type { PaginaAuditoria } from "../api/sistemaApi";
import { AuditoriaPage } from "./AuditoriaPage";

vi.mock("../api/sistemaApi", () => ({ listarAuditoria: vi.fn() }));

const pagina: PaginaAuditoria = {
  pagina: 1,
  tamanoPagina: 50,
  total: 51,
  elementos: [
    {
      id: 101,
      schema: "identity",
      tabla: "personas",
      rowPk: "persona-123",
      accion: "INSERT",
      cambiadoEn: "2026-09-23T17:00:00Z",
      cambiadoPor: null,
      requestId: null,
      columnasCambiadas: ["documento", "estado"],
      cambios: [
        { campo: "documento", valorAnterior: null, valorNuevo: "PERSONA-999-SECRET", oculto: true },
        { campo: "estado", valorAnterior: null, valorNuevo: "activo", oculto: false },
      ],
    },
  ],
};

function renderPagina() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={cliente}>
      <AuditoriaPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(listarAuditoria).mockReset().mockResolvedValue(pagina);
});

describe("Registros de auditoría", () => {
  it("muestra metadatos y oculta valores marcados por la política", async () => {
    const user = userEvent.setup();
    renderPagina();

    const registro = await screen.findByRole("row", { name: /persona-123/ });
    expect(within(registro).getByText("INSERT")).toBeInTheDocument();
    expect(within(registro).getByText(/documento, estado/)).toBeInTheDocument();
    await user.click(within(registro).getByRole("button", { name: "Ver detalle" }));

    expect(await screen.findByText("activo")).toBeInTheDocument();
    expect(screen.getAllByText("Enmascarado por política").length).toBeGreaterThan(0);
    expect(screen.queryByText("PERSONA-999-SECRET")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Siguiente" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Eliminar" })).not.toBeInTheDocument();
  });

  it("muestra un estado vacío y recupera la consulta después de un error", async () => {
    const user = userEvent.setup();
    vi.mocked(listarAuditoria)
      .mockRejectedValueOnce(new Error("detalle interno de conexión"))
      .mockResolvedValueOnce({ ...pagina, total: 0, elementos: [] });
    renderPagina();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "No se pudieron cargar los registros",
    );
    expect(screen.queryByText("detalle interno de conexión")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reintentar" }));
    expect(
      await screen.findByText("No hay registros para los filtros seleccionados."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Siguiente" })).toBeDisabled();
  });

  it("aplica filtros y pagina los resultados", async () => {
    const user = userEvent.setup();
    renderPagina();
    await screen.findByRole("row", { name: /persona-123/ });

    await user.type(screen.getByRole("textbox", { name: "Tabla" }), "personas");
    await user.selectOptions(screen.getByRole("combobox", { name: "Acción" }), "UPDATE");
    await user.click(screen.getByRole("button", { name: "Buscar" }));
    await waitFor(() =>
      expect(listarAuditoria).toHaveBeenLastCalledWith(
        expect.objectContaining({
          pagina: 1,
          tamanoPagina: 50,
          tabla: "personas",
          accion: "UPDATE",
        }),
      ),
    );

    await user.click(screen.getByRole("button", { name: "Siguiente" }));
    await waitFor(() =>
      expect(listarAuditoria).toHaveBeenLastCalledWith(
        expect.objectContaining({ pagina: 2, tabla: "personas", accion: "UPDATE" }),
      ),
    );
  });
});
