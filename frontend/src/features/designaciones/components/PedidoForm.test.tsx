import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PedidoForm } from "./PedidoForm";
import type { DatosEditablesPedido } from "../types";

function renderForm(onGuardar = vi.fn()) {
  render(<PedidoForm pedidosExistentes={[]} onGuardar={onGuardar} onCancelar={vi.fn()} />);
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

/** Completa los campos no-adjunto de un Alta (docente nuevo + designación + una materia). */
async function completarAlta(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText("Ej. 30111222"), "30111222");
  await user.type(screen.getByPlaceholderText("Ej. Pérez, Ana"), "Pérez, Ana");
  await user.selectOptions(screen.getByLabelText("Materia"), "Ingeniería de Software");
  await user.clear(screen.getByLabelText("Horas"));
  await user.type(screen.getByLabelText("Horas"), "4");
  await user.selectOptions(screen.getByLabelText("Cargo solicitado"), "Ayudante");
  await user.selectOptions(screen.getByLabelText("Dedicación solicitada"), "Categoría 5");
}

describe("PedidoForm", () => {
  describe("secciones condicionales por novedad", () => {
    it("en 'Sin novedad' muestra el selector de docente y oculta solicitud/documentación", () => {
      renderForm();
      expect(screen.getByLabelText("Docente")).toBeInTheDocument();
      expect(screen.queryByText("Designación solicitada")).not.toBeInTheDocument();
      expect(screen.queryByText(/Documentación obligatoria/)).not.toBeInTheDocument();
      expect(screen.queryByText("Justificación")).not.toBeInTheDocument();
    });

    it("al elegir 'Alta' muestra datos nuevos + designación + documentación (CV/DNI)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      expect(screen.getByText("Datos del docente · Nuevo")).toBeInTheDocument();
      expect(screen.getByPlaceholderText("Ej. 30111222")).toBeInTheDocument();
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

  describe("materias y horas", () => {
    it("en Alta arranca con una fila (sin acción de quitar) y permite agregar/quitar", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));

      expect(screen.getAllByLabelText("Materia")).toHaveLength(1);
      expect(screen.queryByRole("button", { name: /Quitar materia/ })).not.toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Agregar materia" }));
      expect(screen.getAllByLabelText("Materia")).toHaveLength(2);
      expect(screen.getAllByRole("button", { name: /Quitar materia/ })).toHaveLength(2);

      await user.click(screen.getAllByRole("button", { name: /Quitar materia/ })[0]);
      expect(screen.getAllByLabelText("Materia")).toHaveLength(1);
      // Con una sola fila no se puede quitar más (mínimo 1 obligatorio).
      expect(screen.queryByRole("button", { name: /Quitar materia/ })).not.toBeInTheDocument();
    });

    it("en Cambio precarga las materias del docente y admite agregar/quitar/cambiar", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández tiene 2 materias en el catálogo.
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");
      expect(screen.getAllByLabelText("Materia")).toHaveLength(2);

      await user.click(screen.getByRole("button", { name: "Agregar materia" }));
      expect(screen.getAllByLabelText("Materia")).toHaveLength(3);

      await user.click(screen.getAllByRole("button", { name: /Quitar materia/ })[0]);
      expect(screen.getAllByLabelText("Materia")).toHaveLength(2);
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

  describe("cargo libre, dedicación restringida a mejorar (D-6/D-7)", () => {
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

    it("en Cambio, el Select de dedicación solo ofrece opciones mejores que la actual (D-7)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández tiene dedicación actual "Categoría 3".
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      const selectDedicacion = screen.getByLabelText("Dedicación solicitada") as HTMLSelectElement;
      const opciones = Array.from(selectDedicacion.options).map((o) => o.value);
      expect(opciones).toEqual(["", "Categoría 0", "Categoría 1", "Categoría 2"]);

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
        "Categoría 0",
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

    it("compara el listado de materias por nombre (agregada/quitada/sin cambios)", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      // Lucía Fernández: Programación I (6h) + Ingeniería de Software (4h).
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      // Quita "Programación I" (primera fila) y agrega una materia nueva.
      await user.click(screen.getAllByRole("button", { name: /Quitar materia/ })[0]);
      await user.click(screen.getByRole("button", { name: "Agregar materia" }));
      const selectsMateria = screen.getAllByLabelText("Materia");
      await user.selectOptions(selectsMateria[selectsMateria.length - 1], "Bases de Datos");
      const inputsHoras = screen.getAllByLabelText("Horas");
      await user.clear(inputsHoras[inputsHoras.length - 1]);
      await user.type(inputsHoras[inputsHoras.length - 1], "3");

      const panel = within(panelDatosActuales());
      expect(panel.getByText("Materias")).toBeInTheDocument();
      expect(panel.getByText("Programación I")).toBeInTheDocument();
      // Quitada: se ve tachada, pero sigue mostrando la carga horaria que tenía.
      expect(panel.getByText("6h")).toBeInTheDocument();
      expect(panel.getByText("Bases de Datos")).toBeInTheDocument();
      expect(panel.getByText("3h")).toBeInTheDocument();
    });
  });

  describe("tipificación de la baja", () => {
    it("exige seleccionar el tipo de baja para guardar", async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Seleccioná el tipo de baja.")).toBeInTheDocument();
    });

    it('"Otro" muestra el campo de detalle y lo exige para guardar', async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");
      await user.selectOptions(screen.getByLabelText("Tipo de baja"), "Otro");

      expect(screen.getByLabelText("Detalle")).toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));
      expect(onGuardar).not.toHaveBeenCalled();
      expect(
        screen.getByText('Describí el motivo cuando el tipo de baja es "Otro".'),
      ).toBeInTheDocument();
    });
  });

  describe("validación", () => {
    it("bloquea el submit de un 'Alta' sin adjuntos y muestra el error", async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      await completarAlta(user);

      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Faltan adjuntos")).toBeInTheDocument();
    });

    it("permite guardar un 'Sin novedad' al seleccionar un docente existente", async () => {
      const onGuardar = vi.fn<(datos: DatosEditablesPedido) => void>();
      const { user } = renderForm(onGuardar);
      await user.selectOptions(screen.getByLabelText("Docente"), "28341567");

      await user.click(screen.getByRole("button", { name: "Guardar pedido" }));

      expect(onGuardar).toHaveBeenCalledTimes(1);
      expect(onGuardar.mock.calls[0][0].docente.dni).toBe("28341567");
    });
  });
});
