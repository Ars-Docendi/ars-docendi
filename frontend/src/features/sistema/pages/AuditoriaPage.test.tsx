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
      cambiadoPor: "11111111-1111-4111-8111-111111111111",
      requestId: "request-101",
      columnasCambiadas: ["documento", "estado"],
      actor: "Vidal, Ernesto",
      accionEtiqueta: "Alta",
      modulo: "Identidad",
      objeto: "Persona",
      resumen: "Alta de persona · Documento, Estado",
      cambios: [
        {
          campo: "documento",
          etiquetaCampo: "Documento",
          valorAnterior: null,
          valorNuevo: "PERSONA-999-SECRET",
          oculto: true,
        },
        {
          campo: "estado",
          etiquetaCampo: "Estado",
          valorAnterior: null,
          valorNuevo: "activo",
          oculto: false,
        },
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
  it("presenta el resumen legible y conserva el detalle redactado", async () => {
    const user = userEvent.setup();
    renderPagina();

    const registro = await screen.findByRole("row", { name: /Vidal, Ernesto/ });
    expect(within(registro).getByText("Alta")).toBeInTheDocument();
    expect(within(registro).getByText("Identidad")).toBeInTheDocument();
    expect(within(registro).getByText("Alta de persona · Documento, Estado")).toBeInTheDocument();
    expect(within(registro).queryByText("persona-123")).not.toBeInTheDocument();
    expect(screen.queryByText("11111111-1111-4111-8111-111111111111")).not.toBeInTheDocument();
    await user.click(within(registro).getByRole("button", { name: "Ver detalle" }));

    expect(await screen.findByText("activo")).toBeInTheDocument();
    expect(screen.getByText("request-101")).toBeInTheDocument();
    expect(screen.getByText("persona-123")).toBeInTheDocument();
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
    await screen.findByRole("row", { name: /Vidal, Ernesto/ });

    await user.type(screen.getByRole("textbox", { name: "Tabla" }), "personas");
    await user.selectOptions(screen.getByRole("combobox", { name: "Acción" }), "UPDATE");
    await user.type(screen.getByRole("textbox", { name: "Actor" }), "Vidal");
    await user.click(screen.getByRole("button", { name: "Buscar" }));
    await waitFor(() =>
      expect(listarAuditoria).toHaveBeenLastCalledWith(
        expect.objectContaining({
          pagina: 1,
          tamanoPagina: 50,
          tabla: "personas",
          accion: "UPDATE",
          actor: "Vidal",
        }),
      ),
    );

    await user.click(screen.getByRole("button", { name: "Siguiente" }));
    await waitFor(() =>
      expect(listarAuditoria).toHaveBeenLastCalledWith(
        expect.objectContaining({ pagina: 2, tabla: "personas", accion: "UPDATE", actor: "Vidal" }),
      ),
    );
  });

  it("prioriza los cinco encabezados de lectura rápida", async () => {
    renderPagina();

    const tabla = await screen.findByRole("table", { name: "Registros de auditoría" });

    expect(
      within(tabla)
        .getAllByRole("columnheader")
        .map((encabezado) => encabezado.textContent),
    ).toEqual(["Fecha", "Usuario", "Acción", "Módulo", "Cambio"]);
  });
});
