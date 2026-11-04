import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import { CampanaSidebar, CampanaTopBar } from "./CampanaNotificaciones";

const sinAccion = () => {};

describe("CampanaSidebar", () => {
  it("expanded: label, no badge and the 'ninguna sin leer' name at zero", () => {
    render(<CampanaSidebar cantidad={0} abierta={false} colapsada={false} onClick={sinAccion} />);
    const campana = screen.getByRole("button", { name: "Notificaciones, ninguna sin leer" });
    expect(campana).toHaveTextContent("Notificaciones");
    expect(screen.queryByTestId("insignia-pildora")).not.toBeInTheDocument();
    expect(screen.queryByTestId("insignia-esquina")).not.toBeInTheDocument();
    expect(campana).toHaveAttribute("aria-expanded", "false");
    expect(campana).toHaveAttribute("aria-haspopup", "dialog");
  });

  it("expanded: shows the count pill with the singular and plural names", () => {
    const { rerender } = render(
      <CampanaSidebar cantidad={1} abierta={false} colapsada={false} onClick={sinAccion} />,
    );
    expect(
      screen.getByRole("button", { name: "Notificaciones, 1 notificación sin leer" }),
    ).toBeInTheDocument();
    expect(screen.getByTestId("insignia-pildora")).toHaveTextContent("1");

    rerender(
      <CampanaSidebar cantidad={12} abierta={false} colapsada={false} onClick={sinAccion} />,
    );
    expect(
      screen.getByRole("button", { name: "Notificaciones, 12 notificaciones sin leer" }),
    ).toBeInTheDocument();
  });

  it("caps the badge at 99+ but keeps the real count in the name", () => {
    render(<CampanaSidebar cantidad={150} abierta={false} colapsada={false} onClick={sinAccion} />);
    expect(screen.getByTestId("insignia-pildora")).toHaveTextContent("99+");
    expect(
      screen.getByRole("button", { name: "Notificaciones, 150 notificaciones sin leer" }),
    ).toBeInTheDocument();
  });

  it("collapsed: no label or pill, a corner badge and the tooltip", () => {
    render(<CampanaSidebar cantidad={5} abierta={false} colapsada onClick={sinAccion} />);
    const campana = screen.getByRole("button", {
      name: "Notificaciones, 5 notificaciones sin leer",
    });
    expect(campana).not.toHaveTextContent("Notificaciones");
    expect(campana).toHaveAttribute("data-tooltip", "Notificaciones");
    expect(screen.getByTestId("insignia-esquina")).toHaveTextContent("5");
  });

  it("an unknown count (still loading) shows no badge", () => {
    render(<CampanaSidebar cantidad={undefined} abierta colapsada={false} onClick={sinAccion} />);
    expect(
      screen.getByRole("button", { name: "Notificaciones, ninguna sin leer" }),
    ).toHaveAttribute("aria-expanded", "true");
  });
});

describe("CampanaTopBar", () => {
  it("shows the corner badge only above zero", () => {
    const { rerender } = render(<CampanaTopBar cantidad={0} abierta={false} onClick={sinAccion} />);
    expect(screen.queryByTestId("insignia-esquina")).not.toBeInTheDocument();
    rerender(<CampanaTopBar cantidad={120} abierta={false} onClick={sinAccion} />);
    expect(screen.getByTestId("insignia-esquina")).toHaveTextContent("99+");
    expect(
      screen.getByRole("button", { name: "Notificaciones, 120 notificaciones sin leer" }),
    ).toBeInTheDocument();
  });
});
