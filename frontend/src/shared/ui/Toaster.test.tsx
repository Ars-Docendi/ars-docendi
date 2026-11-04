import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";

import { Toaster } from "./Toaster";
import { limpiarToasts, mostrarToast } from "./toasts";

afterEach(() => {
  vi.useRealTimers();
  limpiarToasts();
});

describe("Toaster", () => {
  it("shows an error toast with title and description, announced as an alert", () => {
    render(<Toaster />);
    act(() => {
      mostrarToast({ titulo: "No pudimos marcar", descripcion: "Intentá nuevamente." });
    });
    const aviso = screen.getByRole("alert");
    expect(aviso).toHaveTextContent("No pudimos marcar");
    expect(aviso).toHaveTextContent("Intentá nuevamente.");
  });

  it("closes with its button", async () => {
    render(<Toaster />);
    act(() => {
      mostrarToast({ titulo: "Uno" });
    });
    await userEvent.setup().click(screen.getByRole("button", { name: "Cerrar aviso" }));
    expect(screen.queryByText("Uno")).not.toBeInTheDocument();
  });

  it("dismisses itself after 7 seconds and stacks several", () => {
    vi.useFakeTimers();
    render(<Toaster />);
    act(() => {
      mostrarToast({ titulo: "Primero" });
    });
    act(() => {
      vi.advanceTimersByTime(3000);
      mostrarToast({ titulo: "Segundo", tipo: "success" });
    });
    expect(screen.getByText("Primero")).toBeInTheDocument();
    expect(screen.getByText("Segundo")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(4100);
    });
    expect(screen.queryByText("Primero")).not.toBeInTheDocument();
    expect(screen.getByText("Segundo")).toBeInTheDocument();
  });
});
