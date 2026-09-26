import { describe, it, expect, vi, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { VotoDeRetroalimentacion } from "./components/VotoDeRetroalimentacion";
import * as api from "./api/asistenteApi";
import { montar } from "./test/soporte";

// ============================================================
// Thumbs up/down on an answered turn (asistente-retroalimentacion,
// asistente-superficie-frontend, asistente-accesibilidad).
// ============================================================

const TOKEN = "11111111-1111-4111-8111-111111111111";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("Whether the control renders at all", () => {
  it("renders nothing without a feedback token", () => {
    const { container } = montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={null} />);

    expect(container.textContent).toBe("");
  });

  it("renders both buttons when there is a token", () => {
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    expect(screen.getByRole("button", { name: "Me sirvió" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "No me sirvió" })).toBeInTheDocument();
  });
});

describe("aria-pressed and keyboard operation", () => {
  it("starts with neither button pressed", () => {
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    expect(screen.getByRole("button", { name: "No me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("marks the thumbs-up button pressed after voting, and toggles on a keyboard activation", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.tab();
    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveFocus();
    await user.keyboard("{Enter}");

    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    // El foco no se movió: sigue en el mismo botón que se activó.
    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveFocus();
  });

  it("both buttons are reachable in sequence with Tab", async () => {
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.tab();
    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole("button", { name: "No me sirvió" })).toHaveFocus();
  });
});

describe("The reason picker on a thumbs-down", () => {
  it("shows the four reason choices before sending", async () => {
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No me sirvió" }));

    expect(screen.getByRole("button", { name: "Datos incorrectos" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "No entendió la pregunta" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Es lento" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Otro motivo" })).toBeInTheDocument();
  });

  it("submitting without a reason still sends the thumbs-down vote", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No me sirvió" }));
    await user.click(screen.getByRole("button", { name: "Enviar sin especificar motivo" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: false, razon: undefined });
    expect(screen.getByRole("button", { name: "No me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("picking a reason sends it along with the vote", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No me sirvió" }));
    await user.click(screen.getByRole("button", { name: "Es lento" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: false, razon: "lento" });
  });
});

describe("Confirmation and changing the vote", () => {
  it("announces the recorded vote via the ancestor live region, without an alert or a moved focus", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(
      <ul role="log" aria-live="polite" aria-label="Conversación con el asistente">
        <li>
          <VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />
        </li>
      </ul>,
    );

    await user.click(screen.getByRole("button", { name: "Me sirvió" }));

    expect(screen.getByText("Se registró tu voto: te sirvió.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveFocus();
  });

  it("the user can change a recorded vote", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "Me sirvió" }));
    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    await user.click(screen.getByRole("button", { name: "No me sirvió" }));
    await user.click(screen.getByRole("button", { name: "Enviar sin especificar motivo" }));

    expect(screen.getByRole("button", { name: "No me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(screen.getByRole("button", { name: "Me sirvió" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("calls the API against the feedback endpoint with the turn's token", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "Me sirvió" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: true, razon: undefined });
  });
});
