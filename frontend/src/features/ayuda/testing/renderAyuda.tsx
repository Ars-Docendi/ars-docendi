import { render, screen, within } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";

import { routes } from "../routes";

/** Renderiza las rutas reales de ayuda dentro de un router en memoria. */
export function renderAyuda(ruta: string) {
  const router = createMemoryRouter(
    [
      {
        path: "/",
        children: [
          { index: true, element: <p>Raíz de prueba</p> },
          { path: "portal", element: <p>Portal de prueba</p> },
          routes,
        ],
      },
    ],
    { initialEntries: [ruta] },
  );
  // Imita el contenedor con scroll del shell (`AppLayout`).
  render(
    <main className="adoc-main">
      <RouterProvider router={router} />
    </main>,
  );
  return router;
}

/** Región con el contenido de la página (excluye la sidebar de documentación). */
export async function contenidoAyuda() {
  return within(await screen.findByRole("region", { name: "Contenido de la ayuda" }));
}
