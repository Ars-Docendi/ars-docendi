import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import { ListaAuditoria } from "./ListaAuditoria";
import type { EventoAuditoria, PaginaAuditoria } from "../api/auditoriaApi";

function evento(sobrescribir: Partial<EventoAuditoria>): EventoAuditoria {
  return {
    id: "cambios-1",
    origen: "cambios",
    schema: "identity",
    tabla: "users",
    rowPk: "1042",
    accion: "UPDATE",
    cambiadoEn: new Date().toISOString(),
    cambiadoPor: null,
    requestId: null,
    columnasCambiadas: [],
    cambios: [],
    actor: "Lucía Fernández",
    tipoActor: "persona",
    accionEtiqueta: "Cambio",
    modulo: "Identidad",
    objeto: "Solicitud #1042",
    resumen: "Solicitud #1042: Estado pendiente → aprobado",
    ...sobrescribir,
  };
}

function props(sobrescribir: Partial<PaginaAuditoria> = {}) {
  const pagina: PaginaAuditoria = {
    elementos: [evento({})],
    pagina: 1,
    tamanoPagina: 50,
    total: 1,
    parcial: false,
    fuentesNoDisponibles: [],
    ...sobrescribir,
  };
  return {
    pagina,
    cargando: false,
    esError: false,
    eventoSeleccionado: null,
    onSeleccionarFila: vi.fn(),
    onCambiarPagina: vi.fn(),
    onReintentar: vi.fn(),
  };
}

describe("ListaAuditoria", () => {
  it("muestra la paginación exacta «51–100 de 312» en la página 2", () => {
    render(
      <ListaAuditoria
        {...props({
          elementos: Array.from({ length: 50 }, (_, i) => evento({ id: `cambios-${i}` })),
          pagina: 2,
          tamanoPagina: 50,
          total: 312,
        })}
      />,
    );

    expect(screen.getByText("51–100 de 312")).toBeInTheDocument();
  });

  it("estado vacío sin paginación", () => {
    render(<ListaAuditoria {...props({ elementos: [], total: 0 })} />);

    expect(screen.getByText("No hay registros para estos filtros.")).toBeInTheDocument();
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });

  it("aviso de resultado parcial cuando falla el asistente", () => {
    render(<ListaAuditoria {...props({ parcial: true, fuentesNoDisponibles: ["asistente"] })} />);

    expect(
      screen.getByText("No se pudieron cargar los registros del asistente. Se muestran los demás."),
    ).toBeInTheDocument();
  });

  it("avatar neutral para «Proceso automático», distinto del de una persona", () => {
    render(
      <ListaAuditoria
        {...props({
          elementos: [
            evento({ id: "cambios-2", actor: "Proceso automático", tipoActor: "proceso" }),
          ],
        })}
      />,
    );

    const avatar = screen.getByText("PA");
    expect(avatar).toHaveClass("auditoria-avatar--neutral");
  });

  it("estado de error distinto del vacío, con Reintentar", () => {
    const onReintentar = vi.fn();
    render(<ListaAuditoria {...props()} esError onReintentar={onReintentar} />);

    expect(screen.getByRole("alert")).toHaveTextContent("No se pudieron cargar los registros.");
    expect(screen.queryByText("No hay registros para estos filtros.")).not.toBeInTheDocument();
  });
});
