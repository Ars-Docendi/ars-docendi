import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useEffect, useState } from "react";
import { createMemoryRouter, Link, Outlet, RouterProvider, useNavigate } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";

import { ModalDeFormulario } from "../ui/ModalDeFormulario";
import { PageHeader } from "../ui/PageHeader";
import { GestorDeNavegacion } from "./GestorDeNavegacion";
import { reiniciarGuardias, useGuardiaDeCambios } from "./guardias";
import { SeguimientoDePagina } from "./paginaActual";
import { reiniciarPosicionesDeScroll } from "./posicionesDeScroll";
import { RestauradorDeScroll } from "./restauracionDeScroll";

function Shell() {
  return (
    <div>
      <main className="adoc-main">
        <Outlet />
      </main>
      <RestauradorDeScroll />
      <SeguimientoDePagina />
      <GestorDeNavegacion />
    </div>
  );
}

function Editor() {
  const [texto, setTexto] = useState("");
  const { permitirSalida } = useGuardiaDeCambios(texto !== "");
  const navegar = useNavigate();
  return (
    <>
      <PageHeader title="Editor" />
      <input aria-label="Texto" value={texto} onChange={(e) => setTexto(e.target.value)} />
      <Link to="/otra">Ir a otra</Link>
      <Link to="/carga">Ir a una con carga</Link>
      <Link to="/editor?x=1">Mismo editor con query</Link>
      <button
        type="button"
        onClick={() => {
          permitirSalida();
          navegar("/otra");
        }}
      >
        Guardar
      </button>
    </>
  );
}

function ConModal() {
  const [abierto, setAbierto] = useState(false);
  return (
    <>
      <PageHeader title="Con modal" />
      <button type="button" onClick={() => setAbierto(true)}>
        Abrir
      </button>
      {abierto && (
        <ModalDeFormulario titulo="Diálogo" onCerrar={() => setAbierto(false)}>
          <input aria-label="Dentro" />
        </ModalDeFormulario>
      )}
    </>
  );
}

/** Página con carga: primero un h1 provisorio y, al llegar los datos, otro h1 distinto (otro nodo). */
function ConCarga() {
  const [cargado, setCargado] = useState(false);
  useEffect(() => {
    const reloj = window.setTimeout(() => setCargado(true), 150);
    return () => window.clearTimeout(reloj);
  }, []);
  return cargado ? (
    <section>
      <PageHeader title="Detalle cargado" />
    </section>
  ) : (
    <PageHeader title="Detalle" />
  );
}

function montar(inicial: string, entradas?: string[]) {
  const router = createMemoryRouter(
    [
      {
        element: <Shell />,
        children: [
          { path: "/editor", element: <Editor /> },
          { path: "/otra", element: <PageHeader title="Otra página" /> },
          { path: "/modal", element: <ConModal /> },
          { path: "/carga", element: <ConCarga /> },
        ],
      },
    ],
    { initialEntries: entradas ?? [inicial], initialIndex: entradas ? entradas.length - 1 : 0 },
  );
  render(<RouterProvider router={router} />);
  return router;
}

afterEach(() => {
  reiniciarGuardias();
  reiniciarPosicionesDeScroll();
  document.title = "";
});

describe("cambios sin guardar", () => {
  it("pide confirmar al salir con cambios y deja la página si se sigue editando", async () => {
    const router = montar("/editor");
    await userEvent.type(screen.getByLabelText("Texto"), "algo");
    await userEvent.click(screen.getByRole("link", { name: "Ir a otra" }));

    expect(await screen.findByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/editor");

    await userEvent.click(screen.getByRole("button", { name: "Seguir editando" }));
    expect(router.state.location.pathname).toBe("/editor");
    expect(screen.getByLabelText("Texto")).toHaveValue("algo");
  });

  it("'Descartar cambios' continúa la navegación", async () => {
    const router = montar("/editor");
    await userEvent.type(screen.getByLabelText("Texto"), "algo");
    await userEvent.click(screen.getByRole("link", { name: "Ir a otra" }));
    await userEvent.click(await screen.findByRole("button", { name: "Descartar cambios" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/otra"));
  });

  it("no bloquea un cambio de query en la misma página ni la salida tras guardar", async () => {
    const router = montar("/editor");
    await userEvent.type(screen.getByLabelText("Texto"), "algo");
    await userEvent.click(screen.getByRole("link", { name: "Mismo editor con query" }));
    expect(router.state.location.search).toBe("?x=1");
    expect(screen.queryByText("Tenés cambios sin guardar.")).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Guardar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/otra"));
  });

  it("sin cambios navega sin preguntar", async () => {
    const router = montar("/editor");
    await userEvent.click(screen.getByRole("link", { name: "Ir a otra" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/otra"));
  });
});

describe("Atrás con un diálogo abierto", () => {
  it("cierra el diálogo y no sale de la página", async () => {
    const router = montar("/modal", ["/editor", "/modal"]);
    await userEvent.click(screen.getByRole("button", { name: "Abrir" }));
    expect(screen.getByRole("dialog", { name: "Diálogo" })).toBeInTheDocument();

    await act(async () => {
      await router.navigate(-1);
    });

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe("/modal");
    // Con el diálogo cerrado, el siguiente Atrás sí sale.
    await act(async () => {
      await router.navigate(-1);
    });
    await waitFor(() => expect(router.state.location.pathname).toBe("/editor"));
  });
});

describe("título, foco y anuncio", () => {
  it("al navegar fija el título, enfoca el h1 y lo anuncia", async () => {
    montar("/editor");
    await userEvent.click(screen.getByRole("link", { name: "Ir a otra" }));

    const titulo = await screen.findByRole("heading", { name: "Otra página" });
    await waitFor(() => expect(titulo).toHaveFocus());
    expect(document.title).toBe("Otra página · Ars Docendi");
    expect(screen.getByRole("status")).toHaveTextContent("Otra página");
  });

  it("si el h1 enfocado se reemplaza al terminar de cargar, el foco pasa al h1 nuevo", async () => {
    montar("/editor");
    await userEvent.click(screen.getByRole("link", { name: "Ir a una con carga" }));

    const final = await screen.findByRole("heading", { name: "Detalle cargado" });
    await waitFor(() => expect(final).toHaveFocus());
  });

  it("un cambio sólo de query no mueve el foco", async () => {
    montar("/editor");
    const campo = screen.getByLabelText("Texto");
    campo.focus();
    await userEvent.click(screen.getByRole("link", { name: "Mismo editor con query" }));
    await new Promise((r) => setTimeout(r, 120));
    expect(screen.getByRole("heading", { name: "Editor" })).not.toHaveFocus();
  });
});

describe("scroll", () => {
  function fijarScroll(valor: number) {
    const main = document.querySelector<HTMLElement>(".adoc-main")!;
    main.scrollTop = valor;
    main.dispatchEvent(new Event("scroll"));
  }

  it("restaura al volver con Atrás y arranca arriba en una página nueva", async () => {
    const router = montar("/editor");
    fijarScroll(240);

    await userEvent.click(screen.getByRole("link", { name: "Ir a otra" }));
    await screen.findByRole("heading", { name: "Otra página" });
    expect(document.querySelector<HTMLElement>(".adoc-main")!.scrollTop).toBe(0);

    await act(async () => {
      await router.navigate(-1);
    });
    await waitFor(() =>
      expect(document.querySelector<HTMLElement>(".adoc-main")!.scrollTop).toBe(240),
    );
  });

  it("un cambio de query en la misma página conserva el scroll", async () => {
    montar("/editor");
    fijarScroll(150);
    await userEvent.click(screen.getByRole("link", { name: "Mismo editor con query" }));
    expect(document.querySelector<HTMLElement>(".adoc-main")!.scrollTop).toBe(150);
  });
});
