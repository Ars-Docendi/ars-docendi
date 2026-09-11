import { Button, Input, StatusBadge, Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";

import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import {
  FILTROS_USUARIOS_VACIOS,
  opcionesRolesUsuarios,
  siguienteOrdenUsuarios,
  VALOR_SIN_DATO,
  type ColumnaOrdenUsuarios,
  type FiltrosUsuarios,
  type OrdenUsuarios,
} from "../filtrosUsuarios";
import { nombreCompleto, type UsuarioMock } from "../models";

interface TablaUsuariosProps {
  usuarios: UsuarioMock[];
  usuariosParaOpciones?: UsuarioMock[];
  filtros?: FiltrosUsuarios;
  orden?: OrdenUsuarios | null;
  onFiltrosChange?: (filtros: FiltrosUsuarios) => void;
  onOrdenChange?: (orden: OrdenUsuarios | null) => void;
  onDesactivar: (usuario: UsuarioMock) => void;
  onActivar: (usuario: UsuarioMock) => void;
  onEditarUsuario: (usuario: UsuarioMock) => void;
}

const SIN_CAMBIOS = () => {};

export function TablaUsuarios({
  usuarios,
  usuariosParaOpciones = usuarios,
  filtros = FILTROS_USUARIOS_VACIOS,
  orden = null,
  onFiltrosChange = SIN_CAMBIOS,
  onOrdenChange = SIN_CAMBIOS,
  onDesactivar,
  onActivar,
  onEditarUsuario,
}: TablaUsuariosProps) {
  const roles = opcionesRolesUsuarios(usuariosParaOpciones);

  function cambiarFiltro<K extends keyof FiltrosUsuarios>(campo: K, valor: FiltrosUsuarios[K]) {
    onFiltrosChange({ ...filtros, [campo]: valor });
  }

  function cambiarOrden(columna: ColumnaOrdenUsuarios) {
    onOrdenChange(siguienteOrdenUsuarios(orden, columna));
  }

  function alternarOpcion(campo: "roles" | "perfilDocente" | "estado", valor: string) {
    const valores = filtros[campo] as string[];
    cambiarFiltro(
      campo,
      (valores.includes(valor)
        ? valores.filter((actual) => actual !== valor)
        : [...valores, valor]) as FiltrosUsuarios[typeof campo],
    );
  }

  function limpiar(campo: keyof FiltrosUsuarios) {
    cambiarFiltro(
      campo,
      (Array.isArray(filtros[campo]) ? [] : "") as FiltrosUsuarios[typeof campo],
    );
  }

  return (
    <div style={{ overflowX: "auto" }}>
      <Table>
        <Table.Root>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell
                sort={orden?.columna === "nombre" ? orden.direccion : null}
                onSortChange={() => cambiarOrden("nombre")}
              >
                <Encabezado etiqueta="Apellido y Nombre">
                  <FiltroEncabezado
                    etiqueta="Apellido y Nombre"
                    activo={Boolean(filtros.apellidoNombre.trim())}
                    onLimpiar={() => limpiar("apellidoNombre")}
                  >
                    <Input
                      className="adoc-filtro-encabezado-campo"
                      placeholder="Buscar apellido o nombre…"
                      aria-label="Buscar Apellido y Nombre"
                      value={filtros.apellidoNombre}
                      onChange={(evento) => cambiarFiltro("apellidoNombre", evento.target.value)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell
                sort={orden?.columna === "documento" ? orden.direccion : null}
                onSortChange={() => cambiarOrden("documento")}
              >
                <Encabezado etiqueta="Documento">
                  <FiltroEncabezado
                    etiqueta="Documento"
                    activo={Boolean(filtros.documento.trim())}
                    onLimpiar={() => limpiar("documento")}
                  >
                    <Input
                      className="adoc-filtro-encabezado-campo"
                      placeholder="Buscar documento…"
                      aria-label="Buscar Documento"
                      value={filtros.documento}
                      onChange={(evento) => cambiarFiltro("documento", evento.target.value)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell
                sort={orden?.columna === "legajo" ? orden.direccion : null}
                onSortChange={() => cambiarOrden("legajo")}
              >
                <Encabezado etiqueta="Legajo">
                  <FiltroEncabezado
                    etiqueta="Legajo"
                    activo={Boolean(filtros.legajo.trim())}
                    onLimpiar={() => limpiar("legajo")}
                  >
                    <Input
                      className="adoc-filtro-encabezado-campo"
                      placeholder="Buscar legajo…"
                      aria-label="Buscar Legajo"
                      value={filtros.legajo}
                      onChange={(evento) => cambiarFiltro("legajo", evento.target.value)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell
                sort={orden?.columna === "upn" ? orden.direccion : null}
                onSortChange={() => cambiarOrden("upn")}
              >
                <Encabezado etiqueta="UPN / Email">
                  <FiltroEncabezado
                    etiqueta="UPN / Email"
                    activo={Boolean(filtros.upn.trim())}
                    onLimpiar={() => limpiar("upn")}
                  >
                    <Input
                      className="adoc-filtro-encabezado-campo"
                      placeholder="Buscar UPN o email…"
                      aria-label="Buscar UPN / Email"
                      value={filtros.upn}
                      onChange={(evento) => cambiarFiltro("upn", evento.target.value)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <Encabezado etiqueta="Roles">
                  <FiltroEncabezado
                    etiqueta="Roles"
                    activo={filtros.roles.length > 0}
                    onLimpiar={() => limpiar("roles")}
                  >
                    <Opciones
                      opciones={roles}
                      valores={filtros.roles}
                      onToggle={(valor) => alternarOpcion("roles", valor)}
                      etiquetaSinDato="Sin rol"
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <Encabezado etiqueta="Perfil docente">
                  <FiltroEncabezado
                    etiqueta="Perfil docente"
                    activo={filtros.perfilDocente.length > 0}
                    onLimpiar={() => limpiar("perfilDocente")}
                  >
                    <Opciones
                      opciones={["si", "no"]}
                      etiquetas={{ si: "Con perfil docente", no: "Sin perfil docente" }}
                      valores={filtros.perfilDocente}
                      onToggle={(valor) => alternarOpcion("perfilDocente", valor)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <Encabezado etiqueta="Estado">
                  <FiltroEncabezado
                    etiqueta="Estado"
                    activo={filtros.estado.length > 0}
                    onLimpiar={() => limpiar("estado")}
                  >
                    <Opciones
                      opciones={["activo", "inactivo"]}
                      etiquetas={{ activo: "Activo", inactivo: "Inactivo" }}
                      valores={filtros.estado}
                      onToggle={(valor) => alternarOpcion("estado", valor)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell>Acciones</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {usuarios.map((usuario) => (
              <Table.Row key={usuario.id} data-inactivo={!usuario.is_active || undefined}>
                <Table.Cell>{nombreCompleto(usuario)}</Table.Cell>
                <Table.Cell className="adoc-mono">{usuario.documento}</Table.Cell>
                <Table.Cell className="adoc-mono">{usuario.legajo}</Table.Cell>
                <Table.Cell>{usuario.upn}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {usuario.roles.map((rol) => (
                      <span
                        key={rol}
                        className="adoc-badge s-pendiente"
                        style={{ fontSize: "11px", height: "20px", padding: "0 8px" }}
                      >
                        {rol}
                      </span>
                    ))}
                  </div>
                </Table.Cell>
                <Table.Cell>
                  {usuario.perfilDocente.esDocente ? (
                    <a href={`/docentes?personaId=${encodeURIComponent(usuario.persona_id)}`}>
                      Ver docente · {usuario.perfilDocente.cantidadMaterias} materias
                    </a>
                  ) : (
                    "No"
                  )}
                </Table.Cell>
                <Table.Cell>
                  <StatusBadge
                    kind={usuario.is_active ? "aprobado" : "rechazado"}
                    label={usuario.is_active ? "Activo" : "Inactivo"}
                  />
                </Table.Cell>
                <Table.Cell className="adoc-table-actions">
                  <Button variant="ghost" size="sm" onClick={() => onEditarUsuario(usuario)}>
                    Editar
                  </Button>
                  {usuario.is_active ? (
                    <Button variant="ghost" size="sm" onClick={() => onDesactivar(usuario)}>
                      Desactivar
                    </Button>
                  ) : (
                    <Button variant="ghost" size="sm" onClick={() => onActivar(usuario)}>
                      Activar
                    </Button>
                  )}
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table.Root>
      </Table>
    </div>
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
  onToggle,
  etiquetas = {},
  etiquetaSinDato,
}: {
  opciones: string[];
  valores: string[];
  onToggle: (valor: string) => void;
  etiquetas?: Record<string, string>;
  etiquetaSinDato?: string;
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
          {opcion === VALOR_SIN_DATO ? etiquetaSinDato : (etiquetas[opcion] ?? opcion)}
        </label>
      ))}
    </div>
  );
}
