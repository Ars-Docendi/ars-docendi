import { describe, it, expect, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { usePreferenciaDelRail } from "./hooks/usePreferenciaDelRail";

// ============================================================
// La preferencia de ancho del rail (design.md D2 de asistente-rediseno-v3,
// tasks.md 1.4): por usuario, en `localStorage`, sin romper si el storage
// está bloqueado.
// ============================================================

/** Un arnés mínimo: pinta el estado y un botón para alternarlo. */
function Arnes({ userId }: { userId: string | undefined }) {
  const { colapsado, alternar } = usePreferenciaDelRail(userId);
  return (
    <button type="button" onClick={alternar}>
      {colapsado ? "colapsado" : "expandido"}
    </button>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("usePreferenciaDelRail", () => {
  it("por default arranca expandido", () => {
    render(<Arnes userId="usuario-1" />);

    expect(screen.getByRole("button")).toHaveTextContent("expandido");
  });

  it("sobrevive a un remount, para el mismo usuario", async () => {
    const user = userEvent.setup();
    const { unmount } = render(<Arnes userId="usuario-1" />);

    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("button")).toHaveTextContent("colapsado");

    unmount();
    render(<Arnes userId="usuario-1" />);

    expect(screen.getByRole("button")).toHaveTextContent("colapsado");
  });

  it("es por usuario: otro usuario en el mismo navegador la ve expandida", async () => {
    const user = userEvent.setup();
    const { unmount } = render(<Arnes userId="usuario-1" />);

    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("button")).toHaveTextContent("colapsado");

    unmount();
    render(<Arnes userId="usuario-2" />);

    expect(screen.getByRole("button")).toHaveTextContent("expandido");
  });

  it("un storage que tira sigue alternando, sólo que no lo recuerda", async () => {
    const original = Storage.prototype.getItem;
    const originalSet = Storage.prototype.setItem;
    Storage.prototype.getItem = () => {
      throw new Error("storage bloqueado");
    };
    Storage.prototype.setItem = () => {
      throw new Error("storage bloqueado");
    };

    try {
      const user = userEvent.setup();
      render(<Arnes userId="usuario-1" />);

      expect(screen.getByRole("button")).toHaveTextContent("expandido");

      await user.click(screen.getByRole("button"));

      expect(screen.getByRole("button")).toHaveTextContent("colapsado");
    } finally {
      Storage.prototype.getItem = original;
      Storage.prototype.setItem = originalSet;
    }
  });
});
