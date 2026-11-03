import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Fragment, StrictMode } from "react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { limpiarToasts } from "../../../shared/ui/toasts";
import { Toaster } from "../../../shared/ui/Toaster";
import { notificacionesKeys } from "../hooks/useNotificaciones";
import { notificacion, pagina } from "../pruebas";
import type { Notificacion } from "../types";
import { PanelNotificaciones } from "./PanelNotificaciones";

const api = vi.hoisted(() => ({
  contarNoLeidas: vi.fn(),
  listarPagina: vi.fn(),
  marcarLeida: vi.fn(),
  marcarLeidas: vi.fn(),
}));
const push = vi.hoisted(() => ({ estado: "sin-soporte" as string }));
vi.mock("../push/useDispositivoPush", () => ({
  useDispositivoPush: () => ({
    estado: push.estado,
    trabajando: false,
    error: null,
    activar: vi.fn(),
    desactivar: vi.fn(),
  }),
}));
vi.mock("../api/pushApi", () => ({
  obtenerPreferenciasPush: vi.fn().mockResolvedValue({ tipos: [] }),
  guardarPreferenciasPush: vi.fn(),
}));
vi.mock("../api/notificacionesApi", async (importarOriginal) => ({
  ...(await importarOriginal<typeof import("../api/notificacionesApi")>()),
  ...api,
}));

const HACE_DOS_MESES = new Date(Date.now() - 60 * 24 * 3600 * 1000).toISOString();

function crearCliente() {
  return new QueryClient({ defaultOptions: { queries: { retryDelay: 0 } } });
}

function renderizar({
  rol = "docente",
  cliente = crearCliente(),
  onCerrar = vi.fn(),
  onNavegar = vi.fn(),
  estricto = false,
}: {
  estricto?: boolean;
  rol?: string;
  cliente?: QueryClient;
  onCerrar?: () => void;
  onNavegar?: (n: Notificacion) => void;
} = {}) {
  const disparador = document.createElement("button");
  disparador.textContent = "Campana";
  document.body.appendChild(disparador);
  const Envoltorio = estricto ? StrictMode : Fragment;
  const resultado = render(
    <Envoltorio>
      <MemoryRouter>
        <QueryClientProvider client={cliente}>
          <PanelNotificaciones
            abierto
            onCerrar={onCerrar}
            triggerRef={{ current: disparador }}
            desplazamiento={200}
            rol={rol}
            onNavegar={onNavegar}
          />
          <Toaster />
        </QueryClientProvider>
      </MemoryRouter>
    </Envoltorio>,
  );
  return { ...resultado, cliente, onCerrar, onNavegar, disparador };
}

/** Lists by tab; anything not set resolves to an empty page. */
function listas(porEstado: { NO_LEIDAS?: Notificacion[]; LEIDAS?: Notificacion[] }) {
  api.listarPagina.mockImplementation(async (estado: "NO_LEIDAS" | "LEIDAS") =>
    pagina(porEstado[estado] ?? []),
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  limpiarToasts();
  push.estado = "sin-soporte";
  localStorage.clear();
  api.contarNoLeidas.mockResolvedValue(0);
  api.marcarLeida.mockResolvedValue({});
  api.marcarLeidas.mockResolvedValue(1);
});

afterEach(() => {
  vi.unstubAllGlobals();
  document.body.innerHTML = "";
});

describe("PanelNotificaciones — pestañas y carga", () => {
  it("opens on 'No leídas' and fetches only that tab; 'Leídas' is fetched when selected", async () => {
    listas({ NO_LEIDAS: [notificacion()], LEIDAS: [notificacion({ leidaEn: HACE_DOS_MESES })] });
    const usuario = userEvent.setup();
    renderizar();

    const dialogo = await screen.findByRole("dialog", { name: "Notificaciones" });
    expect(
      within(dialogo).getByRole("tab", { name: "No leídas", selected: true }),
    ).toBeInTheDocument();
    await waitFor(() => expect(api.listarPagina).toHaveBeenCalledWith("NO_LEIDAS", 1));
    expect(api.listarPagina).not.toHaveBeenCalledWith("LEIDAS", expect.anything());

    await usuario.click(within(dialogo).getByRole("tab", { name: "Leídas" }));
    await waitFor(() => expect(api.listarPagina).toHaveBeenCalledWith("LEIDAS", 1));
    expect(
      within(dialogo).getByRole("tab", { name: "Leídas", selected: true }),
    ).toBeInTheDocument();
  });

  it("moves between tabs with arrows, Home and End (roving tabindex)", async () => {
    listas({});
    const usuario = userEvent.setup();
    renderizar();
    const noLeidas = await screen.findByRole("tab", { name: "No leídas" });
    const leidas = screen.getByRole("tab", { name: "Leídas" });
    expect(noLeidas).toHaveAttribute("tabindex", "0");
    expect(leidas).toHaveAttribute("tabindex", "-1");

    noLeidas.focus();
    await usuario.keyboard("{ArrowRight}");
    expect(leidas).toHaveAttribute("aria-selected", "true");
    expect(leidas).toHaveFocus();
    await usuario.keyboard("{Home}");
    expect(noLeidas).toHaveAttribute("aria-selected", "true");
    await usuario.keyboard("{End}");
    expect(leidas).toHaveAttribute("aria-selected", "true");
    await usuario.keyboard("{ArrowRight}");
    expect(noLeidas).toHaveFocus();
  });

  it("groups by date under 'Hoy' and 'Anteriores' and links each item to its entity", async () => {
    listas({
      NO_LEIDAS: [
        notificacion({
          titulo: "De hoy",
          tipoEntidad: "SOLICITUD_AULA",
          tipoEvento: "AULA_ASIGNADA",
        }),
        notificacion({
          titulo: "Vieja",
          creadaEn: HACE_DOS_MESES,
          tipoEntidad: "PEDIDO_DESIGNACION",
          tipoEvento: "PEDIDO_DEVUELTO",
        }),
      ],
    });
    renderizar();

    const hoy = await screen.findByRole("region", { name: "Hoy" });
    expect(within(hoy).getByText("De hoy")).toBeInTheDocument();
    expect(within(hoy).getByText("Ir a la solicitud →")).toBeInTheDocument();
    const anteriores = screen.getByRole("region", { name: "Anteriores" });
    expect(within(anteriores).getByText("Vieja")).toBeInTheDocument();
    expect(within(anteriores).getByText("Ir al pedido →")).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Esta semana" })).not.toBeInTheDocument();
  });
});

describe("PanelNotificaciones — estados", () => {
  it("shows a skeleton while the first page loads", async () => {
    api.listarPagina.mockReturnValue(new Promise(() => {}));
    renderizar();
    expect(
      await screen.findByRole("status", { name: "Cargando notificaciones" }),
    ).toBeInTheDocument();
  });

  it("shows the empty state of each tab", async () => {
    listas({});
    const usuario = userEvent.setup();
    renderizar();
    expect(await screen.findByText("No tenés notificaciones sin leer")).toBeInTheDocument();
    expect(screen.getByText("Las nuevas notificaciones van a aparecer acá.")).toBeInTheDocument();

    await usuario.click(screen.getByRole("tab", { name: "Leídas" }));
    expect(await screen.findByText("Todavía no tenés notificaciones leídas")).toBeInTheDocument();
    expect(
      screen.getByText("Cuando abras una notificación, la vas a encontrar acá."),
    ).toBeInTheDocument();
  });

  it("shows the error state and 'Reintentar' fetches again", async () => {
    api.listarPagina.mockRejectedValue(new Error("red"));
    const usuario = userEvent.setup();
    renderizar();

    expect(await screen.findByText("No pudimos cargar las notificaciones")).toBeInTheDocument();
    expect(screen.getByText("Revisá tu conexión e intentá nuevamente.")).toBeInTheDocument();

    api.listarPagina.mockResolvedValue(pagina([notificacion({ titulo: "Ya llegó" })]));
    await usuario.click(screen.getByRole("button", { name: "Reintentar" }));
    expect(await screen.findByText("Ya llegó")).toBeInTheDocument();
  });

  it("keeps the data and shows 'No pudimos actualizar la lista.' when a refetch fails", async () => {
    listas({ NO_LEIDAS: [notificacion({ titulo: "Sigue acá" })] });
    const { cliente } = renderizar();
    expect(await screen.findByText("Sigue acá")).toBeInTheDocument();

    api.listarPagina.mockRejectedValue(new Error("red"));
    await act(async () => {
      await cliente.refetchQueries({ queryKey: notificacionesKeys.listas("docente") });
    });

    expect(await screen.findByText("No pudimos actualizar la lista.")).toBeInTheDocument();
    expect(screen.getByText("Sigue acá")).toBeInTheDocument();
    api.listarPagina.mockResolvedValue(pagina([notificacion({ titulo: "Sigue acá" })]));
    await userEvent.setup().click(screen.getByRole("button", { name: "Reintentar" }));
    await waitFor(() =>
      expect(screen.queryByText("No pudimos actualizar la lista.")).not.toBeInTheDocument(),
    );
  });
});

describe("PanelNotificaciones — scroll infinito", () => {
  let disparar: (visible: boolean) => void;
  beforeEach(() => {
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        constructor(alCambiar: IntersectionObserverCallback) {
          disparar = (visible) =>
            alCambiar(
              [{ isIntersecting: visible } as IntersectionObserverEntry],
              this as unknown as IntersectionObserver,
            );
        }
        observe() {}
        disconnect() {}
        unobserve() {}
        takeRecords = () => [];
      },
    );
  });

  it("loads the next page when the end of the list is reached and appends it", async () => {
    api.listarPagina.mockImplementation(async (_estado: string, numero: number) =>
      numero === 1
        ? pagina([notificacion({ titulo: "Primera" })], { pagina: 1, totalPaginas: 2, total: 2 })
        : pagina([notificacion({ titulo: "Segunda" })], { pagina: 2, totalPaginas: 2, total: 2 }),
    );
    renderizar();
    expect(await screen.findByText("Primera")).toBeInTheDocument();

    act(() => disparar(true));

    expect(await screen.findByText("Segunda")).toBeInTheDocument();
    expect(api.listarPagina).toHaveBeenCalledWith("NO_LEIDAS", 2);
    expect(screen.getByText("Primera")).toBeInTheDocument();
  });

  it("shows 'No pudimos cargar más notificaciones.' with a retry when the next page fails", async () => {
    api.listarPagina.mockImplementation(async (_estado: string, numero: number) => {
      if (numero === 1)
        return pagina([notificacion({ titulo: "Primera" })], {
          pagina: 1,
          totalPaginas: 2,
          total: 2,
        });
      throw new Error("red");
    });
    const usuario = userEvent.setup();
    renderizar();
    await screen.findByText("Primera");

    act(() => disparar(true));
    expect(await screen.findByText("No pudimos cargar más notificaciones.")).toBeInTheDocument();
    expect(screen.getByText("Primera")).toBeInTheDocument();

    api.listarPagina.mockImplementation(async () =>
      pagina([notificacion({ titulo: "Segunda" })], { pagina: 2, totalPaginas: 2, total: 2 }),
    );
    await usuario.click(screen.getByRole("button", { name: "Reintentar" }));
    expect(await screen.findByText("Segunda")).toBeInTheDocument();
  });
});

describe("PanelNotificaciones — abrir y marcar como leída", () => {
  it("opening an unread notification marks it, navigates and updates the badge optimistically", async () => {
    const n = notificacion({ titulo: "Para abrir" });
    // A server that remembers: once marked, the notification is no longer unread.
    let servidor = [n, notificacion({ titulo: "Otra" })];
    api.listarPagina.mockImplementation(async (estado: string) =>
      pagina(estado === "NO_LEIDAS" ? servidor : []),
    );
    api.marcarLeida.mockImplementation(async (id: string) => {
      servidor = servidor.filter((x) => x.id !== id);
    });
    api.contarNoLeidas.mockResolvedValue(2);
    const usuario = userEvent.setup();
    const { onNavegar, cliente } = renderizar();
    await screen.findByText("Para abrir");
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(2),
    );

    api.contarNoLeidas.mockResolvedValue(1);
    await usuario.click(screen.getByTestId(`notificacion-${n.id}`));

    expect(onNavegar).toHaveBeenCalledWith(n);
    expect(api.marcarLeida).toHaveBeenCalledWith(n.id);
    expect(screen.queryByText("Para abrir")).not.toBeInTheDocument(); // gone from the unread list at once
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(1),
    );
  });

  it("rolls back and shows a toast when marking as read fails, and still navigates", async () => {
    const n = notificacion({
      titulo: "Falla",
      tipoEntidad: "SOLICITUD_AULA",
      tipoEvento: "AULA_ASIGNADA",
    });
    listas({ NO_LEIDAS: [n] });
    api.contarNoLeidas.mockResolvedValue(1);
    api.marcarLeida.mockRejectedValue(new Error("500"));
    const usuario = userEvent.setup();
    const { onNavegar, cliente } = renderizar();
    await screen.findByText("Falla");
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(1),
    );

    await usuario.click(screen.getByTestId(`notificacion-${n.id}`));

    expect(onNavegar).toHaveBeenCalledWith(n);
    const aviso = await screen.findByRole("alert");
    expect(aviso).toHaveTextContent("No pudimos marcar la notificación como leída");
    expect(aviso).toHaveTextContent("La solicitud se abrió igualmente. Intentá nuevamente.");
    expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(1);
    expect(await screen.findByText("Falla")).toBeInTheDocument(); // back in the unread list
  });

  it("opening an already read notification does not call the API", async () => {
    const leida = notificacion({ titulo: "Ya leída", leidaEn: HACE_DOS_MESES });
    listas({ LEIDAS: [leida] });
    const usuario = userEvent.setup();
    const { onNavegar } = renderizar();
    await usuario.click(await screen.findByRole("tab", { name: "Leídas" }));
    await usuario.click(await screen.findByTestId(`notificacion-${leida.id}`));

    expect(onNavegar).toHaveBeenCalledWith(leida);
    expect(api.marcarLeida).not.toHaveBeenCalled();
  });
});

describe("PanelNotificaciones — selección", () => {
  it("selects some, shows the count and marks exactly those", async () => {
    const [a, b, c] = [
      notificacion({ titulo: "A" }),
      notificacion({ titulo: "B" }),
      notificacion({ titulo: "C" }),
    ];
    let servidor = [a, b, c];
    api.listarPagina.mockImplementation(async (estado: string) =>
      pagina(estado === "NO_LEIDAS" ? servidor : []),
    );
    api.marcarLeidas.mockImplementation(async ({ ids }: { ids: string[] }) => {
      servidor = servidor.filter((x) => !ids.includes(x.id));
      return ids.length;
    });
    api.contarNoLeidas.mockResolvedValue(3);
    const usuario = userEvent.setup();
    const { cliente } = renderizar();
    await screen.findByText("A");

    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas" }));
    expect(screen.getAllByRole("checkbox")).toHaveLength(3);
    const confirmar = screen.getAllByRole("button", { name: "Marcar como leídas" }).at(-1)!;
    expect(confirmar).toBeDisabled();

    await usuario.click(screen.getByTestId(`notificacion-${a.id}`));
    await usuario.click(screen.getByTestId(`notificacion-${c.id}`));
    expect(screen.getByTestId(`notificacion-${a.id}`)).toHaveAttribute("aria-checked", "true");
    expect(screen.getByRole("button", { name: "Marcar como leídas (2)" })).toBeEnabled();

    api.contarNoLeidas.mockResolvedValue(1);
    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas (2)" }));

    expect(api.marcarLeidas).toHaveBeenCalledWith({ ids: [a.id, c.id] });
    expect(screen.queryByText("A")).not.toBeInTheDocument();
    expect(screen.queryByText("C")).not.toBeInTheDocument();
    expect(screen.getByText("B")).toBeInTheDocument();
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument(); // the mode ended
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(1),
    ); // refetched
  });

  it("'Seleccionar todas' sends {todas, hasta} with the newest loaded instant, and toggles back", async () => {
    const vieja = notificacion({ titulo: "Vieja", creadaEn: "2026-11-03T10:00:00.000Z" });
    const nueva = notificacion({ titulo: "Nueva", creadaEn: "2026-11-04T10:00:00.000Z" });
    listas({ NO_LEIDAS: [nueva, vieja] });
    api.contarNoLeidas.mockResolvedValue(40);
    api.marcarLeidas.mockResolvedValue(40);
    const usuario = userEvent.setup();
    const { cliente } = renderizar();
    await screen.findByText("Nueva");
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(40),
    );

    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas" }));
    await usuario.click(screen.getByRole("button", { name: "Seleccionar todas" }));
    expect(screen.getByRole("button", { name: "Marcar como leídas (40)" })).toBeEnabled();
    await usuario.click(screen.getByRole("button", { name: "Deseleccionar todas" }));
    expect(screen.getByRole("button", { name: "Seleccionar todas" })).toBeInTheDocument();
    await usuario.click(screen.getByRole("button", { name: "Seleccionar todas" }));

    api.contarNoLeidas.mockResolvedValue(0);
    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas (40)" }));

    expect(api.marcarLeidas).toHaveBeenCalledWith({
      todas: true,
      hasta: "2026-11-04T10:00:00.000Z",
    });
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(0),
    );
  });

  it("cancels the selection without calling the API", async () => {
    listas({ NO_LEIDAS: [notificacion({ titulo: "A" })] });
    const usuario = userEvent.setup();
    renderizar();
    await screen.findByText("A");
    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas" }));
    await usuario.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    expect(api.marcarLeidas).not.toHaveBeenCalled();
  });

  it("is only offered on the unread tab and only with unread items", async () => {
    listas({ LEIDAS: [notificacion({ leidaEn: HACE_DOS_MESES })] });
    const usuario = userEvent.setup();
    renderizar();
    expect(await screen.findByText("No tenés notificaciones sin leer")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Marcar como leídas" })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole("tab", { name: "Leídas" }));
    await screen.findByTestId(/notificacion-/);
    expect(screen.queryByRole("button", { name: "Marcar como leídas" })).not.toBeInTheDocument();
  });

  it("restores the list and explains it when the bulk request fails", async () => {
    const [a, b] = [notificacion({ titulo: "A" }), notificacion({ titulo: "B" })];
    listas({ NO_LEIDAS: [a, b] });
    api.contarNoLeidas.mockResolvedValue(2);
    api.marcarLeidas.mockRejectedValue(new Error("500"));
    const usuario = userEvent.setup();
    const { cliente } = renderizar();
    await screen.findByText("A");
    await waitFor(() =>
      expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(2),
    );

    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas" }));
    await usuario.click(screen.getByTestId(`notificacion-${a.id}`));
    await usuario.click(screen.getByRole("button", { name: "Marcar como leídas (1)" }));

    const aviso = await screen.findByRole("alert");
    expect(aviso).toHaveTextContent("No pudimos marcar las notificaciones como leídas");
    expect(aviso).toHaveTextContent("Intentá nuevamente.");
    expect(await screen.findByText("A")).toBeInTheDocument();
    expect(cliente.getQueryData(notificacionesKeys.cantidad("docente"))).toBe(2);
  });
});

describe("PanelNotificaciones — cierre y foco", () => {
  it("Esc and an outside press close it; the close button too", async () => {
    listas({});
    const usuario = userEvent.setup();
    const { onCerrar } = renderizar();
    await screen.findByRole("dialog", { name: "Notificaciones" });

    await usuario.keyboard("{Escape}");
    expect(onCerrar).toHaveBeenCalledTimes(1);

    await usuario.click(document.body);
    expect(onCerrar).toHaveBeenCalledTimes(2);

    await usuario.click(screen.getByRole("button", { name: "Cerrar notificaciones" }));
    expect(onCerrar).toHaveBeenCalledTimes(3);
  });

  it("a press on the bell does not count as outside (the bell toggles by itself)", async () => {
    listas({});
    const usuario = userEvent.setup();
    const { onCerrar, disparador } = renderizar();
    await screen.findByRole("dialog", { name: "Notificaciones" });
    await usuario.click(disparador);
    expect(onCerrar).not.toHaveBeenCalled();
  });

  it("keeps the focus on the close button under StrictMode's double effect run", async () => {
    listas({});
    renderizar({ estricto: true });
    const cerrar = await screen.findByRole("button", { name: "Cerrar notificaciones" });
    await new Promise((resolver) => window.setTimeout(resolver, 20));
    expect(cerrar).toHaveFocus();
  });

  it("focuses the close button on open and returns focus to the bell on close", async () => {
    listas({});
    const { unmount, disparador } = renderizar();
    expect(await screen.findByRole("button", { name: "Cerrar notificaciones" })).toHaveFocus();
    unmount();
    await waitFor(() => expect(disparador).toHaveFocus());
  });
});

describe("PanelNotificaciones — preferencias de push", () => {
  it("the gear opens the preferences view inside the panel and 'Volver' returns to the list", async () => {
    listas({});
    push.estado = "activo";
    const usuario = userEvent.setup();
    renderizar();

    await usuario.click(
      await screen.findByRole("button", { name: "Preferencias de notificaciones" }),
    );
    const dialogo = screen.getByRole("dialog", { name: "Preferencias de notificaciones" });
    expect(
      within(dialogo).getByRole("switch", { name: "Notificaciones en este dispositivo" }),
    ).toBeChecked();
    expect(within(dialogo).queryByRole("tab")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Volver a las notificaciones" })).toHaveFocus();

    await usuario.click(screen.getByRole("button", { name: "Volver a las notificaciones" }));
    expect(await screen.findByRole("dialog", { name: "Notificaciones" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "No leídas", selected: true })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Preferencias de notificaciones" })).toHaveFocus();
  });

  it("offers one dismissible hint while push is off, and it stays dismissed", async () => {
    listas({});
    push.estado = "desactivado";
    const usuario = userEvent.setup();
    const primera = renderizar();

    expect(await screen.findByText(/Recibí los avisos en este dispositivo/)).toBeInTheDocument();
    await usuario.click(screen.getByRole("button", { name: "Descartar sugerencia" }));
    expect(screen.queryByText(/Recibí los avisos en este dispositivo/)).not.toBeInTheDocument();

    primera.unmount();
    renderizar();
    await screen.findByRole("dialog", { name: "Notificaciones" });
    expect(screen.queryByText(/Recibí los avisos en este dispositivo/)).not.toBeInTheDocument();
  });

  it("the hint's 'Activar' opens the preferences view", async () => {
    listas({});
    push.estado = "desactivado";
    const usuario = userEvent.setup();
    renderizar();
    await usuario.click(await screen.findByRole("button", { name: "Activar" }));
    expect(
      screen.getByRole("dialog", { name: "Preferencias de notificaciones" }),
    ).toBeInTheDocument();
  });

  it.each(["activo", "bloqueado", "sin-soporte", "ios-sin-instalar", "servidor-apagado"])(
    "shows no hint when the device is '%s'",
    async (estado) => {
      listas({});
      push.estado = estado;
      renderizar();
      await screen.findByRole("dialog", { name: "Notificaciones" });
      expect(screen.queryByText(/Recibí los avisos en este dispositivo/)).not.toBeInTheDocument();
    },
  );
});
