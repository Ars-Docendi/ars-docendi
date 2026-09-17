import { Button, Input, StatusBadge, Table } from "@ars-docendi/ui";
import type { ReactNode } from "react";

import { FiltroEncabezado } from "../../../shared/ui/FiltroEncabezado";
import {
  FILTROS_DOCENTES_VACIOS,
  opcionesAmbitosDocentes,
  opcionesRolesDocentes,
  siguienteOrdenDocentes,
  VALOR_SIN_DATO,
  type ColumnaOrdenDocentes,
  type FiltrosDocentes,
  type OrdenDocentes,
} from "../filtrosDocentes";
import { nombreCompleto, type DocenteMock } from "../models";

interface TablaDocentesProps {
  docentes: DocenteMock[];
  docentesParaOpciones?: DocenteMock[];
  filtros?: FiltrosDocentes;
  orden?: OrdenDocentes | null;
  onFiltrosChange?: (filtros: FiltrosDocentes) => void;
  onOrdenChange?: (orden: OrdenDocentes | null) => void;
  onDesactivar: (docente: DocenteMock) => void;
  onActivar: (docente: DocenteMock) => void;
  onEditar: (docente: DocenteMock) => void;
  soloLectura?: boolean;
}

const SIN_CAMBIOS = () => {};

export function TablaDocentes({
  docentes,
  docentesParaOpciones = docentes,
  filtros = FILTROS_DOCENTES_VACIOS,
  orden = null,
  onFiltrosChange = SIN_CAMBIOS,
  onOrdenChange = SIN_CAMBIOS,
  onDesactivar,
  onActivar,
  onEditar,
  soloLectura = false,
}: TablaDocentesProps) {
  const roles = opcionesRolesDocentes(docentesParaOpciones);
  const ambitos = opcionesAmbitosDocentes(docentesParaOpciones);

  function cambiarFiltro<K extends keyof FiltrosDocentes>(campo: K, valor: FiltrosDocentes[K]) {
    onFiltrosChange({ ...filtros, [campo]: valor });
  }

  function cambiarOrden(columna: ColumnaOrdenDocentes) {
    onOrdenChange(siguienteOrdenDocentes(orden, columna));
  }

  function alternarOpcion(campo: "rol" | "ambitos" | "cuenta" | "estado", valor: string) {
    const valores = filtros[campo] as string[];
    cambiarFiltro(
      campo,
      (valores.includes(valor)
        ? valores.filter((actual) => actual !== valor)
        : [...valores, valor]) as FiltrosDocentes[typeof campo],
    );
  }

  function limpiar(campo: keyof FiltrosDocentes) {
    cambiarFiltro(
      campo,
      (Array.isArray(filtros[campo]) ? [] : "") as FiltrosDocentes[typeof campo],
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
              <Table.HeaderCell>
                <Encabezado etiqueta="Rol">
                  <FiltroEncabezado
                    etiqueta="Rol"
                    activo={filtros.rol.length > 0}
                    onLimpiar={() => limpiar("rol")}
                  >
                    <Opciones
                      opciones={roles}
                      valores={filtros.rol}
                      onToggle={(valor) => alternarOpcion("rol", valor)}
                      etiquetaSinDato="Sin rol"
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <Encabezado etiqueta="Ámbitos">
                  <FiltroEncabezado
                    etiqueta="Ámbitos"
                    activo={filtros.ambitos.length > 0}
                    onLimpiar={() => limpiar("ambitos")}
                  >
                    <Opciones
                      opciones={ambitos}
                      valores={filtros.ambitos}
                      onToggle={(valor) => alternarOpcion("ambitos", valor)}
                      etiquetaFormateada={formatearAmbito}
                      etiquetaSinDato="Sin ámbito"
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <Encabezado etiqueta="Asignaciones">
                  <FiltroEncabezado
                    etiqueta="Asignaciones"
                    activo={Boolean(filtros.asignaciones.trim())}
                    onLimpiar={() => limpiar("asignaciones")}
                  >
                    <Input
                      className="adoc-filtro-encabezado-campo"
                      placeholder="Buscar materia, código o cargo…"
                      aria-label="Buscar Asignaciones"
                      value={filtros.asignaciones}
                      onChange={(evento) => cambiarFiltro("asignaciones", evento.target.value)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell
                sort={orden?.columna === "cuenta" ? orden.direccion : null}
                onSortChange={() => cambiarOrden("cuenta")}
              >
                <Encabezado etiqueta="Cuenta">
                  <FiltroEncabezado
                    etiqueta="Cuenta"
                    activo={filtros.cuenta.length > 0}
                    onLimpiar={() => limpiar("cuenta")}
                  >
                    <Opciones
                      opciones={["con_cuenta", "sin_cuenta"]}
                      etiquetas={{ con_cuenta: "Con cuenta", sin_cuenta: "Sin cuenta" }}
                      valores={filtros.cuenta}
                      onToggle={(valor) => alternarOpcion("cuenta", valor)}
                    />
                  </FiltroEncabezado>
                </Encabezado>
              </Table.HeaderCell>
              <Table.HeaderCell style={{ whiteSpace: "nowrap", width: "1%" }}>
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
              {!soloLectura && (
                <Table.HeaderCell style={{ whiteSpace: "nowrap", width: "1%" }}>
                  Acciones
                </Table.HeaderCell>
              )}
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {docentes.map((docente) => (
              <Table.Row key={docente.id} data-inactivo={!docente.is_active || undefined}>
                <Table.Cell>{nombreCompleto(docente)}</Table.Cell>
                <Table.Cell className="adoc-mono">{docente.documento}</Table.Cell>
                <Table.Cell className="adoc-mono">{docente.legajo}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {docente.roles.map((rol) => (
                      <span
                        key={rol}
                        className={
                          rol === "Jefe de Cátedra"
                            ? "adoc-badge s-aprobado"
                            : "adoc-badge s-pendiente"
                        }
                        style={{ fontSize: "11px", height: "20px", padding: "0 8px" }}
                      >
                        {rol}
                      </span>
                    ))}
                  </div>
                </Table.Cell>
                <Table.Cell>{resumenAmbitos(docente.membresias)}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                    {docente.asignaciones.map((asignacion) => (
                      <span
                        key={asignacion.materia.codigo}
                        className="adoc-badge s-pendiente"
                        style={{ fontSize: "11px", height: "20px", padding: "0 8px" }}
                      >
                        {asignacion.materia.codigo} –{" "}
                        {asignacion.cargoAbreviatura ?? asignacion.cargo}
                      </span>
                    ))}
                  </div>
                </Table.Cell>
                <Table.Cell>
                  {docente.tieneCuenta ? (
                    <a
                      href={`/usuarios?personaId=${encodeURIComponent(docente.persona_id ?? docente.id)}`}
                    >
                      Ver usuario
                    </a>
                  ) : (
                    "Sin cuenta"
                  )}
                </Table.Cell>
                <Table.Cell style={{ whiteSpace: "nowrap", width: "1%" }}>
                  <StatusBadge
                    kind={docente.is_active ? "aprobado" : "rechazado"}
                    label={docente.is_active ? "Activo" : "Inactivo"}
                  />
                </Table.Cell>
                {!soloLectura && (
                  <Table.Cell
                    className="adoc-table-actions"
                    style={{ whiteSpace: "nowrap", width: "1%" }}
                  >
                    <Button variant="ghost" size="sm" onClick={() => onEditar(docente)}>
                      Editar
                    </Button>
                    {docente.is_active ? (
                      <Button variant="ghost" size="sm" onClick={() => onDesactivar(docente)}>
                        Desactivar
                      </Button>
                    ) : (
                      <Button variant="ghost" size="sm" onClick={() => onActivar(docente)}>
                        Activar
                      </Button>
                    )}
                  </Table.Cell>
                )}
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
  etiquetaFormateada,
  etiquetaSinDato,
}: {
  opciones: string[];
  valores: string[];
  onToggle: (valor: string) => void;
  etiquetas?: Record<string, string>;
  etiquetaFormateada?: (valor: string) => string;
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
          {opcion === VALOR_SIN_DATO
            ? etiquetaSinDato
            : (etiquetaFormateada?.(opcion) ?? etiquetas[opcion] ?? opcion)}
        </label>
      ))}
    </div>
  );
}

function formatearAmbito(ambito: string): string {
  return ambito.charAt(0).toUpperCase() + ambito.slice(1);
}

function resumenAmbitos(membresias: DocenteMock["membresias"]): string {
  const materias = membresias.filter((membresia) => membresia.ambito === "materia").length;
  const carreras = membresias.filter((membresia) => membresia.ambito === "carrera").length;
  const globales = membresias.filter((membresia) => membresia.ambito === "global").length;
  return (
    [
      materias && `${materias} materia${materias === 1 ? "" : "s"}`,
      carreras && `${carreras} carrera${carreras === 1 ? "" : "s"}`,
      globales && `${globales} global${globales === 1 ? "" : "es"}`,
    ]
      .filter(Boolean)
      .join(" · ") || "Sin ámbito"
  );
}
