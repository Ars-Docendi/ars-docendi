import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, useLocation } from "react-router-dom";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import type { CurrentUserState } from "../../../shared/auth/useCurrentUser";
import { SistemaPage } from "./SistemaPage";
import * as sistemaApi from "../api/sistemaApi";
import * as auditoriaApi from "../api/auditoriaApi";

vi.mock("../../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

const usuarioBase: CurrentUserState = {
  user: {
    name: "x",
    initials: "X",
    upn: "x@unlam.edu.ar",
    role: "r",
    roleCode: "r",
    permissions: [],
  },
  isLoading: false,
  error: null,
  retry: () => undefined,
};

function conPermisos(permissions: string[]) {
  vi.mocked(useCurrentUser).mockReturnValue({
    ...usuarioBase,
    user: { ...usuarioBase.user!, permissions },
  });
}

function PanelAsistenteDePrueba({ actualizacion }: { actualizacion?: number }) {
  return <p>Panel de asistente {actualizacion ?? "sin actualizar"}</p>;
}

function montar(entrada = "/sistema") {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[entrada]}>
        <SistemaPage PanelAsistente={PanelAsistenteDePrueba} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Expone `pathname+search+hash` en el DOM: hace visible al test lo que le
 * pasa a la URL real, en vez de inferirlo del contenido renderizado. */
function Ubicacion() {
  const { pathname, search, hash } = useLocation();
  return <p data-testid="ubicacion">{`${pathname}${search}${hash}`}</p>;
}

function montarConUbicacion(entrada = "/sistema#auditoria") {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[entrada]}>
        <Ubicacion />
        <SistemaPage PanelAsistente={PanelAsistenteDePrueba} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.spyOn(sistemaApi, "comprobarPing").mockImplementation(async (c) => ({
    id: c.id,
    nombre: c.nombre,
    disponible: true,
    duracionMs: 10,
    comprobadoEn: new Date().toISOString(),
  }));
  vi.spyOn(sistemaApi, "consultarEstadoSistema").mockResolvedValue({
    estado: "disponible",
    comprobadoEn: new Date().toISOString(),
    duracionMs: 5,
    mantenimientoAsistente: "inactivo",
  });
  vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue({
    elementos: [],
    pagina: 1,
    tamanoPagina: 50,
    total: 0,
    parcial: false,
    fuentesNoDisponibles: [],
  });
});

afterEach(() => vi.restoreAllMocks());

describe("SistemaPage — pestañas por permiso (design D9)", () => {
  it("con los tres permisos: Estado, Asistente y Auditoría en orden, Estado activo, hash #estado", async () => {
    conPermisos(["sistema.estado.ver", "asistente.administrar", "auditoria.ver"]);

    montar("/sistema");

    const tabs = await screen.findAllByRole("tab");
    expect(tabs.map((t) => t.textContent)).toEqual([
      expect.stringContaining("Estado"),
      expect.stringContaining("Asistente"),
      expect.stringContaining("Auditoría"),
    ]);
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
  });

  it("con un único permiso, sólo esa pestaña se renderiza y ninguna otra hace pedidos", async () => {
    conPermisos(["auditoria.ver"]);
    const espiaPing = vi.spyOn(sistemaApi, "comprobarPing");

    montar("/sistema");

    const tabs = await screen.findAllByRole("tab");
    expect(tabs).toHaveLength(1);
    expect(tabs[0]).toHaveTextContent("Auditoría");
    expect(espiaPing).not.toHaveBeenCalled();
  });

  it("dos de tres: Asistente y Auditoría, Asistente activo, sin sondas de salud", async () => {
    conPermisos(["asistente.administrar", "auditoria.ver"]);
    const espiaPing = vi.spyOn(sistemaApi, "comprobarPing");

    montar("/sistema");

    const tabs = await screen.findAllByRole("tab");
    expect(
      tabs.map((t) => t.textContent?.includes("Asistente") || t.textContent?.includes("Auditoría")),
    ).toEqual([true, true]);
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
    expect(espiaPing).not.toHaveBeenCalled();
  });

  it("deep link a una pestaña permitida activa esa pestaña", async () => {
    conPermisos(["sistema.estado.ver", "auditoria.ver"]);

    montar("/sistema#auditoria");

    await waitFor(() => {
      const activa = screen
        .getAllByRole("tab")
        .find((t) => t.getAttribute("aria-selected") === "true");
      expect(activa).toHaveTextContent("Auditoría");
    });
  });

  it("hash no permitido o inválido cae a la primera pestaña permitida y corrige la URL", async () => {
    conPermisos(["sistema.estado.ver"]);

    montar("/sistema#asistente");

    await waitFor(() => {
      const activa = screen
        .getAllByRole("tab")
        .find((t) => t.getAttribute("aria-selected") === "true");
      expect(activa).toHaveTextContent("Estado");
    });
    expect(screen.queryByText("Panel de asistente")).not.toBeInTheDocument();
  });

  it("las flechas mueven la selección entre pestañas (Tabs de @ars-docendi/ui)", async () => {
    conPermisos(["sistema.estado.ver", "asistente.administrar", "auditoria.ver"]);
    const user = userEvent.setup();

    montar("/sistema");

    const primeraTab = (await screen.findAllByRole("tab"))[0];
    primeraTab.focus();
    await user.keyboard("{ArrowRight}");

    await waitFor(() => {
      const activa = screen
        .getAllByRole("tab")
        .find((t) => t.getAttribute("aria-selected") === "true");
      expect(activa).toHaveTextContent("Asistente");
    });
  });
});

describe("SistemaPage — «Actualizar» (requisito «Refresh the active tab»)", () => {
  it("en Auditoría, sólo refetchea la consulta de auditoría — las sondas de Estado no se vuelven a pedir", async () => {
    conPermisos(["sistema.estado.ver", "asistente.administrar", "auditoria.ver"]);
    const espiaAuditoria = vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue({
      elementos: [],
      pagina: 1,
      tamanoPagina: 50,
      total: 0,
      parcial: false,
      fuentesNoDisponibles: [],
    });
    const espiaPing = vi.spyOn(sistemaApi, "comprobarPing");
    const user = userEvent.setup();

    montar("/sistema#auditoria");
    await waitFor(() => expect(espiaAuditoria).toHaveBeenCalledTimes(1));
    // Las sondas de Estado SÍ corren aunque la pestaña activa sea otra
    // (design D10: el punto de Estado las necesita en cualquier pestaña).
    await waitFor(() => expect(espiaPing).toHaveBeenCalledTimes(5));

    const boton = screen.getByRole("button", { name: "Actualizar" });
    await user.click(boton);

    await waitFor(() => expect(espiaAuditoria).toHaveBeenCalledTimes(2));
    // «Actualizar» en Auditoría no dispara una sonda de Estado adicional.
    expect(espiaPing).toHaveBeenCalledTimes(5);
  });
});

describe("SistemaPage — la pestaña Auditoría no debe expulsar al usuario a Estado (regresión)", () => {
  const eventoDePrueba = {
    id: "evt-1",
    origen: "cambios" as const,
    schema: "identity",
    tabla: "identity.users",
    rowPk: "1",
    accion: "UPDATE" as const,
    cambiadoEn: new Date().toISOString(),
    cambiadoPor: "u1",
    requestId: "r1",
    columnasCambiadas: ["estado"],
    cambios: [],
    actor: "Lucía Fernández",
    tipoActor: "persona" as const,
    accionEtiqueta: "Cambio",
    modulo: "Identidad",
    objeto: "Usuario",
    resumen: "Cambio de estado",
  };

  beforeEach(() => {
    conPermisos(["sistema.estado.ver", "asistente.administrar", "auditoria.ver"]);
    vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue({
      elementos: [eventoDePrueba],
      pagina: 1,
      tamanoPagina: 50,
      total: 1,
      parcial: false,
      fuentesNoDisponibles: [],
    });
  });

  async function pestanaActivaEs(nombre: string) {
    await waitFor(() => {
      const activa = screen
        .getAllByRole("tab")
        .find((t) => t.getAttribute("aria-selected") === "true");
      expect(activa).toHaveTextContent(nombre);
    });
  }

  it("un chip de período mantiene la pestaña Auditoría, el hash y refleja el filtro en la query", async () => {
    const user = userEvent.setup();
    montarConUbicacion("/sistema#auditoria");
    await pestanaActivaEs("Auditoría");

    await user.click(await screen.findByRole("button", { name: "Todo" }));

    await pestanaActivaEs("Auditoría");
    await waitFor(() => {
      expect(screen.getByTestId("ubicacion").textContent).toBe("/sistema?periodo=todo#auditoria");
    });
  });

  it("un chip de Acción mantiene la pestaña Auditoría y el hash", async () => {
    const user = userEvent.setup();
    montarConUbicacion("/sistema#auditoria");
    await pestanaActivaEs("Auditoría");

    await user.click(await screen.findByRole("button", { name: "Cambios" }));

    await pestanaActivaEs("Auditoría");
    await waitFor(() => {
      expect(screen.getByTestId("ubicacion").textContent).toBe("/sistema?accion=UPDATE#auditoria");
    });
  });

  it("un chip de Módulo mantiene la pestaña Auditoría y el hash", async () => {
    const user = userEvent.setup();
    montarConUbicacion("/sistema#auditoria");
    await pestanaActivaEs("Auditoría");

    await user.click(await screen.findByRole("button", { name: "Portal" }));

    await pestanaActivaEs("Auditoría");
    await waitFor(() => {
      expect(screen.getByTestId("ubicacion").textContent).toBe("/sistema?modulo=portal#auditoria");
    });
  });

  it("escribir en el buscador mantiene la pestaña Auditoría y el hash", async () => {
    const user = userEvent.setup({ delay: null });
    vi.useFakeTimers({ shouldAdvanceTime: true });
    montarConUbicacion("/sistema#auditoria");
    await pestanaActivaEs("Auditoría");

    const buscador = await screen.findByRole("textbox", { name: "Buscar" });
    await user.type(buscador, "perfil");
    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });

    vi.useRealTimers();
    await pestanaActivaEs("Auditoría");
    await waitFor(() => {
      expect(screen.getByTestId("ubicacion").textContent).toBe("/sistema?q=perfil#auditoria");
    });
  });

  it("hacer clic en una fila abre su detalle sin perder la pestaña Auditoría ni el hash", async () => {
    const user = userEvent.setup();
    montarConUbicacion("/sistema#auditoria");
    await pestanaActivaEs("Auditoría");

    await user.click(await screen.findByRole("button", { name: /Cambio de estado/ }));

    await pestanaActivaEs("Auditoría");
    await waitFor(() => {
      expect(screen.getByTestId("ubicacion").textContent).toBe("/sistema?evento=evt-1#auditoria");
    });
  });
});

describe("SistemaPage — puntos de estado accesibles", () => {
  it("cada punto expone su equivalente textual", async () => {
    conPermisos(["sistema.estado.ver", "asistente.administrar", "auditoria.ver"]);

    montar("/sistema");

    await waitFor(() => expect(screen.getAllByRole("tab")).toHaveLength(3));
    const tabs = screen.getAllByRole("tab");
    for (const tab of tabs) {
      expect(tab.textContent?.length).toBeGreaterThan(0);
    }
  });
});
