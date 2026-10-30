import { Button, Input, StatusBadge, Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";

import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import { propsFilaClickeable } from "../../../shared/ui/filaClickeable";
import { TextoRecortado } from "../../../shared/ui/TextoRecortado";
import {
  ETIQUETAS_ESTADO,
  ETIQUETAS_TIPO,
  estadoDe,
  type EstadoRol,
  type ColumnaOrdenRol,
  type FiltrosRoles,
  type OrdenRoles,
  type TipoRol,
} from "../filtrosRoles";
import { ETIQUETAS_SCOPE, SCOPES_ROL, type RolMock, type ScopeRol } from "../models";

/** Alto de la grilla: la página no scrollea, scrollea la tabla con el encabezado fijo. */
const ALTO_TABLA = "calc(100vh - 300px)";
const ANGOSTA = { whiteSpace: "nowrap", width: "1%" } as const;

interface TablaRolesProps {
  roles: RolMock[];
  totalPermisos: number;
  filtros: FiltrosRoles;
  orden: OrdenRoles | null;
  onFiltrosChange: (filtros: FiltrosRoles) => void;
  onOrdenar: (columna: ColumnaOrdenRol) => void;
  onAbrir: (rol: RolMock) => void;
  /** Sin permiso de administración no se activa ni desactiva. */
  onCambiarEstado?: (rol: RolMock) => void;
  textoAbrir: string;
}

export function TablaRoles({
  roles,
  totalPermisos,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenar,
  onAbrir,
  onCambiarEstado,
  textoAbrir,
}: TablaRolesProps) {
  const cambiar = <K extends keyof FiltrosRoles>(campo: K, valor: FiltrosRoles[K]) =>
    onFiltrosChange({ ...filtros, [campo]: valor });
  const alternar = <K extends "ambito" | "tipo" | "estado">(
    campo: K,
    valor: FiltrosRoles[K][number],
  ) => {
    const actuales = filtros[campo] as string[];
    cambiar(
      campo,
      (actuales.includes(valor)
        ? actuales.filter((x) => x !== valor)
        : [...actuales, valor]) as FiltrosRoles[K],
    );
  };
  const sort = (columna: ColumnaOrdenRol) => (orden?.columna === columna ? orden.direccion : null);

  const filtroTexto = (campo: "nombre" | "descripcion", etiqueta: string) => (
    <FiltroEncabezado
      etiqueta={etiqueta}
      activo={Boolean(filtros[campo].trim())}
      onLimpiar={() => cambiar(campo, "")}
    >
      <Input
        className="adoc-filtro-encabezado-campo"
        placeholder={`Buscar ${etiqueta.toLowerCase()}…`}
        aria-label={`Buscar ${etiqueta}`}
        value={filtros[campo]}
        onChange={(e) => cambiar(campo, e.target.value)}
      />
    </FiltroEncabezado>
  );

  return (
    <Table>
      <Table.Root maxHeight={ALTO_TABLA}>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell sort={sort("nombre")} onSortChange={() => onOrdenar("nombre")}>
              <Encabezado etiqueta="Nombre">{filtroTexto("nombre", "Nombre")}</Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell>
              <Encabezado etiqueta="Descripción">
                {filtroTexto("descripcion", "Descripción")}
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell
              sort={sort("ambito")}
              onSortChange={() => onOrdenar("ambito")}
              style={ANGOSTA}
            >
              <Encabezado etiqueta="Ámbito">
                <FiltroEncabezado
                  etiqueta="Ámbito"
                  activo={filtros.ambito.length > 0}
                  onLimpiar={() => cambiar("ambito", [])}
                >
                  <Opciones
                    opciones={SCOPES_ROL}
                    valores={filtros.ambito}
                    etiqueta={(o) => ETIQUETAS_SCOPE[o as ScopeRol]}
                    onToggle={(o) => alternar("ambito", o as ScopeRol)}
                  />
                </FiltroEncabezado>
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell
              sort={sort("tipo")}
              onSortChange={() => onOrdenar("tipo")}
              style={ANGOSTA}
            >
              <Encabezado etiqueta="Tipo">
                <FiltroEncabezado
                  etiqueta="Tipo"
                  activo={filtros.tipo.length > 0}
                  onLimpiar={() => cambiar("tipo", [])}
                >
                  <Opciones
                    opciones={Object.keys(ETIQUETAS_TIPO)}
                    valores={filtros.tipo}
                    etiqueta={(o) => ETIQUETAS_TIPO[o as TipoRol]}
                    onToggle={(o) => alternar("tipo", o as TipoRol)}
                  />
                </FiltroEncabezado>
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell
              sort={sort("permisos")}
              onSortChange={() => onOrdenar("permisos")}
              style={ANGOSTA}
            >
              Permisos
            </Table.HeaderCell>
            <Table.HeaderCell
              sort={sort("estado")}
              onSortChange={() => onOrdenar("estado")}
              style={ANGOSTA}
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
                    etiqueta={(o) => ETIQUETAS_ESTADO[o as EstadoRol]}
                    onToggle={(o) => alternar("estado", o as EstadoRol)}
                  />
                </FiltroEncabezado>
              </Encabezado>
            </Table.HeaderCell>
            <Table.HeaderCell style={ANGOSTA}>Acciones</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {roles.length === 0 && (
            <Table.Row>
              <Table.Cell colSpan={7} className="empty">
                No hay roles que coincidan con los filtros.
              </Table.Cell>
            </Table.Row>
          )}
          {roles.map((rol) => {
            return (
              <Table.Row key={rol.id} {...propsFilaClickeable(() => onAbrir(rol))}>
                <Table.Cell>
                  <TextoRecortado texto={rol.nombre} anchoMinimo={160} />
                </Table.Cell>
                <Table.Cell>
                  <TextoRecortado texto={rol.descripcion || "—"} anchoMinimo={200} />
                </Table.Cell>
                <Table.Cell style={ANGOSTA}>{ETIQUETAS_SCOPE[rol.scope]}</Table.Cell>
                <Table.Cell style={ANGOSTA}>
                  {ETIQUETAS_TIPO[rol.es_sistema ? "sistema" : "personalizado"]}
                </Table.Cell>
                <Table.Cell className="adoc-mono" style={ANGOSTA}>
                  {rol.permisos.length}/{totalPermisos}
                </Table.Cell>
                <Table.Cell style={ANGOSTA}>
                  <StatusBadge
                    kind={rol.activo ? "aprobado" : "rechazado"}
                    label={ETIQUETAS_ESTADO[estadoDe(rol)]}
                  />
                </Table.Cell>
                <Table.Cell className="adoc-table-actions adoc-acciones-fila" style={ANGOSTA}>
                  <Button variant="ghost" size="sm" onClick={() => onAbrir(rol)}>
                    {textoAbrir}
                  </Button>
                  {onCambiarEstado && !rol.es_sistema && (
                    <Button variant="ghost" size="sm" onClick={() => onCambiarEstado(rol)}>
                      {rol.activo ? "Desactivar" : "Activar"}
                    </Button>
                  )}
                </Table.Cell>
              </Table.Row>
            );
          })}
        </Table.Body>
      </Table.Root>
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
  opciones: readonly string[];
  valores: readonly string[];
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
