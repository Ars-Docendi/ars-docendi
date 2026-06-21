import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PedidoForm } from "./PedidoForm";
import type { DatosEditablesPedido } from "../types";

function renderForm(onGuardar = vi.fn()) {
  render(<PedidoForm pedidosExistentes={[]} onGuardar={onGuardar} onCancelar={vi.fn()} />);
  return { onGuardar, user: userEvent.setup() };
}

async function completarDatosComunes(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText("Ej. 30111222"), "30111222");
  await user.type(screen.getByPlaceholderText("Ej. Ana Pérez"), "Ana Pérez");
  await user.type(
    screen.getByPlaceholderText("Ej. Ingeniería de Software"),
    "Ingeniería de Software",
  );
}

describe("PedidoForm", () => {
  describe("secciones condicionales por novedad", () => {
    it("oculta solicitud y documentación en 'Sin novedad'", () => {
      renderForm();
      expect(screen.queryByText("Solicitud")).not.toBeInTheDocument();
      expect(screen.queryByText("Documentación obligatoria")).not.toBeInTheDocument();
    });

    it("muestra solicitud + documentación (CV/DNI) al elegir 'Alta'", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      expect(screen.getByText("Solicitud")).toBeInTheDocument();
      expect(screen.getByText("Documentación obligatoria")).toBeInTheDocument();
      expect(screen.getByText("CV")).toBeInTheDocument();
      expect(screen.getByText("Foto DNI (frente)")).toBeInTheDocument();
    });

    it("muestra justificación al elegir 'Cambio de cargo o dedicación'", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Cambio de cargo o dedicación"));
      expect(screen.getByText("Solicitud")).toBeInTheDocument();
      expect(screen.getByText("Justificación")).toBeInTheDocument();
    });

    it("para 'Baja' pide justificativo, no solicitud", async () => {
      const { user } = renderForm();
      await user.click(screen.getByLabelText("Baja"));
      expect(screen.queryByText("Solicitud")).not.toBeInTheDocument();
      expect(screen.getByText("Documentación obligatoria")).toBeInTheDocument();
      expect(screen.getByText("Justificativo")).toBeInTheDocument();
    });
  });

  describe("validación", () => {
    it("bloquea el submit de un 'Alta' sin adjuntos y muestra el error", async () => {
      const { user, onGuardar } = renderForm();
      await user.click(screen.getByLabelText("Alta"));
      await completarDatosComunes(user);

      await user.click(screen.getByRole("button", { name: "Guardar borrador" }));

      expect(onGuardar).not.toHaveBeenCalled();
      expect(screen.getByText("Faltan adjuntos")).toBeInTheDocument();
    });

    it("permite guardar un 'Sin novedad' válido", async () => {
      const onGuardar = vi.fn<(datos: DatosEditablesPedido) => void>();
      const { user } = renderForm(onGuardar);
      await completarDatosComunes(user);

      await user.click(screen.getByRole("button", { name: "Guardar borrador" }));

      expect(onGuardar).toHaveBeenCalledTimes(1);
      expect(onGuardar.mock.calls[0][0].docente.dni).toBe("30111222");
    });
  });
});
