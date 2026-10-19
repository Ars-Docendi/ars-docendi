import { Input, Table } from "@ars-docendi/ui";
import { useState } from "react";
import type { ReactNode } from "react";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import type { Comision } from "../api/comisionesApi";
import {
  aplicarFiltrosComisiones,
  aulasDeComision,
  FILTROS_COMISIONES_INICIALES,
  formatearBanda,
  type FiltrosComisiones,
} from "./filtrosComisiones";
import "./tablaComisiones.css";

/** Alto de la grilla: la página no scrollea, scrollea la tabla con el encabezado fijo. */
const ALTO_TABLA = "calc(100vh - 340px)";
const VACIO = "—";

/** Grilla de sólo lectura de las comisiones de un período; filtros locales, sin paginación. */
export function TablaComisiones({ comisiones }: { comisiones: Comision[] }) {
  const [filtros, setFiltros] = useState<FiltrosComisiones>(FILTROS_COMISIONES_INICIALES);
  const visibles = aplicarFiltrosComisiones(comisiones, filtros);

  function filtro(campo: keyof FiltrosComisiones, etiqueta: string, placeholder: string) {
    return (
      <EncabezadoFiltrable
        etiqueta={etiqueta}
        activo={Boolean(filtros[campo].trim())}
        onLimpiar={() => setFiltros((actual) => ({ ...actual, [campo]: "" }))}
      >
        <Input
          className="adoc-filtro-encabezado-campo"
          placeholder={placeholder}
          aria-label={`Buscar ${etiqueta}`}
          value={filtros[campo]}
          onChange={(evento) =>
            setFiltros((actual) => ({ ...actual, [campo]: evento.target.value }))
          }
        />
      </EncabezadoFiltrable>
    );
  }

  return (
    <Table className="adoc-comisiones-table">
      <Table.Root maxHeight={ALTO_TABLA}>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Comisión</Table.HeaderCell>
            {filtro("materia", "Materia", "Nombre o código…")}
            {filtro("carrera", "Carreras", "Nombre o código…")}
            <Table.HeaderCell>Aula</Table.HeaderCell>
            <Table.HeaderCell>Bandas horarias</Table.HeaderCell>
            {filtro("docente", "Docentes", "Buscar docente…")}
            {filtro("modalidad", "Modalidad", "Buscar modalidad…")}
            <Table.HeaderCell>Ubicación</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {visibles.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={8} className="adoc-comisiones-sin-resultados">
                Sin resultados para los filtros aplicados.
              </Table.Cell>
            </Table.Row>
          ) : (
            visibles.map((comision) => (
              <Table.Row key={comision.id}>
                <Table.Cell>{comision.nombre}</Table.Cell>
                <Table.Cell>
                  <Lista items={[`${comision.materia.nombre} (${comision.materia.codigo})`]} />
                </Table.Cell>
                <Table.Cell>
                  <Lista items={comision.carreras.map((k) => `${k.nombre} (${k.codigo})`)} />
                </Table.Cell>
                <Table.Cell>
                  <Lista items={aulasDeComision(comision)} />
                </Table.Cell>
                <Table.Cell>
                  <Lista items={comision.horarios.map(formatearBanda)} />
                </Table.Cell>
                <Table.Cell>
                  <Lista
                    items={comision.docentes.map((d) =>
                      d.responsabilidad ? `${d.nombre} (${d.responsabilidad})` : d.nombre,
                    )}
                  />
                </Table.Cell>
                <Table.Cell>{comision.modalidad ?? comision.modalidadCodigo ?? VACIO}</Table.Cell>
                <Table.Cell>{comision.ubicacion ?? VACIO}</Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table.Root>
    </Table>
  );
}

function Lista({ items }: { items: string[] }) {
  if (items.length === 0) return <>{VACIO}</>;
  if (items.length === 1) return <>{items[0]}</>;
  return (
    <ul className="adoc-comisiones-lista">
      {items.map((item, indice) => (
        <li key={`${indice}-${item}`}>{item}</li>
      ))}
    </ul>
  );
}

function EncabezadoFiltrable({
  etiqueta,
  activo,
  onLimpiar,
  children,
}: {
  etiqueta: string;
  activo: boolean;
  onLimpiar: () => void;
  children: ReactNode;
}) {
  return (
    <Table.HeaderCell aria-label={etiqueta}>
      <span>
        {etiqueta}
        <FiltroEncabezado etiqueta={etiqueta} activo={activo} onLimpiar={onLimpiar}>
          {children}
        </FiltroEncabezado>
      </span>
    </Table.HeaderCell>
  );
}
