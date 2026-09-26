import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider, useLocation, Outlet } from "react-router-dom";
import { AxiosError } from "axios";

import { routes } from "./routes";
import { LanzadorAsistente } from "./components/LanzadorAsistente";
import * as api from "./api/asistenteApi";
import { CAPACIDADES } from "./test/soporte";

// ============================================================
// El redirect de `/asistente` (ARS-151, tasks.md §10; design.md D8 de
// asistente-rediseno-v3). La página a pantalla completa se borró: lo que queda
// es una marca en la URL de la home, que `LanzadorAsistente` consume. Este
// archivo reemplaza a `RutaConAltoPropio.test.tsx`, que probaba el CSS de esa
// página.
//
// `soporte-historial` y `administracion` no cambian con este task y siguen
// cubiertos por sus propios archivos (`SoporteHistorialPage.test.tsx`,
// `AdministracionAsistentePage.test.tsx`), que ya montan por el mismo `routes`
// que acá — su verde sigue siendo la prueba de que esas dos rutas resuelven.
//
// EL SHELL SE MONTA IGUAL QUE `AppLayout` DE VERDAD (`app/router.tsx`):
// `LanzadorAsistente` vive en la barra, POR ENCIMA de `/asistente` en el árbol
// de rutas, no adentro de `/portal`. Montarlo sólo en `/portal` —como hacía la
// primera versión de este archivo— hacía que `LanzadorAsistente` recién se
// montara DESPUÉS del redirect, con la marca ya puesta desde el primer render;
// eso ocultaba el bug real: en la app, el lanzador YA ESTÁ MONTADO cuando
// `/asistente` todavía no redirigió, así que su primer render ve la URL SIN la
// marca todavía.
// ============================================================

afterEach(() => {
  vi.restoreAllMocks();
});

function Shell() {
  const { pathname, search } = useLocation();
  return (
    <>
      <LanzadorAsistente />
      <p data-testid="ubicacion">{`${pathname}${search}`}</p>
      <Outlet />
    </>
  );
}

function montarConRouter(entrada: string) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const router = createMemoryRouter(
    [{ element: <Shell />, children: [{ path: "/portal", element: null }, routes] }],
    { initialEntries: [entrada] },
  );
  return render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe("Un vínculo viejo a /asistente, con acceso (tasks.md 10.1)", () => {
  it("termina en /portal con el modal abierto y la URL sin la marca", async () => {
    vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);

    montarConRouter("/asistente");

    expect(await screen.findByRole("dialog", { name: "Asistente" })).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId("ubicacion")).not.toHaveTextContent("asistente=abrir"),
    );
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/portal");
  });
});

describe("Un vínculo viejo a /asistente, sin acceso (tasks.md 10.1)", () => {
  it("no abre nada: sólo queda en /portal, también sin la marca", async () => {
    // Un 403 de verdad (`AxiosError` con `response.status`), no un `Error` genérico:
    // ese es el que `useAccesoAlAsistente` no reintenta, así que se resuelve rápido
    // y sin depender del backoff entre reintentos.
    vi.spyOn(api, "obtenerCapacidades").mockRejectedValue(
      new AxiosError("Forbidden", "403", undefined, undefined, { status: 403 } as never),
    );

    montarConRouter("/asistente");

    await waitFor(() =>
      expect(screen.getByTestId("ubicacion")).not.toHaveTextContent("asistente=abrir"),
    );
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/portal");
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.queryByRole("button", { name: "Preguntar" })).toBeNull();
  });
});
