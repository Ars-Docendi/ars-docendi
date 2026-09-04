import { describe, it, expect, beforeEach, vi } from "vitest";
import { fireEvent, render as testingRender, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { IndexPage } from "./IndexPage";
import { obtenerPerfilInstitucional, perfilDe } from "../mock/mockStore";

vi.mock("../api/portalApi", async () => {
  const real = await vi.importActual<typeof import("../api/portalApi")>("../api/portalApi");
  return {
    ...real,
    obtenerPerfil: async () => {
      const institucional = obtenerPerfilInstitucional(sesion.upn);
      if (!institucional) throw new Error("no encontrado");
      return perfilDe(institucional);
    },
  };
});

// admin.aulas es el único usuario de la sesión mock sin perfil cargado: es el
// que permite recorrer el estado vacío.
const sesion = vi.hoisted(() => ({ upn: "admin.aulas@unlam.edu.ar" }));

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      name: "P. Gómez",
      initials: "PG",
      upn: sesion.upn,
      role: "Docente",
      roles: ["Docente"],
    },
  }),
}));

function render(ui: React.ReactElement) {
  return testingRender(ui, {
    wrapper: ({ children }) => (
      <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
    ),
  });
}

/** La sección cuyo encabezado es `titulo`, para acotar las consultas. */
function seccion(titulo: string): HTMLElement {
  const encabezado = screen.getByRole("heading", { name: titulo });
  const contenedor = encabezado.closest("section");
  if (!contenedor) throw new Error(`La sección "${titulo}" no tiene contenedor`);
  return contenedor;
}

beforeEach(() => {
  sesion.upn = "admin.aulas@unlam.edu.ar";
});

describe("estados de la pantalla", () => {
  it("muestra el estado de carga antes de tener el perfil", () => {
    render(<IndexPage />);
    expect(screen.getByText("Cargando tu perfil…")).toBeInTheDocument();
  });

  it("muestra el perfil una vez cargado", async () => {
    render(<IndexPage />);
    expect(await screen.findByRole("heading", { name: "Perfil" })).toBeInTheDocument();
  });

  it("muestra un error accionable si el docente no está en el padrón", async () => {
    sesion.upn = "desconocido@unlam.edu.ar";
    render(<IndexPage />);
    expect(await screen.findByText("No pudimos cargar tu perfil")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Perfil" })).not.toBeInTheDocument();
  });
});

describe("bloque Perfil de solo lectura", () => {
  it("viene precargado aunque el docente no haya cargado nada", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    const bloque = within(seccion("Perfil"));
    expect(bloque.getByText("Gómez, Paula")).toBeInTheDocument();
    expect(bloque.getByText("admin.aulas@unlam.edu.ar")).toBeInTheDocument();
    expect(bloque.getByText("0058")).toBeInTheDocument();
  });

  it("no ofrece ningún control de edición, a diferencia del resto", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    // Ninguna fila del Perfil abre nada: esa ausencia es lo que comunica que
    // no se edita acá.
    expect(within(seccion("Perfil")).queryByRole("button")).not.toBeInTheDocument();
    expect(within(seccion("Contacto")).getAllByRole("button")).toHaveLength(2);
  });
});

describe("secciones vacías", () => {
  it("se presentan compactas con su control de alta", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    for (const titulo of ["Experiencia", "Educación", "Certificaciones", "Proyectos"]) {
      expect(
        within(seccion(titulo)).getByRole("button", { name: "+ Agregar" }),
      ).toBeInTheDocument();
    }
  });

  it("no muestra avisos de lo que falta cargar ni barra de progreso", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    expect(screen.queryByText(/todavía no cargaste/i)).not.toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });

  it("se expande al cargar el primer ítem", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(within(seccion("Educación")).getByRole("button", { name: "+ Agregar" }));
    await usuario.type(screen.getByLabelText(/Carrera o título/), "Ingeniería en Informática");
    await usuario.type(screen.getByLabelText(/Institución/), "UNLaM");
    await usuario.type(screen.getByLabelText("Año de desde"), "2002");
    await usuario.click(screen.getByRole("button", { name: "Guardar" }));

    expect(within(seccion("Educación")).getByText(/Ingeniería en Informática/)).toBeInTheDocument();
  });

  it("no da de alta un ítem al que le faltan los campos identificatorios", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(within(seccion("Educación")).getByRole("button", { name: "+ Agregar" }));
    await usuario.click(screen.getByRole("button", { name: "Guardar" }));

    expect(screen.getByText("Ingresá la carrera o el título.")).toBeInTheDocument();
  });
});

describe("edición por sección", () => {
  it("no existe un guardado global de la pantalla", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    expect(screen.queryByRole("button", { name: /guardar (todo|perfil|cambios)/i })).toBeNull();
  });

  it("guardar un campo no exige ni altera a los demás", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(screen.getByRole("button", { name: "Editar teléfono" }));
    await usuario.type(screen.getByRole("textbox", { name: "Teléfono" }), "11-2233-4455");
    await usuario.click(screen.getByRole("button", { name: "Guardar" }));

    expect(within(seccion("Contacto")).getByText("11-2233-4455")).toBeInTheDocument();
    expect(await screen.findByText("Cambios guardados")).toBeInTheDocument();
    // Las secciones que no se tocaron siguen vacías, sin bloquear el guardado.
    expect(
      within(seccion("Proyectos")).getByRole("button", { name: "+ Agregar" }),
    ).toBeInTheDocument();
  });

  it("descarta la edición al cancelar", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(screen.getByRole("button", { name: "Editar teléfono" }));
    await usuario.type(screen.getByRole("textbox", { name: "Teléfono" }), "11-9999-0000");
    await usuario.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.queryByText("11-9999-0000")).not.toBeInTheDocument();
  });
});

describe("validación del mail de contacto", () => {
  it("bloquea el guardado y marca el campo si el formato es inválido", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(screen.getByRole("button", { name: "Editar mail" }));
    await usuario.type(screen.getByRole("textbox", { name: "Mail" }), "marina@");
    await usuario.click(screen.getByRole("button", { name: "Guardar" }));

    expect(screen.getByText("Revisá el mail: no tiene un formato válido.")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Mail" })).toBeInTheDocument();
  });

  it("acepta un mail bien formado", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(screen.getByRole("button", { name: "Editar mail" }));
    await usuario.type(screen.getByRole("textbox", { name: "Mail" }), "marina.diaz@gmail.com");
    await usuario.click(screen.getByRole("button", { name: "Guardar" }));

    expect(within(seccion("Contacto")).getByText("marina.diaz@gmail.com")).toBeInTheDocument();
  });
});

describe("CV", () => {
  function inputDeArchivo(): HTMLInputElement {
    const input = seccion("CV").querySelector('input[type="file"]');
    if (!input) throw new Error("La sección CV no tiene input de archivo");
    return input as HTMLInputElement;
  }

  it("solo ofrece PDF en el selector de archivos", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    expect(inputDeArchivo()).toHaveAttribute("accept", "application/pdf,.pdf");
  });

  it("rechaza un archivo que no es PDF", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    // `userEvent.upload` respeta el accept del input, así que no alcanzaría el
    // guard: se dispara el change directo, que es lo que hace un drag & drop.
    fireEvent.change(inputDeArchivo(), {
      target: { files: [new File(["x"], "cv.docx", { type: "application/msword" })] },
    });

    expect(screen.getByText("El CV tiene que ser un archivo PDF.")).toBeInTheDocument();
    expect(screen.queryByText("cv.docx")).not.toBeInTheDocument();
  });

  it("carga un PDF y lo reemplaza sin dejar historial", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.upload(
      inputDeArchivo(),
      new File(["x"], "cv-2026.pdf", { type: "application/pdf" }),
    );
    expect(within(seccion("CV")).getByText("cv-2026.pdf")).toBeInTheDocument();

    await usuario.upload(
      inputDeArchivo(),
      new File(["y"], "cv-nuevo.pdf", { type: "application/pdf" }),
    );
    expect(within(seccion("CV")).getByText("cv-nuevo.pdf")).toBeInTheDocument();
    expect(screen.queryByText("cv-2026.pdf")).not.toBeInTheDocument();
  });
});

describe("perfil con datos cargados", () => {
  beforeEach(() => {
    sesion.upn = "marina.diaz@unlam.edu.ar";
  });

  it("muestra las investigaciones dentro de Proyectos, sin una sección aparte", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    expect(
      within(seccion("Proyectos")).getByText(/Detección de anomalías en tráfico SCADA/),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /Producción/ })).not.toBeInTheDocument();
  });

  it("muestra un período vigente como actual", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    expect(within(seccion("Proyectos")).getByText(/2022 – actual/)).toBeInTheDocument();
  });

  it("elimina un ítem previa confirmación", async () => {
    const usuario = userEvent.setup();
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });

    await usuario.click(
      within(seccion("Certificaciones")).getByRole("button", {
        name: /Acciones de AWS Certified Solutions Architect/,
      }),
    );
    await usuario.click(screen.getByRole("menuitem", { name: "Eliminar" }));
    expect(screen.getByText("Esta acción no se puede deshacer.")).toBeInTheDocument();
    await usuario.click(screen.getByRole("button", { name: "Eliminar" }));

    expect(screen.queryByText("AWS Certified Solutions Architect")).not.toBeInTheDocument();
  });

  it("habilidades e intereses son listas separadas", async () => {
    render(<IndexPage />);
    await screen.findByRole("heading", { name: "Perfil" });
    expect(within(seccion("Habilidades")).getByText("Bases de datos")).toBeInTheDocument();
    expect(within(seccion("Intereses")).getByText("Machine learning")).toBeInTheDocument();
    expect(within(seccion("Habilidades")).queryByText("Machine learning")).not.toBeInTheDocument();
  });
});
