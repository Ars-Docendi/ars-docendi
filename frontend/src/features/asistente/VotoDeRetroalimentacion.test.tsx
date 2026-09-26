import { describe, it, expect, vi, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { VotoDeRetroalimentacion } from "./components/VotoDeRetroalimentacion";
import * as api from "./api/asistenteApi";
import { montar } from "./test/soporte";

// ============================================================
// Thumbs up/down on an answered turn, v3 (asistente-retroalimentacion,
// asistente-superficie-frontend, asistente-accesibilidad, design.md D6/D7/D14
// de asistente-rediseno-v3): íconos en vez de texto, panel de pastillas de
// elección única, sin campo de texto libre.
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

  it("renders both icon buttons when there is a token", () => {
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    expect(screen.getByRole("button", { name: "Sirvió" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "No sirvió" })).toBeInTheDocument();
  });
});

describe("aria-pressed and keyboard operation", () => {
  it("starts with neither button pressed", () => {
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "No sirvió" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("marks «Sirvió» pressed after voting, and toggles on a keyboard activation", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.tab();
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveFocus();
    await user.keyboard("{Enter}");

    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveAttribute("aria-pressed", "true");
    // El foco no se movió: sigue en el mismo botón que se activó.
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveFocus();
  });

  it("both buttons are reachable in sequence with Tab", async () => {
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.tab();
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole("button", { name: "No sirvió" })).toHaveFocus();
  });
});

describe("El panel «¿Qué falló? Opcional» en un «No sirvió»", () => {
  it("shows the four reason pills before sending, and no text input", async () => {
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));

    expect(screen.getByText(/¿Qué falló\?/)).toBeInTheDocument();
    expect(screen.getByText("Opcional")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Datos incorrectos" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "No entendió la pregunta" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Faltan datos" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Otro" })).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).toBeNull();
  });

  it("«Omitir» sends the thumbs-down vote with no reason", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Omitir" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: false, razon: undefined });
    expect(screen.getByRole("button", { name: "No sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("«Enviar» sin elegir ninguna pastilla también manda sin motivo", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Enviar" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: false, razon: undefined });
  });

  it("picking a reason and sending includes it", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Faltan datos" }));
    await user.click(screen.getByRole("button", { name: "Enviar" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: false, razon: "faltan_datos" });
  });

  it("sólo una pastilla puede estar elegida a la vez", async () => {
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Datos incorrectos" }));
    expect(screen.getByRole("button", { name: "Datos incorrectos" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    await user.click(screen.getByRole("button", { name: "Faltan datos" }));

    expect(screen.getByRole("button", { name: "Faltan datos" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(screen.getByRole("button", { name: "Datos incorrectos" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("tocar la pastilla ya elegida la deselecciona", async () => {
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Otro" }));
    await user.click(screen.getByRole("button", { name: "Otro" }));

    expect(screen.getByRole("button", { name: "Otro" })).toHaveAttribute("aria-pressed", "false");
  });
});

describe("Confirmation and changing the vote", () => {
  it("«Sirvió» announces via the ancestor live region, without an alert or a moved focus", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(
      <ul role="log" aria-live="polite" aria-label="Conversación con el asistente">
        <li>
          <VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />
        </li>
      </ul>,
    );

    await user.click(screen.getByRole("button", { name: "Sirvió" }));

    expect(screen.getByText("Se registró tu voto: te sirvió.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveFocus();
  });

  it("después de enviar un «No sirvió» se ve el agradecimiento", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Omitir" }));

    expect(
      screen.getByText("Gracias. Tu comentario ayuda a mejorar el asistente."),
    ).toBeInTheDocument();
    // El panel se cierra al enviar.
    expect(screen.queryByText(/¿Qué falló\?/)).toBeNull();
  });

  it("the user can change a recorded vote", async () => {
    vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "Sirvió" }));
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveAttribute("aria-pressed", "true");

    await user.click(screen.getByRole("button", { name: "No sirvió" }));
    await user.click(screen.getByRole("button", { name: "Omitir" }));

    expect(screen.getByRole("button", { name: "No sirvió" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(screen.getByRole("button", { name: "Sirvió" })).toHaveAttribute("aria-pressed", "false");
  });

  it("calls the API against the feedback endpoint with the turn's token", async () => {
    const enviar = vi.spyOn(api, "enviarRetroalimentacion").mockResolvedValue(undefined);
    const user = userEvent.setup();
    montar(<VotoDeRetroalimentacion claveDeRetroalimentacion={TOKEN} />);

    await user.click(screen.getByRole("button", { name: "Sirvió" }));

    expect(enviar).toHaveBeenCalledWith({ token: TOKEN, voto: true, razon: undefined });
  });
});
