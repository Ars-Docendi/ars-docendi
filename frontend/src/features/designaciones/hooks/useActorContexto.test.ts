import { describe, expect, it, vi } from "vitest";
import { renderHook } from "@testing-library/react";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { pestaniaInicial } from "../components/tableroRevisionModelo";
import { useActorContexto } from "./useActorContexto";

vi.mock("../../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

function actorCon(roleCode: string, role: string) {
  vi.mocked(useCurrentUser).mockReturnValue({
    user: { role, roleCode, name: "Ana" },
  } as unknown as ReturnType<typeof useCurrentUser>);
  return renderHook(() => useActorContexto()).result.current;
}

describe("useActorContexto", () => {
  // El nombre del rol en la base no es la etiqueta del circuito: la Revisión abría en "Todos".
  it.each([
    ["secretaria", "Secretaría Académica", "finalizados"],
    ["coordinador_carrera", "Coordinador de Carrera", "en-coordinacion"],
    ["decanato", "Decanato", "en-decanato"],
  ])("con el rol %s la Revisión abre en su pestaña", (codigo, nombre, pestania) => {
    expect(pestaniaInicial(actorCon(codigo, nombre))).toBe(pestania);
  });
});
