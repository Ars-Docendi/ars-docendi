import { Button, Input, Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";

import { ContenedorTabla } from "../../../shared/ui/ContenedorTabla";
import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import { EnlaceFila } from "../../../shared/navegacion/EnlaceFila";
import { propsFilaClickeable } from "../../../shared/ui/filaClickeable";
import { TextoRecortado } from "../../../shared/ui/TextoRecortado";
import type { Persona } from "../api/personasApi";
import { InsigniaEstado, InsigniasAsignaciones, InsigniasRoles } from "./insigniasPersona";
import {
  ETIQUETAS_ESTADO,
  SIN_ROL,
  nombreCompleto,
  type ColumnaOrden,
  type EstadoPersona,
  type FiltrosPersonas,
  type OrdenPersonas,
} from "../filtrosPersonas";

/** Alto de la grilla: la página no scrollea, scrollea la tabla con el encabezado fijo. */
const ALTO_TABLA = "calc(100dvh - 300px)";
const ANCHO_NOMBRE = 160;

interface TablaPersonasProps {
  personas: Persona[];
  opcionesRol: string[];
  filtros: FiltrosPersonas;
  orden: OrdenPersonas | null;
  onFiltrosChange: (filtros: FiltrosPersonas) => void;
  onOrdenar: (columna: ColumnaOrden) => void;
  onAbrir: (persona: Persona) => void;
  /** Sin permiso de administración no se activa ni desactiva. */
  onCambiarEstado?: (persona: Persona) => void;
  textoAbrir: string;
}

export function TablaPersonas({
  personas,
  opcionesRol,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenar,
  onAbrir,
  onCambiarEstado,
  textoAbrir,
}: TablaPersonasProps) {
  const cambiar = <K extends keyof FiltrosPersonas>(campo: K, valor: FiltrosPersonas[K]) =>
    onFiltrosChange({ ...filtros, [campo]: valor });
  const alternar = (campo: "rol" | "estado", valor: string) => {
    const actuales = filtros[campo] as string[];
    cambiar(
      campo,
      (actuales.includes(valor)
        ? actuales.filter((x) => x !== valor)
        : [...actuales, valor]) as never,
    );
  };
  const sort = (columna: ColumnaOrden) => (orden?.columna === columna ? orden.direccion : null);

  const filtroTexto = (
    campo: "nombre" | "documento" | "legajo" | "asignaciones",
    etiqueta: string,
    placeholder: string,
  ) => (
    <FiltroEncabezado
      etiqueta={etiqueta}
      activo={Boolean(filtros[campo].trim())}
      onLimpiar={() => cambiar(campo, "")}
    >
      <Input
        className="adoc-filtro-encabezado-campo"
        placeholder={placeholder}
        aria-label={`Buscar ${etiqueta}`}
        value={filtros[campo]}
        onChange={(e) => cambiar(campo, e.target.value)}
      />
    </FiltroEncabezado>
  );

  return (
    <Table>
      <ContenedorTabla etiqueta="Personas" maxHeight={ALTO_TABLA}>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell sort={sort("nombre")} onSortChange={() => onOrdenar("nombre")}>
              <Encabezado etiqueta="Nombre">
                {filtroTexto("nombre", "Nombre", "Buscar apellido o nombre…")}
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell sort={sort("documento")} onSortChange={() => onOrdenar("documento")}>
              <Encabezado etiqueta="Documento">
                {filtroTexto("documento", "Documento", "Buscar documento…")}
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell sort={sort("legajo")} onSortChange={() => onOrdenar("legajo")}>
              <Encabezado etiqueta="Legajo">
                {filtroTexto("legajo", "Legajo", "Buscar legajo…")}
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell>
              <Encabezado etiqueta="Rol">
                <FiltroEncabezado
                  etiqueta="Rol"
                  activo={filtros.rol.length > 0}
                  onLimpiar={() => cambiar("rol", [])}
                >
                  <Opciones
                    opciones={opcionesRol}
                    valores={filtros.rol}
                    etiqueta={(o) => (o === SIN_ROL ? "Sin rol" : o)}
                    onToggle={(o) => alternar("rol", o)}
                  />
                </FiltroEncabezado>
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell>
              <Encabezado etiqueta="Asignaciones">
                {filtroTexto("asignaciones", "Asignaciones", "Buscar materia, código o cargo…")}
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell
              sort={sort("estado")}
              onSortChange={() => onOrdenar("estado")}
              style={{ whiteSpace: "nowrap", width: "1%" }}
            >
              <Encabezado etiqueta="Estado">
                <FiltroEncabezado
                  etiqueta="Estado"
                  activo={filtros.estado.length > 0}
                  onLimpiar={() => cambiar("estado", [])}
                >
                  <Opciones
                    opciones={Object.keys(ETIQUETAS_ESTADO)}
                    valores={filtros.estado}
                    etiqueta={(o) => ETIQUETAS_ESTADO[o as EstadoPersona]}
                    onToggle={(o) => alternar("estado", o)}
                  />
                </FiltroEncabezado>
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell style={{ whiteSpace: "nowrap", width: "1%" }}>
              Acciones
            </Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {personas.length === 0 && (
            <Table.Row>
              <Table.Cell colSpan={7} className="empty">
                No hay personas que coincidan con los filtros.
              </Table.Cell>
            </Table.Row>
          )}
          {personas.map((persona) => {
            return (
              <Table.Row key={persona.personaId} {...propsFilaClickeable(() => onAbrir(persona))}>
                <Table.Cell>
                  <EnlaceFila
                    ruta={`/personas/${persona.personaId}`}
                    etiqueta={`Ver a ${nombreCompleto(persona)}`}
                  >
                    <TextoRecortado texto={nombreCompleto(persona)} anchoMinimo={ANCHO_NOMBRE} />
                  </EnlaceFila>
                </Table.Cell>
                <Table.Cell className="adoc-mono">{persona.documento}</Table.Cell>
                <Table.Cell className="adoc-mono">{persona.legajo ?? "—"}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    <InsigniasRoles persona={persona} />
                  </div>
                </Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    <InsigniasAsignaciones persona={persona} />
                  </div>
                </Table.Cell>
                <Table.Cell style={{ whiteSpace: "nowrap", width: "1%" }}>
                  <InsigniaEstado persona={persona} />
                </Table.Cell>
                <Table.Cell
                  className="adoc-table-actions adoc-acciones-fila"
                  style={{ whiteSpace: "nowrap", width: "1%" }}
                >
                  <Button variant="ghost" size="sm" onClick={() => onAbrir(persona)}>
                    {textoAbrir}
                  </Button>
                  {onCambiarEstado && persona.tieneCuenta && (
                    <Button variant="ghost" size="sm" onClick={() => onCambiarEstado(persona)}>
                      {persona.activo ? "Desactivar" : "Activar"}
                    </Button>
                  )}
                </Table.Cell>
              </Table.Row>
            );
          })}
        </Table.Body>
      </ContenedorTabla>
    </Table>
  );
}

function Encabezado({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <span>
      {etiqueta}
      {children}
    </span>
  );
}

function Opciones({
  opciones,
  valores,
  etiqueta,
  onToggle,
}: {
  opciones: string[];
  valores: string[];
  etiqueta: (opcion: string) => string;
  onToggle: (opcion: string) => void;
}) {
  return (
    <div className="adoc-filtro-encabezado-opciones">
      {opciones.map((opcion) => (
        <label className="adoc-filtro-encabezado-opcion" key={opcion}>
          <input
            type="checkbox"
            checked={valores.includes(opcion)}
            onChange={() => onToggle(opcion)}
          />
          {etiqueta(opcion)}
        </label>
      ))}
    </div>
  );
}
