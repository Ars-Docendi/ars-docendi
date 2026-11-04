import { describe, expect, it } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

import { useFiltrosAuditoria } from "./useFiltrosAuditoria";

function montar(entrada = "/sistema") {
  return renderHook(() => useFiltrosAuditoria(), {
    wrapper: ({ children }) => <MemoryRouter initialEntries={[entrada]}>{children}</MemoryRouter>,
  });
}

describe("useFiltrosAuditoria", () => {
  it("valores por default cuando no hay query string", () => {
    const { result } = montar();

    expect(result.current.filtros).toEqual({
      q: "",
      periodo: "7d",
      accion: "",
      modulo: "",
      desde: "",
      hasta: "",
      tabla: "",
      clave: "",
      pagina: 1,
      evento: null,
    });
    expect(result.current.difiereDelDefault).toBe(false);
    expect(result.current.cantidadFiltrosAvanzados).toBe(0);
  });

  it("round trip: escribir un filtro se refleja al leerlo de nuevo", () => {
    const { result } = montar();

    act(() =>
      result.current.actualizarFiltros({
        periodo: "30d",
        accion: "UPDATE",
        modulo: "portal",
        q: "perfil",
      }),
    );

    expect(result.current.filtros).toMatchObject({
      periodo: "30d",
      accion: "UPDATE",
      modulo: "portal",
      q: "perfil",
      pagina: 1,
    });
    expect(result.current.difiereDelDefault).toBe(true);
  });

  it("una URL compartida con filtros no-default restaura los mismos filtros", () => {
    const { result } = montar(
      "/sistema?periodo=30d&accion=UPDATE&modulo=portal&q=perfil#auditoria",
    );

    expect(result.current.filtros).toMatchObject({
      periodo: "30d",
      accion: "UPDATE",
      modulo: "portal",
      q: "perfil",
    });
  });

  it("un rango custom (Desde/Hasta) cuenta para el badge de «Más filtros» y activa «Limpiar filtros»", () => {
    const { result } = montar();

    act(() => result.current.actualizarFiltros({ desde: "2026-09-01T00:00" }));

    expect(result.current.cantidadFiltrosAvanzados).toBe(1);
    expect(result.current.difiereDelDefault).toBe(true);
  });

  it("cambiar un filtro vuelve a la página 1", () => {
    const { result } = montar();
    act(() => result.current.irAPagina(3));
    expect(result.current.filtros.pagina).toBe(3);

    act(() => result.current.actualizarFiltros({ accion: "DELETE" }));
    expect(result.current.filtros.pagina).toBe(1);
  });

  it("limpiar filtros remueve todos los parámetros, incluido el evento abierto", () => {
    const { result } = montar();
    act(() => result.current.actualizarFiltros({ q: "algo", modulo: "portal" }));
    act(() => result.current.abrirEvento("cambios-9"));
    expect(result.current.filtros.evento).toBe("cambios-9");

    act(() => result.current.limpiarFiltros());

    expect(result.current.filtros).toEqual({
      q: "",
      periodo: "7d",
      accion: "",
      modulo: "",
      desde: "",
      hasta: "",
      tabla: "",
      clave: "",
      pagina: 1,
      evento: null,
    });
    expect(result.current.difiereDelDefault).toBe(false);
  });

  it("abrirEvento no reinicia la página ni otros filtros", () => {
    const { result } = montar();
    act(() => result.current.irAPagina(2));
    act(() => result.current.abrirEvento("cambios-1"));

    expect(result.current.filtros.pagina).toBe(2);
    expect(result.current.filtros.evento).toBe("cambios-1");
  });
});
