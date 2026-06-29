import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PedidoForm } from "./PedidoForm";
import type { DatosEditablesPedido } from "../types";

function renderForm(onGuardar = vi.fn()) {
  render(<PedidoForm pedidosExistentes={[]} onGuardar={onGuardar} onCancelar={vi.fn()} />);
  return { onGuardar, user: userEvent.setup() };
}

/** Completa los campos no-adjunto de un Alta (docente nuevo + designación). */
async function completarAlta(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText("Ej. 30111222"), "30111222");
  await user.type(screen.getByPlaceholderText("Ej. Pérez, Ana"), "Pérez, Ana");
  await user.selectOptions(screen.getByLabelText("Materia asociada"), "Ingeniería de Software");
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

    it("al elegir 'Baja' muestra selector de docente + justificativo, sin solicitud", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      expect(screen.getByLabelText("Docente")).toBeInTheDocument();
      expect(screen.queryByText("Designación solicitada")).not.toBeInTheDocument();
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
