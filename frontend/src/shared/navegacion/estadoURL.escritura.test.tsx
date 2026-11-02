import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it } from "vitest";

import { useFiltrosURL } from "./estadoURL";

const POR_DEFECTO = { texto: "" };

function Campo() {
  const [filtros, setFiltros] = useFiltrosURL(POR_DEFECTO, "t");
  return (
    <input
      aria-label="Buscar"
      value={filtros.texto}
      onChange={(e) => setFiltros({ texto: e.target.value })}
    />
  );
}

/** Un router cuya navegación tarda: lo que pasa con una tabla grande o un equipo lento. */
function montarLento(demoraMs: number) {
  const router = createMemoryRouter([{ path: "/", element: <Campo /> }], {
    initialEntries: ["/"],
  });
  const navegar = router.navigate.bind(router);
  router.navigate = ((...args: Parameters<typeof navegar>) =>
    new Promise((resolver) => setTimeout(() => resolver(navegar(...args)), demoraMs))) as never;
  render(<RouterProvider router={router} />);
  return router;
}

describe("escribir en un filtro que vive en la URL", () => {
  it("no pierde letras aunque la URL tarde en actualizarse", async () => {
    const router = montarLento(40);
    const campo = screen.getByLabelText("Buscar");

    await userEvent.type(campo, "lopez", { delay: 5 });

    expect(campo).toHaveValue("lopez");
    await waitFor(() => expect(router.state.location.search).toBe("?t.texto=lopez"));
    expect(campo).toHaveValue("lopez");
  });

  it("si la URL cambia por otro lado (Atrás, un enlace) el campo la sigue", async () => {
    const router = montarLento(1);
    const campo = screen.getByLabelText("Buscar");
    await userEvent.type(campo, "ab", { delay: 5 });
    await waitFor(() => expect(router.state.location.search).toBe("?t.texto=ab"));

    await router.navigate("/?t.texto=otro");
    await waitFor(() => expect(campo).toHaveValue("otro"));
  });
});
