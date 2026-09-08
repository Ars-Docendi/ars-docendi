import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PedidoForm } from "./PedidoForm";
import type { DatosEditablesPedido } from "../types";

const CATALOGOS = {
  personas: [{ id: "persona-alta", dni: "30111222", nombre: "Pérez, Ana" }],
  docentes: [
    {
      dni: "28341567",
      nombre: "Lucía Fernández",
      legajo: "1001",
      antiguedad: 8,
      cargoActual: "Adjunto",
      dedicacionActual: "Categoría 3",
      materiasActuales: [
        { materia: "Programación I", horas: 6 },
        { materia: "Ingeniería de Software", horas: 4 },
      ],
      horasInvestigacionActuales: 2,
      horasExternasActuales: 0,
    },
  ],
  cargos: ["Titular", "Adjunto", "JTP", "Ayudante"],
  dedicaciones: Array.from({ length: 6 }, (_, i) => `Categoría ${i + 1}`),
  tiposBaja: ["Renuncia", "Jubilación", "Otro"],
};

function renderForm(onGuardar = vi.fn()) {
  render(
    <PedidoForm
      catedra="Ingeniería de Software"
      pedidosExistentes={[]}
      onGuardar={onGuardar}
      onCancelar={vi.fn()}
      {...CATALOGOS}
    />,
  );
  return { onGuardar, user: userEvent.setup() };
}

/** Panel de datos actuales (resumen de cambios en Cambio) — acotado porque los
 * nombres de cargo/dedicación/materia también aparecen como `<option>` en los
 * `Select` de la sección "Designación solicitada". */
function panelDatosActuales(): HTMLElement {
  const panel = document.querySelector(".adoc-pf-datos");
  if (!panel) throw new Error("No se encontró el panel de datos actuales.");
  return panel as HTMLElement;
}

/** Completa los campos no-adjunto de un Alta. La materia no se completa: es la
 * cátedra del actor y se muestra de solo lectura. */
async function completarAlta(user: ReturnType<typeof userEvent.setup>) {
  await user.selectOptions(screen.getByLabelText("Persona"), "persona-alta");
  await user.clear(screen.getByLabelText("Horas"));
  await user.type(screen.getByLabelText("Horas"), "4");
  await user.selectOptions(screen.getByLabelText("Cargo solicitado"), "Ayudante");
  await user.selectOptions(screen.getByLabelText("Dedicación solicitada"), "Categoría 5");
}

async function adjuntar(user: ReturnType<typeof userEvent.setup>, titulo: string, nombre: string) {
  const input = screen.getByText(titulo).parentElement?.querySelector("input[type=file]");
  if (!(input instanceof HTMLInputElement)) throw new Error(`No se encontró el adjunto ${titulo}`);
  await user.upload(input, new File(["contenido"], nombre, { type: "application/pdf" }));
}

describe("PedidoForm", () => {
  describe("secciones condicionales por novedad", () => {
    it("inicia sin novedad seleccionada y no ofrece Sin novedad", () => {
      renderForm();
      expect(screen.queryByLabelText("Sin novedad")).not.toBeInTheDocument();
      expect(screen.getByText("Seleccioná una novedad para continuar.")).toBeInTheDocument();
      expect(screen.queryByLabelText("Docente")).not.toBeInTheDocument();
      expect(screen.queryByText("Designación solicitada")).not.toBeInTheDocument();
      expect(screen.queryByText(/Documentación obligatoria/)).not.toBeInTheDocument();
      expect(screen.queryByText("Justificación")).not.toBeInTheDocument();
    });

    it("al elegir 'Alta' muestra datos nuevos + designación + documentación (CV/DNI)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      expect(screen.getByText("Datos del docente · Nuevo")).toBeInTheDocument();
      expect(screen.getByLabelText("Persona")).toBeInTheDocument();
      expect(screen.getByText("Designación solicitada")).toBeInTheDocument();
      expect(screen.getByText("Documentación obligatoria · Alta")).toBeInTheDocument();
      expect(screen.getByText("CV (PDF)")).toBeInTheDocument();
      expect(screen.getByText("DNI · Frente")).toBeInTheDocument();
    });

    it("al elegir 'Baja' muestra selector de docente + tipo de baja + justificativo, sin solicitud", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      expect(screen.getByLabelText("Docente")).toBeInTheDocument();
      expect(screen.queryByText("Designación solicitada")).not.toBeInTheDocument();
      expect(screen.getByLabelText("Tipo de baja")).toBeInTheDocument();
      expect(screen.getByText("Documentación obligatoria · Baja")).toBeInTheDocument();
      expect(screen.getByText("Documento justificativo de la baja")).toBeInTheDocument();
    });

    it("al elegir 'Cambio' muestra designación solicitada + justificación", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      expect(screen.getByText("Designación solicitada")).toBeInTheDocument();
      expect(screen.getByText("Justificación")).toBeInTheDocument();
      expect(screen.getByLabelText("Motivo del pedido")).toBeInTheDocument();
    });

    it("muestra los datos actuales (solo lectura) al seleccionar un docente existente", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");
      expect(screen.getByText("Cargo actual")).toBeInTheDocument();
      expect(screen.getByText("Adjunto")).toBeInTheDocument();
      expect(screen.getByText("Categoría 3")).toBeInTheDocument();
    });
  });

  describe("materia y horas", () => {
    // Un pedido cubre exactamente una materia: la cátedra del actor. Por eso la
    // materia no se elige ni se agrega/quita — sólo la carga horaria es editable.
    it("en Alta muestra la materia de la cátedra sin ofrecer elegirla ni agregar otras", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));

      const seccionMateria = document.querySelector(".adoc-pf-materias") as HTMLElement;
      expect(within(seccionMateria).getByText("Ingeniería de Software")).toBeInTheDocument();

      // Sin Select de materia, sin agregar, sin quitar.
      expect(within(seccionMateria).queryByRole("combobox")).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Agregar materia" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: /Quitar materia/ })).not.toBeInTheDocument();

      // Las horas sí son editables.
      expect(screen.getByLabelText("Horas")).toBeInTheDocument();
    });

    it("en Cambio precarga las horas vigentes del docente en esa cátedra", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández tiene 4h en "Ingeniería de Software" (y 6h en otra materia,
      // que no participa de este pedido).
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      expect(screen.getByLabelText("Horas")).toHaveValue(4);
    });

    it("carga horas de investigación y externas en Alta", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));

      await user.clear(screen.getByLabelText("Horas de investigación"));
      await user.type(screen.getByLabelText("Horas de investigación"), "4");
      await user.clear(screen.getByLabelText("Horas externas (otro depto.)"));
      await user.type(screen.getByLabelText("Horas externas (otro depto.)"), "2");

      expect(screen.getByLabelText("Horas de investigación")).toHaveValue(4);
      expect(screen.getByLabelText("Horas externas (otro depto.)")).toHaveValue(2);
    });
  });

  describe("cargo y dedicación libres (D-6/D-7)", () => {
    it("en Cambio, el cargo solicitado admite cualquier valor del catálogo (D-6)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández tiene cargo actual "Adjunto".
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      const selectCargo = screen.getByLabelText("Cargo solicitado") as HTMLSelectElement;
      // "Ayudante" es inferior a "Adjunto": no hay restricción que lo impida.
      await user.selectOptions(selectCargo, "Ayudante");
      expect(selectCargo).toHaveValue("Ayudante");
    });

    it("en Cambio, el Select de dedicación ofrece las seis categorías (D-7)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández tiene dedicación actual "Categoría 3".
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      const selectDedicacion = screen.getByLabelText("Dedicación solicitada") as HTMLSelectElement;
      const opciones = Array.from(selectDedicacion.options).map((o) => o.value);
      expect(opciones).toEqual([
        "",
        "Categoría 1",
        "Categoría 2",
        "Categoría 3",
        "Categoría 4",
        "Categoría 5",
        "Categoría 6",
      ]);

      await user.selectOptions(selectDedicacion, "Categoría 1");
      expect(selectDedicacion).toHaveValue("Categoría 1");
    });

    it("Alta no restringe las opciones de dedicación (no hay dedicación actual)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));

      const selectDedicacion = screen.getByLabelText("Dedicación solicitada") as HTMLSelectElement;
      const opciones = Array.from(selectDedicacion.options).map((o) => o.value);
      expect(opciones).toEqual([
        "",
        "Categoría 1",
        "Categoría 2",
        "Categoría 3",
        "Categoría 4",
        "Categoría 5",
        "Categoría 6",
      ]);
    });
  });

  describe("resumen de cambios en el panel de datos actuales (D-8)", () => {
    it("muestra la transición de cargo y dedicación", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      await user.selectOptions(screen.getByLabelText("Cargo solicitado"), "Titular");
      await user.selectOptions(screen.getByLabelText("Dedicación solicitada"), "Categoría 1");

      const panel = within(panelDatosActuales());
      expect(panel.getByText("Adjunto")).toBeInTheDocument();
      expect(panel.getByText("Titular")).toBeInTheDocument();
      expect(panel.getByText("Cat. 3")).toBeInTheDocument();
      expect(panel.getByText("Cat. 1")).toBeInTheDocument();
    });

    it("muestra la transición de horas de investigación y externas", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández: 2h investigación / 0h externas en el catálogo.
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      await user.clear(screen.getByLabelText("Horas de investigación"));
      await user.type(screen.getByLabelText("Horas de investigación"), "5");

      const panel = within(panelDatosActuales());
      expect(panel.getByText("Investigación")).toBeInTheDocument();
      expect(panel.getByText("2h")).toBeInTheDocument();
      expect(panel.getByText("5h")).toBeInTheDocument();
      expect(panel.getByText("Externas")).toBeInTheDocument();
      // Sin cambios en externas: valor plano, sin flecha de transición.
      expect(panel.getAllByText("0h")).toHaveLength(1);
    });

    it("muestra la transición de la carga horaria de la materia", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández tiene 4h en "Ingeniería de Software", la cátedra del pedido.
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      await user.clear(screen.getByLabelText("Horas"));
      await user.type(screen.getByLabelText("Horas"), "8");

      const panel = within(panelDatosActuales());
      expect(panel.getByText("Materia")).toBeInTheDocument();
      expect(panel.getByText("Ingeniería de Software")).toBeInTheDocument();
      expect(panel.getByText("4h")).toBeInTheDocument();
      expect(panel.getByText("8h")).toBeInTheDocument();
    });

    it("una carga horaria sin cambios se muestra sin transición", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      // Sin tocar las horas: valor plano, una sola vez.
      const panel = within(panelDatosActuales());
      expect(panel.getAllByText("4h")).toHaveLength(1);
    });
  });

  describe("tipificación de la baja", () => {
    it("exige seleccionar el tipo de baja antes de guardar", async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      await user.click(screen.getByRole("button", { name: "Guardar y enviar" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Seleccioná el tipo de baja.")).toBeInTheDocument();
    });

    it('"Otro" muestra el campo de detalle y lo exige para enviar', async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");
      await user.selectOptions(screen.getByLabelText("Tipo de baja"), "Otro");

      expect(screen.getByLabelText("Detalle")).toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Guardar y enviar" }));
      expect(onGuardar).not.toHaveBeenCalled();
      expect(
        screen.getByText('Describí el motivo cuando el tipo de baja es "Otro".'),
      ).toBeInTheDocument();
    });
  });

  describe("validación", () => {
    it("'Guardar pedido' adelanta la validación autoritativa del backend", async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      await completarAlta(user);

      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Faltan adjuntos")).toBeInTheDocument();
    });

    it("no permite guardar sin seleccionar una novedad", async () => {
      const onGuardar = vi.fn<(datos: DatosEditablesPedido) => void>();
      const { user } = renderForm(onGuardar);
      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Seleccioná una novedad admitida.")).toBeInTheDocument();
    });
  });

  describe("Guardar y enviar / Guardar y reenviar", () => {
    it("edita un devuelto con solicitud editable y snapshot histórico separado", async () => {
      const onGuardar = vi.fn();
      const user = userEvent.setup();
      render(
        <PedidoForm
          catedra="Ingeniería de Software"
          pedidoInicial={{
            id: "p-devuelto",
            numero: "N°-2026-0001",
            periodoId: "1",
            catedra: "Ingeniería de Software",
            carrera: "Ingeniería en Informática",
            docente: { dni: "28341567", nombre: "Lucía Fernández", antiguedad: 8, legajo: "1001" },
            horas: 12,
            horasActuales: 8,
            horasInvestigacion: 4,
            horasInvestigacionActuales: 2,
            horasExternas: 3,
            horasExternasActuales: 1,
            cargoActual: "Adjunto",
            dedicacionActual: "Categoría 3",
            cargoSolicitado: "Titular",
            dedicacionSolicitada: "Categoría 1",
            justificacion: "Actualización de la carga",
            novedad: "Cambio de cargo o dedicación",
            snapshot: {
              cargo: "Adjunto",
              dedicacion: "Categoría 3",
              horas: 8,
              horasInvestigacion: 2,
              horasExternas: 1,
              materia: "Ingeniería de Software",
            },
            adjuntos: [],
            estado: "devuelto",
            propietarioActual: "Jefe de Cátedra",
            etapaRetorno: "en_revision_coordinador",
            prioritario: false,
            historial: [],
          }}
          pedidosExistentes={[]}
          esEdicion
          onGuardar={onGuardar}
          onCancelar={vi.fn()}
          {...CATALOGOS}
        />,
      );

      expect(screen.getByLabelText("Horas")).toHaveValue(12);
      expect(screen.getByLabelText("Horas de investigación")).toHaveValue(4);
      expect(screen.getByLabelText("Horas externas (otro depto.)")).toHaveValue(3);
      const panel = within(panelDatosActuales());
      expect(panel.getByText("8h")).toBeInTheDocument();
      expect(panel.getByText("2h")).toBeInTheDocument();
      expect(panel.getByText("1h")).toBeInTheDocument();

      await user.clear(screen.getByLabelText("Horas"));
      await user.type(screen.getByLabelText("Horas"), "16");
      await user.clear(screen.getByLabelText("Horas de investigación"));
      await user.type(screen.getByLabelText("Horas de investigación"), "5");
      await user.clear(screen.getByLabelText("Horas externas (otro depto.)"));
      await user.type(screen.getByLabelText("Horas externas (otro depto.)"), "2");
      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));

      expect(onGuardar).toHaveBeenCalledWith(
        expect.objectContaining({ horas: 16, horasInvestigacion: 5, horasExternas: 2 }),
        undefined,
      );
      expect(onGuardar.mock.calls[0][0]).not.toHaveProperty("snapshot");
    });

    it("un pedido nuevo (o en borrador) muestra 'Guardar y enviar'", () => {
      renderForm();
      expect(screen.getByRole("button", { name: "Guardar y enviar" })).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Guardar y reenviar" })).not.toBeInTheDocument();
    });

    it("un pedido devuelto muestra 'Guardar y reenviar' en vez de 'Guardar y enviar'", () => {
      const onGuardar = vi.fn();
      render(
        <PedidoForm
          catedra="Ingeniería de Software"
          pedidoInicial={{
            id: "p1",
            numero: "N°-2026-0001",
            periodoId: "1",
            catedra: "Ingeniería de Software",
            carrera: "Ingeniería en Informática",
            docente: { dni: "28341567", nombre: "Lucía Fernández", antiguedad: 8 },
            horas: 6,
            cargoActual: "Adjunto",
            dedicacionActual: "Categoría 3",
            novedad: "Sin novedad",
            horasExternas: 0,
            horasInvestigacion: 0,
            adjuntos: [],
            estado: "devuelto",
            propietarioActual: "Jefe de Cátedra",
            etapaRetorno: "en_revision_coordinador",
            prioritario: false,
            historial: [],
          }}
          pedidosExistentes={[]}
          esEdicion
          onGuardar={onGuardar}
          onCancelar={vi.fn()}
          {...CATALOGOS}
        />,
      );
      expect(screen.getByRole("button", { name: "Guardar y reenviar" })).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Guardar y enviar" })).not.toBeInTheDocument();
    });

    it("bloquea 'Guardar y enviar' si faltan campos obligatorios", async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      await completarAlta(user);

      await user.click(screen.getByRole("button", { name: "Guardar y enviar" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Faltan adjuntos")).toBeInTheDocument();
    });

    it("con datos válidos, 'Guardar y enviar' llama a onGuardar con { enviar: true }", async () => {
      const onGuardar = vi.fn();
      const { user } = renderForm(onGuardar);
      await user.click(screen.getByLabelText("Alta"));
      await completarAlta(user);
      await adjuntar(user, "CV (PDF)", "cv.pdf");
      await adjuntar(user, "DNI · Frente", "frente.pdf");
      await adjuntar(user, "DNI · Dorso", "dorso.pdf");

      await user.click(screen.getByRole("button", { name: "Guardar y enviar" }));

      expect(onGuardar).toHaveBeenCalledTimes(1);
      expect(onGuardar.mock.calls[0][1]).toEqual({ enviar: true });
    });
  });
});
