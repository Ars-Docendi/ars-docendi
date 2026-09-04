import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { LanzadorAsistente } from "./components/LanzadorAsistente";
import * as api from "./api/asistenteApi";
import { CAPACIDADES, montar } from "./test/soporte";

// ============================================================
// El lanzador de la barra y el modal que abre. Los tests de que aparece o no
// según el permiso siguen en `asistente.test.tsx`; acá va el modal en sí.
// ============================================================

beforeEach(() => {
  vi.spyOn(api, "obtenerCapacidades").mockResolvedValue(CAPACIDADES);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("El modal del asistente", () => {
  it("se titula «Asistente», visible y como nombre del diálogo", async () => {
    // Sin título, el Modal de la librería pinta un encabezado con sólo la «×» y
    // el nombre accesible sale de un `aria-label` que nadie ve. Con título hay un
    // encabezado que dice qué es esto, y el diálogo se nombra por él.
    const user = userEvent.setup();
    montar(<LanzadorAsistente />);

    await user.click(await screen.findByRole("button", { name: "Preguntar" }));

    const dialogo = await screen.findByRole("dialog", { name: "Asistente" });

    expect(within(dialogo).getByRole("heading", { name: "Asistente" })).toBeVisible();
    expect(
      within(dialogo).getByRole("region", { name: "Asistente conversacional" }),
    ).toBeInTheDocument();
  });
});
