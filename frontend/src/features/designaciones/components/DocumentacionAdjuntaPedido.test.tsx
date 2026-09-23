import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Adjunto } from "../types";
import { descargarAdjuntoPedido } from "../api/pedidosApi";
import { DocumentacionAdjuntaPedido } from "./DocumentacionAdjuntaPedido";

vi.mock("../api/pedidosApi", () => ({
  descargarAdjuntoPedido: vi.fn(),
}));

const disponibles: Adjunto[] = [
  {
    id: "adjunto-1",
    tipo: "cv",
    nombre: "cv.pdf",
    archivoId: "archivo-1",
    estadoArchivo: "disponible",
  },
  {
    id: "adjunto-2",
    tipo: "dni_frente",
    nombre: "frente.png",
    archivoId: "archivo-2",
    estadoArchivo: "disponible",
  },
  {
    id: "adjunto-legacy",
    tipo: "justificativo",
    nombre: "legacy.pdf",
    estadoArchivo: "legacy",
  },
];

let ventanaNueva: Window;

beforeEach(() => {
  vi.clearAllMocks();
  ventanaNueva = {
    location: { href: "" },
    opener: window,
    close: vi.fn(),
  } as unknown as Window;
  vi.stubGlobal("URL", {
    createObjectURL: vi.fn(() => "blob:adjunto"),
    revokeObjectURL: vi.fn(),
  });
  vi.spyOn(window, "open").mockReturnValue(ventanaNueva);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("DocumentacionAdjuntaPedido", () => {
  it("ofrece una acción por archivo disponible y conserva el legacy como metadata", () => {
    render(<DocumentacionAdjuntaPedido pedidoId="pedido-1" adjuntos={disponibles} />);

    expect(screen.getByRole("button", { name: "Abrir adjunto CV: cv.pdf" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Abrir adjunto DNI (frente): frente.png" }),
    ).toBeInTheDocument();
    expect(screen.getByText("CV · PDF")).toBeInTheDocument();
    expect(screen.getByText("DNI (frente) · PNG")).toBeInTheDocument();
    expect(screen.getByText("legacy.pdf")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /legacy\.pdf/i })).not.toBeInTheDocument();
  });

  it("abre el adjunto en una pestaña nueva al activarlo con teclado", async () => {
    const contenido = new Blob(["%PDF-1.7"], { type: "application/pdf" });
    vi.mocked(descargarAdjuntoPedido).mockResolvedValue(contenido);
    const user = userEvent.setup();

    render(<DocumentacionAdjuntaPedido pedidoId="pedido-1" adjuntos={disponibles} />);
    const boton = screen.getByRole("button", { name: "Abrir adjunto CV: cv.pdf" });
    boton.focus();
    await user.keyboard("{Enter}");

    expect(descargarAdjuntoPedido).toHaveBeenCalledWith("pedido-1", "archivo-1");
    expect(window.open).toHaveBeenCalledWith("", "_blank");
    expect(ventanaNueva.location.href).toBe("blob:adjunto");
  });

  it("abre la pestaña antes de esperar la respuesta de descarga", async () => {
    let resolver!: (contenido: Blob) => void;
    const descargaPendiente = new Promise<Blob>((resolve) => {
      resolver = resolve;
    });
    vi.mocked(descargarAdjuntoPedido).mockReturnValue(descargaPendiente);
    const user = userEvent.setup();

    render(<DocumentacionAdjuntaPedido pedidoId="pedido-1" adjuntos={[disponibles[0]]} />);
    await user.click(screen.getByRole("button", { name: "Abrir adjunto CV: cv.pdf" }));

    expect(window.open).toHaveBeenCalledWith("", "_blank");
    expect(descargarAdjuntoPedido).toHaveBeenCalledWith("pedido-1", "archivo-1");

    resolver(new Blob(["%PDF-1.7"], { type: "application/pdf" }));
    await Promise.resolve();
    await Promise.resolve();
    expect(ventanaNueva.location.href).toBe("blob:adjunto");
  });

  it("libera la URL temporal después de abrir el adjunto", async () => {
    vi.useFakeTimers();
    vi.mocked(descargarAdjuntoPedido).mockResolvedValue(
      new Blob(["%PDF-1.7"], { type: "application/pdf" }),
    );

    render(<DocumentacionAdjuntaPedido pedidoId="pedido-1" adjuntos={[disponibles[0]]} />);
    fireEvent.click(screen.getByRole("button", { name: "Abrir adjunto CV: cv.pdf" }));
    await Promise.resolve();
    await Promise.resolve();

    vi.advanceTimersByTime(60_000);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:adjunto");
    vi.useRealTimers();
  });

  it("muestra un error si el navegador bloquea la pestaña nueva", async () => {
    vi.mocked(descargarAdjuntoPedido).mockResolvedValue(
      new Blob(["%PDF-1.7"], { type: "application/pdf" }),
    );
    vi.spyOn(window, "open").mockReturnValue(null);
    const user = userEvent.setup();

    render(<DocumentacionAdjuntaPedido pedidoId="pedido-1" adjuntos={[disponibles[0]]} />);
    await user.click(screen.getByRole("button", { name: "Abrir adjunto CV: cv.pdf" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("No se pudo abrir CV: cv.pdf");
  });

  it("muestra un error accesible y permite reintentar la visualización", async () => {
    const contenido = new Blob(["%PDF-1.7"], { type: "application/pdf" });
    vi.mocked(descargarAdjuntoPedido)
      .mockRejectedValueOnce(new Error("archivo no disponible"))
      .mockResolvedValueOnce(contenido);
    const user = userEvent.setup();

    render(<DocumentacionAdjuntaPedido pedidoId="pedido-1" adjuntos={[disponibles[0]]} />);
    const boton = screen.getByRole("button", { name: "Abrir adjunto CV: cv.pdf" });
    await user.click(boton);

    expect(await screen.findByRole("alert")).toHaveTextContent("No se pudo abrir CV: cv.pdf");
    expect(boton).toBeEnabled();

    await user.click(boton);
    expect(descargarAdjuntoPedido).toHaveBeenCalledTimes(2);
  });
});
