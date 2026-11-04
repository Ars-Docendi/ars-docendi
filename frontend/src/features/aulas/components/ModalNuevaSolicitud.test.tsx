import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ModalNuevaSolicitud } from "./ModalNuevaSolicitud";

const api = vi.hoisted(() => ({
  listarMateriasPropias: vi.fn(),
  crearSolicitud: vi.fn(),
}));
vi.mock("../api/solicitudesApi", () => api);

function renderizar(onCerrar = vi.fn()) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <ModalNuevaSolicitud onCerrar={onCerrar} />
    </QueryClientProvider>,
  );
  return onCerrar;
}

beforeEach(() => {
  vi.clearAllMocks();
  api.listarMateriasPropias.mockResolvedValue([
    { id: "m1", codigo: "3001", nombre: "Ingeniería de Software" },
  ]);
  api.crearSolicitud.mockResolvedValue({});
});

describe("ModalNuevaSolicitud", () => {
  it("on a failed submit shows each field error and focuses the first invalid field", async () => {
    const usuario = userEvent.setup();
    renderizar();
    await screen.findByRole("option", { name: "Ingeniería de Software" });

    await usuario.type(screen.getByLabelText(/^Día/), "31/02/2026");
    await usuario.type(screen.getByLabelText(/^Horario desde/), "2561");
    await usuario.click(screen.getByRole("button", { name: "Generar solicitud" }));

    expect(screen.getByText("Elegí una materia.")).toBeInTheDocument();
    expect(screen.getByText("Ingresá la comisión.")).toBeInTheDocument();
    expect(screen.getByText("Ingresá una fecha válida (dd/mm/aaaa).")).toBeInTheDocument();
    expect(screen.getByText("Ingresá un horario válido (hh:mm).")).toBeInTheDocument();
    expect(screen.getByText("Completá el horario hasta.")).toBeInTheDocument();
    expect(screen.getByLabelText(/^Materia/)).toHaveFocus();
    expect(screen.getByLabelText(/^Día/)).toHaveAttribute("aria-describedby", "nueva-dia-error");
    expect(api.crearSolicitud).not.toHaveBeenCalled();
  });

  it("sends ISO values to the API and closes on success", async () => {
    const usuario = userEvent.setup();
    const onCerrar = renderizar();
    await screen.findByRole("option", { name: "Ingeniería de Software" });

    await usuario.selectOptions(screen.getByLabelText(/^Materia/), "m1");
    await usuario.type(screen.getByLabelText(/^Comisión/), "K3001");
    await usuario.type(screen.getByLabelText(/^Día/), "20112026");
    await usuario.type(screen.getByLabelText(/^Horario desde/), "0800");
    await usuario.type(screen.getByLabelText(/^Horario hasta/), "1000");
    await usuario.type(screen.getByLabelText(/^Cantidad aproximada/), "35");
    await usuario.click(screen.getByRole("button", { name: "Generar solicitud" }));

    expect(api.crearSolicitud).toHaveBeenCalledWith({
      dia: "2026-11-20",
      horarioDesde: "08:00",
      horarioHasta: "10:00",
      cantidadAlumnosAprox: 35,
      materiaId: "m1",
      comision: "K3001",
    });
    await vi.waitFor(() => expect(onCerrar).toHaveBeenCalled());
  });

  it("offers a real Reintentar button when the subjects fail to load", async () => {
    const usuario = userEvent.setup();
    api.listarMateriasPropias.mockRejectedValueOnce(new Error("boom"));
    renderizar();

    const reintentar = await screen.findByRole("button", { name: "Reintentar" });
    await usuario.click(reintentar);

    expect(
      await screen.findByRole("option", { name: "Ingeniería de Software" }),
    ).toBeInTheDocument();
  });

  it("blocks submitting when the teacher has no assigned subjects", async () => {
    api.listarMateriasPropias.mockResolvedValue([]);
    renderizar();

    expect(await screen.findByText("No tenés materias asignadas")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Generar solicitud" })).toBeDisabled();
  });
});
