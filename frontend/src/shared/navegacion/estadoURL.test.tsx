import { act, renderHook } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, Outlet } from "react-router-dom";
import { describe, expect, it } from "vitest";

import { useFiltrosURL, useOrdenURL, usePestanaURL } from "./estadoURL";

const POR_DEFECTO = { texto: "", estados: [] as string[] };

function montar<T>(hook: () => T, inicial = "/lista") {
  const router = createMemoryRouter([{ path: "*", element: <Outlet /> }], {
    initialEntries: [inicial],
  });
  return { router, ...renderHookEnRouter(router, hook) };
}

function renderHookEnRouter<T>(router: ReturnType<typeof createMemoryRouter>, hook: () => T) {
  const resultado: { actual: T } = { actual: undefined as T };
  const Sonda = () => {
    resultado.actual = hook();
    return null;
  };
  router.routes[0].element = <Sonda />;
  return {
    resultado,
    ...renderHook(() => null, { wrapper: () => <RouterProvider router={router} /> }),
  };
}

describe("estado en la URL", () => {
  it("omite los valores por defecto y reemplaza la entrada del historial", () => {
    const { router, resultado } = montar(() => useFiltrosURL(POR_DEFECTO));
    expect(resultado.actual[0]).toEqual(POR_DEFECTO);

    act(() => resultado.actual[1]({ texto: "ana", estados: ["a", "b"] }));
    expect(router.state.location.search).toBe("?texto=ana&estados=a%2Cb");
    expect(resultado.actual[0]).toEqual({ texto: "ana", estados: ["a", "b"] });
    expect(router.state.historyAction).toBe("REPLACE");

    act(() => resultado.actual[1]({ texto: "", estados: [] }));
    expect(router.state.location.search).toBe("");
  });

  it("lee el estado de la URL al cargar (recargar lo restaura)", () => {
    const { resultado } = montar(() => useFiltrosURL(POR_DEFECTO), "/lista?texto=luz&estados=x");
    expect(resultado.actual[0]).toEqual({ texto: "luz", estados: ["x"] });
  });

  it("una opción con coma sobrevive a la URL y un % inválido se lee tal cual", () => {
    const { router, resultado } = montar(() => useFiltrosURL(POR_DEFECTO));
    act(() => resultado.actual[1]({ texto: "", estados: ["Pérez, Ana", "b"] }));
    const recargado = montar(
      () => useFiltrosURL(POR_DEFECTO),
      `/lista${router.state.location.search}`,
    );
    expect(recargado.resultado.actual[0].estados).toEqual(["Pérez, Ana", "b"]);

    const invalido = montar(() => useFiltrosURL(POR_DEFECTO), "/lista?estados=100%25");
    expect(invalido.resultado.actual[0].estados).toEqual(["100%"]);
  });

  it("dos tablas con prefijos distintos no se pisan", () => {
    const { router, resultado } = montar(() => ({
      mias: useFiltrosURL(POR_DEFECTO, "mias"),
      todas: useFiltrosURL(POR_DEFECTO, "todas"),
    }));
    act(() => resultado.actual.mias[1]({ texto: "a", estados: [] }));
    act(() => resultado.actual.todas[1]({ texto: "b", estados: [] }));
    expect(router.state.location.search).toBe("?mias.texto=a&todas.texto=b");
    expect(resultado.actual.mias[0].texto).toBe("a");
    expect(resultado.actual.todas[0].texto).toBe("b");
  });

  it("el orden ignora columnas desconocidas", () => {
    const { resultado } = montar(() => useOrdenURL(["a", "b"] as const), "/lista?orden=zzz:desc");
    expect(resultado.actual[0]).toBeNull();
  });

  it("una pestaña agrega historial salvo que se pida reemplazar", () => {
    const { router, resultado } = montar(() => usePestanaURL(["uno", "dos"] as const, "uno"));
    act(() => resultado.actual[1]("dos"));
    expect(router.state.location.search).toBe("?pestania=dos");
    expect(router.state.historyAction).toBe("PUSH");
    act(() => resultado.actual[1]("uno"));
    expect(router.state.location.search).toBe("");
  });
});
