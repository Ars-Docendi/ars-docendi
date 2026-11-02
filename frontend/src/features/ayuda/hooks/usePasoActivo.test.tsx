import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { Link, MemoryRouter } from "react-router-dom";

import { observadores, prepararEntornoDom } from "../testing/entornoDom";
import { usePasoActivo } from "./usePasoActivo";

const IDS = ["paso-1", "paso-2", "paso-3"];

function Prueba({ ids = IDS }: { ids?: string[] }) {
  const [activo, setActivo] = useState<string>();
  usePasoActivo(ids, setActivo);
  return (
    <>
      <output aria-label="activo">{activo ?? "ninguno"}</output>
      <Link to={{ hash: "#paso-3" }}>ir al paso 3</Link>
      {ids.map((id) => (
        <div key={id} id={id} data-testid={id} />
      ))}
    </>
  );
}

function renderizar(entrada = "/ayuda/m/a") {
  return render(
    <MemoryRouter initialEntries={[entrada]}>
      <Prueba />
    </MemoryRouter>,
  );
}

const activo = () => screen.getByLabelText("activo").textContent;

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  prepararEntornoDom();
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("usePasoActivo", () => {
  it("arranca en el primer paso y observa todos", () => {
    renderizar();

    expect(activo()).toBe("paso-1");
    expect(observadores.at(-1)?.elementos.size).toBe(3);
  });

  it("sigue al paso que pasa a ser visible", () => {
    renderizar();

    act(() => observadores.at(-1)!.interseccion(screen.getByTestId("paso-2")));

    expect(activo()).toBe("paso-2");
  });

  it("un deep link fija el paso y pausa el scroll-spy hasta que termina el desplazamiento", () => {
    renderizar("/ayuda/m/a#paso-3");
    expect(activo()).toBe("paso-3");

    act(() => observadores.at(-1)!.interseccion(screen.getByTestId("paso-1")));
    expect(activo()).toBe("paso-3");

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    act(() => observadores.at(-1)!.interseccion(screen.getByTestId("paso-1")));
    expect(activo()).toBe("paso-1");
  });

  it("navegar a un paso por hash lo deja activo", async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderizar();

    await user.click(screen.getByRole("link", { name: "ir al paso 3" }));

    expect(activo()).toBe("paso-3");
  });

  it("al desmontar desconecta el observador y desconecta el observador", () => {
    const { unmount } = renderizar();
    const observador = observadores.at(-1)!;

    unmount();

    expect(observador.elementos.size).toBe(0);
  });
});
