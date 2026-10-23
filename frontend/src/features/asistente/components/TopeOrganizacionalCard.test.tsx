import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { TopeOrganizacionalCard } from "./TopeOrganizacionalCard";

// El espacio entre «US$» y el número lo decide `Intl.NumberFormat` (puede ser
// un NBSP u otro espacio angosto según el motor de ICU): los matchers usan
// `\s` en vez de un carácter literal, para no acoplarse a esa decisión.

describe("TopeOrganizacionalCard", () => {
  it("mientras GET …/presupuestos está en vuelo, muestra un estado de carga", () => {
    render(
      <TopeOrganizacionalCard
        topeConocido={undefined}
        gastoEstimadoDelMes={undefined}
        error={false}
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    expect(screen.getByText("Cargando…")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Editar tope" })).not.toBeInTheDocument();
  });

  it("si GET …/presupuestos falla, lo dice y no ofrece editar", () => {
    render(
      <TopeOrganizacionalCard
        topeConocido={undefined}
        gastoEstimadoDelMes={undefined}
        error
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    expect(screen.getByText("No se pudo cargar el tope organizacional.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Editar tope" })).not.toBeInTheDocument();
  });

  it("el título lleva el mes en curso, «Tope de {Mes}» (fidelidad con el canvas)", () => {
    render(
      <TopeOrganizacionalCard
        topeConocido={0}
        gastoEstimadoDelMes={0}
        error={false}
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    const mesEnCurso = new Intl.DateTimeFormat("es-AR", { month: "long" }).format(new Date());
    const etiqueta = `Tope de ${mesEnCurso.charAt(0).toUpperCase()}${mesEnCurso.slice(1)}`;
    expect(screen.getByText(etiqueta)).toBeInTheDocument();
  });

  it("tope en 0 (desactivado): el gasto se ve igual, «gastado este mes», y ninguna barra", () => {
    render(
      <TopeOrganizacionalCard
        topeConocido={0}
        gastoEstimadoDelMes={0}
        error={false}
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    expect(screen.getByText("US$ 0,00")).toBeInTheDocument();
    // «de sin tope» (copiado del canvas) no se lee; sin tope no hay «de».
    expect(screen.getByText("gastado este mes")).toBeInTheDocument();
    expect(screen.queryByText(/de sin tope/)).not.toBeInTheDocument();
    expect(screen.getByText("Sin tope: el gasto no bloquea consultas.")).toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });

  it("con tope activo, el gasto es el número principal y la barra muestra el porcentaje usado", () => {
    render(
      <TopeOrganizacionalCard
        topeConocido={100}
        gastoEstimadoDelMes={85}
        error={false}
        onGuardar={vi.fn()}
        onGuardado={vi.fn()}
      />,
    );

    expect(screen.getByText("US$ 85,00")).toBeInTheDocument();
    expect(screen.getByText(/^de\s+US\$\s*100,00\s*\(estimado\)$/)).toBeInTheDocument();
    expect(screen.getByRole("progressbar", { name: /Gasto del mes/ })).toHaveAttribute(
      "aria-valuenow",
      "85",
    );
    expect(screen.getByText(/85\s*% usado/)).toBeInTheDocument();
  });

  it("editar el tope persistido y guardar el primer valor no pide confirmación", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const onGuardado = vi.fn();
    const user = userEvent.setup();

    render(
      <TopeOrganizacionalCard
        topeConocido={0}
        gastoEstimadoDelMes={0}
        error={false}
        onGuardar={onGuardar}
        onGuardado={onGuardado}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar tope" }));
    await user.clear(screen.getByLabelText("Tope mensual (USD)"));
    await user.type(screen.getByLabelText("Tope mensual (USD)"), "150.5");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(onGuardar).toHaveBeenCalledWith(150.5));
    expect(onGuardado).toHaveBeenCalledWith(
      expect.stringMatching(/^Tope organizacional mensual: US\$\s*150,50\.$/),
    );
    // El valor mostrado lo controla el padre (`topeConocido` es un prop, no
    // estado propio de la tarjeta): acá sólo hace falta ver que volvió a modo
    // vista. La integración con el valor recién guardado la cubre
    // `PanelAdministracionAsistente.test.tsx`.
    expect(screen.getByRole("button", { name: "Editar tope" })).toBeInTheDocument();
  });

  it("bajar un tope ya conocido pide confirmación inline antes de aplicarlo", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();

    render(
      <TopeOrganizacionalCard
        topeConocido={200}
        gastoEstimadoDelMes={10}
        error={false}
        onGuardar={onGuardar}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar tope" }));
    await user.clear(screen.getByLabelText("Tope mensual (USD)"));
    await user.type(screen.getByLabelText("Tope mensual (USD)"), "50");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    const grupo = within(screen.getByRole("group", { name: "Tope organizacional mensual" }));
    expect(await grupo.findByText(/¿Bajar de US\$\s*200,00 a US\$\s*50,00\?/)).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();

    await user.click(grupo.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(onGuardar).toHaveBeenCalledWith(50));
  });

  it("cancelar vuelve a mostrar el valor conocido sin guardar nada", async () => {
    const onGuardar = vi.fn();
    const user = userEvent.setup();

    render(
      <TopeOrganizacionalCard
        topeConocido={80}
        gastoEstimadoDelMes={5}
        error={false}
        onGuardar={onGuardar}
        onGuardado={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Editar tope" }));
    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.getByText(/^de\s+US\$\s*80,00\s*\(estimado\)$/)).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();
  });
});
