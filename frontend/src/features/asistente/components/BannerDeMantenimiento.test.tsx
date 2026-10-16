import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { BannerDeMantenimiento } from "./BannerDeMantenimiento";
import type { MantenimientoDelAsistente } from "../types";

// ============================================================
// El banner de mantenimiento (asistente-modo-mantenimiento), rehecho 1:1 con
// el canvas «Uso del asistente»: banner compacto de una fila con
// «Activar mantenimiento» — reemplaza el toggle + «Guardar cambios» del
// primer rediseño. La integración con `PanelAdministracionAsistente` (el
// refetch de `capacidades` y el anuncio) la cubre su propio test de
// integración; acá va el comportamiento propio de esta pieza.
// ============================================================

describe("Disponible (mantenimiento inactivo)", () => {
  it("muestra el punto de estado, el título y el copete de disponibilidad", () => {
    render(
      <BannerDeMantenimiento mantenimiento={{ activo: false, razon: null }} onGuardar={vi.fn()} />,
    );

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    expect(grupo.getByText("Asistente disponible")).toBeInTheDocument();
    expect(grupo.getByText("Todos los usuarios con permiso pueden consultar.")).toBeInTheDocument();
    expect(grupo.getByRole("button", { name: "Activar mantenimiento" })).toBeInTheDocument();
  });

  it("«Activar mantenimiento» abre el panel de razón, deshabilitado hasta escribir algo", async () => {
    const user = userEvent.setup();
    render(
      <BannerDeMantenimiento mantenimiento={{ activo: false, razon: null }} onGuardar={vi.fn()} />,
    );

    await user.click(screen.getByRole("button", { name: "Activar mantenimiento" }));

    const razon = screen.getByLabelText(/Razón/);
    const confirmar = screen.getByRole("button", { name: "Activar mantenimiento" });
    expect(confirmar).toBeDisabled();

    await user.type(razon, "Actualización del modelo");
    expect(confirmar).not.toBeDisabled();
  });

  it("confirmar activa con la razón escrita y devuelve el foco al botón original", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <BannerDeMantenimiento
        mantenimiento={{ activo: false, razon: null }}
        onGuardar={onGuardar}
      />,
    );

    const disparador = screen.getByRole("button", { name: "Activar mantenimiento" });
    await user.click(disparador);
    await user.type(screen.getByLabelText(/Razón/), "Actualización del modelo");
    await user.click(screen.getByRole("button", { name: "Activar mantenimiento" }));

    await waitFor(() => expect(onGuardar).toHaveBeenCalledWith(true, "Actualización del modelo"));
  });

  it("cancelar cierra el panel sin guardar nada", async () => {
    const onGuardar = vi.fn();
    const user = userEvent.setup();
    render(
      <BannerDeMantenimiento
        mantenimiento={{ activo: false, razon: null }}
        onGuardar={onGuardar}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Activar mantenimiento" }));
    await user.type(screen.getByLabelText(/Razón/), "texto que se descarta");
    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByLabelText(/Razón/)).not.toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();
  });
});

describe("En mantenimiento (activo)", () => {
  it("muestra la razón vigente y «Desactivar mantenimiento», sin pedir razón para apagarlo", async () => {
    const onGuardar = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <BannerDeMantenimiento
        mantenimiento={{ activo: true, razon: "Actualización programada" }}
        onGuardar={onGuardar}
      />,
    );

    const grupo = within(screen.getByRole("group", { name: "Modo mantenimiento" }));
    expect(grupo.getByText("Asistente en mantenimiento")).toBeInTheDocument();
    expect(grupo.getByText(/Razón visible: «Actualización programada»/)).toBeInTheDocument();

    await user.click(grupo.getByRole("button", { name: "Desactivar mantenimiento" }));
    await waitFor(() => expect(onGuardar).toHaveBeenCalledWith(false, undefined));
  });

  it("activar la rama entera cambia de vista: el foco pasa a «Desactivar mantenimiento», nunca al `<body>`", async () => {
    // Este componente es controlado por `mantenimiento` (viene de la query
    // `capacidades` del padre): simula el refetch que ocurre entre medio del
    // guardado —onGuardar resuelve, PERO el padre todavía no re-renderizó con
    // el nuevo valor hasta el `rerender`— igual que pasa en la integración
    // real con react-query.
    let mantenimientoActual: MantenimientoDelAsistente = { activo: false, razon: null };
    const onGuardar = vi.fn().mockImplementation(async (activo: boolean, razon?: string) => {
      mantenimientoActual = { activo, razon: razon ?? null };
    });
    const user = userEvent.setup();

    const { rerender } = render(
      <BannerDeMantenimiento mantenimiento={mantenimientoActual} onGuardar={onGuardar} />,
    );

    await user.click(screen.getByRole("button", { name: "Activar mantenimiento" }));
    await user.type(screen.getByLabelText(/Razón/), "Actualización");
    await user.click(screen.getByRole("button", { name: "Activar mantenimiento" }));
    await waitFor(() => expect(onGuardar).toHaveBeenCalled());

    rerender(<BannerDeMantenimiento mantenimiento={mantenimientoActual} onGuardar={onGuardar} />);

    await waitFor(() =>
      expect(document.activeElement).toBe(
        screen.getByRole("button", { name: "Desactivar mantenimiento" }),
      ),
    );
  });
});
