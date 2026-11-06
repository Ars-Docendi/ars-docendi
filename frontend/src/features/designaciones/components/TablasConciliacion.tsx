import { Fragment, type MouseEvent } from "react";
import { Button, Checkbox, StatusBadge, Table } from "@ars-docendi/ui";
import { ContenedorTabla } from "../../../shared/ui/ContenedorTabla";
import type { PedidoConciliacion, ValoresDesignacion } from "../api/conciliacionApi";
import type { Novedad } from "../types";
import { NovedadChip } from "./NovedadChip";
import {
  aceptable,
  carrerasDe,
  describirValores,
  nombreCompleto,
  pendientes,
  plural,
  resultado,
  sinCambios,
  tipoBajaTexto,
  type DocenteConciliacion,
} from "./conciliacionModelo";

const NOVEDADES: Novedad[] = ["Alta", "Cambio de cargo o dedicación", "Baja"];
const ALTO_TABLA = "calc(100vh - 300px)";

const fechaHora = new Intl.DateTimeFormat("es-AR", { dateStyle: "short", timeStyle: "short" });

function Valores({ valores }: { valores: ValoresDesignacion }) {
  const extras = [
    valores.horasInvestigacion ? `Investigación ${valores.horasInvestigacion} h` : null,
    valores.horasExternas ? `Externas ${valores.horasExternas} h` : null,
  ].filter(Boolean);
  return (
    <>
      {[valores.cargo, valores.dedicacion].filter(Boolean).join(" ")}, {valores.horas ?? 0} h
      {extras.length > 0 && <span className="conc-extra">{extras.join(", ")}</span>}
    </>
  );
}

/** Cuántas materias tiene hoy y con cuántas queda: "2 → 3", o "2" si no cambia. */
function MateriasDelDocente({ hoy, queda }: { hoy: number; queda: number }) {
  return (
    <span className="conc-materias">
      <span className="conc-sr-only">
        {hoy === queda
          ? `Sigue con ${plural(queda, "materia", "materias")}`
          : `Pasa de ${hoy} a ${plural(queda, "materia", "materias")}`}
      </span>
      <span aria-hidden="true">
        {hoy !== queda && <>{hoy} → </>}
        <strong>{queda}</strong>
      </span>
    </span>
  );
}

function Docente({ docente, nueva }: { docente: DocenteConciliacion; nueva?: boolean }) {
  return (
    <div className="conc-docente">
      <span className="nombre">
        {nombreCompleto(docente)}
        {nueva && <span className="conc-chip-persona-nueva">Persona nueva</span>}
      </span>
      <span className="cuil">{docente.cuil ?? `DNI ${docente.documento}`}</span>
    </div>
  );
}

const detener = (evento: MouseEvent) => evento.stopPropagation();

interface AccionesPedido {
  onEditar: (pedido: PedidoConciliacion) => void;
  onRechazar: (docente: DocenteConciliacion, pedido: PedidoConciliacion) => void;
}

function FilaDiff({
  docente,
  pedido,
  onEditar,
  onRechazar,
}: { docente: DocenteConciliacion; pedido: PedidoConciliacion } & AccionesPedido) {
  const rechazado = pedido.estado === "no_aprobado";
  return (
    <tr className={rechazado ? "rechazado" : undefined}>
      <td>
        {pedido.materia}
        {rechazado && pedido.motivoRechazo && (
          <span className="conc-motivo">Rechazado: {pedido.motivoRechazo}</span>
        )}
        {pedido.novedad === "Baja" && pedido.tipoBaja && (
          <span className="conc-nota">Tipo de baja: {tipoBajaTexto(pedido)}</span>
        )}
        {pedido.novedad === "Cambio de cargo o dedicación" && pedido.justificacion && (
          <span className="conc-nota">Justificación: {pedido.justificacion}</span>
        )}
      </td>
      <td className="conc-carrera">{pedido.carrera}</td>
      <td>
        {pedido.hoy ? (
          <Valores valores={pedido.hoy} />
        ) : (
          <span className="conc-vacio">Sin designación</span>
        )}
      </td>
      <td>
        {rechazado ? (
          <span className="conc-vacio">No aplica</span>
        ) : pedido.novedad === "Baja" ? (
          <span className="conc-baja">Se da de baja</span>
        ) : pedido.queda ? (
          <Valores valores={pedido.queda} />
        ) : null}
      </td>
      <td>
        <NovedadChip novedad={pedido.novedad} />
      </td>
      <td className="conc-numero">{pedido.numero}</td>
      <td className="conc-acciones">
        {rechazado ? (
          <StatusBadge kind="rechazado" label="Rechazado" />
        ) : (
          <>
            {pedido.acciones.includes("editar") && (
              <Button variant="ghost" size="sm" onClick={() => onEditar(pedido)}>
                Editar
              </Button>
            )}
            {pedido.acciones.includes("rechazar") && (
              <Button variant="ghost" size="sm" onClick={() => onRechazar(docente, pedido)}>
                Rechazar
              </Button>
            )}
          </>
        )}
      </td>
    </tr>
  );
}

function Detalle({ docente, ...acciones }: { docente: DocenteConciliacion } & AccionesPedido) {
  const abiertos = docente.pedidos.filter((pedido) => pedido.estado !== "aprobado");
  const queda = resultado(docente);
  const horas = queda.reduce((suma, d) => suma + (d.valores.horas ?? 0), 0);
  return (
    <tr className="conc-detalle">
      <td colSpan={7}>
        <div className="conc-detalle-inner">
          <table className="conc-diff">
            <thead>
              <tr>
                <th>Materia</th>
                <th>Carrera</th>
                <th>Hoy</th>
                <th>Queda</th>
                <th>Novedad</th>
                <th>Pedido</th>
                <th>
                  <span className="conc-sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {abiertos.map((pedido) => (
                <FilaDiff key={pedido.id} docente={docente} pedido={pedido} {...acciones} />
              ))}
              {sinCambios(docente).map((vigente) => (
                <tr key={vigente.materiaId} className="sin-cambios">
                  <td>{vigente.materia}</td>
                  <td className="conc-carrera">{vigente.carrera}</td>
                  <td>
                    <Valores valores={vigente.valores} />
                  </td>
                  <td>
                    <Valores valores={vigente.valores} />
                  </td>
                  <td colSpan={3}>Sin cambios</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="conc-detalle-pie">
            {docente.personaNueva && "Persona nueva: se da de alta en el sistema al sincronizar. "}
            Queda con <strong>{plural(queda.length, "materia", "materias")}</strong> y{" "}
            <strong>{horas} h</strong> semanales de materia.
          </p>
        </div>
      </td>
    </tr>
  );
}

interface TablaPorConciliarProps extends AccionesPedido {
  docentes: DocenteConciliacion[];
  aceptados: ReadonlySet<string>;
  abiertos: ReadonlySet<string>;
  onAceptar: (docentes: DocenteConciliacion[], valor: boolean) => void;
  onAlternar: (personaId: string) => void;
}

export function TablaPorConciliar({
  docentes,
  aceptados,
  abiertos,
  onAceptar,
  onAlternar,
  ...acciones
}: TablaPorConciliarProps) {
  const tildables = docentes.filter(aceptable);
  const todos = tildables.length > 0 && tildables.every((d) => aceptados.has(d.personaId));
  const alguno = tildables.some((d) => aceptados.has(d.personaId));
  return (
    <Table>
      <ContenedorTabla etiqueta="Docentes por conciliar" maxHeight={ALTO_TABLA}>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell className="select-col">
              <Checkbox
                aria-label="Aceptar todos los docentes visibles"
                checked={todos}
                indeterminate={!todos && alguno}
                disabled={tildables.length === 0}
                onChange={() => onAceptar(tildables, !todos)}
              />
            </Table.HeaderCell>
            <Table.HeaderCell>
              <span className="conc-sr-only">Detalle</span>
            </Table.HeaderCell>
            <Table.HeaderCell>Docente</Table.HeaderCell>
            <Table.HeaderCell className="conc-col-carrera">Carrera</Table.HeaderCell>
            <Table.HeaderCell>Cambios</Table.HeaderCell>
            <Table.HeaderCell className="conc-col-materias">Materias</Table.HeaderCell>
            <Table.HeaderCell>Estado</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {docentes.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={7} className="empty">
                Sin docentes que cumplan los filtros.
              </Table.Cell>
            </Table.Row>
          ) : (
            docentes.map((docente) => {
              const lista = pendientes(docente);
              const abierto = abiertos.has(docente.personaId);
              const aceptado = aceptados.has(docente.personaId);
              const rechazados = docente.pedidos.filter((p) => p.estado === "no_aprobado").length;
              const nombre = nombreCompleto(docente);
              return (
                <Fragment key={docente.personaId}>
                  <Table.Row
                    selected={aceptado}
                    className={`conc-fila-docente${abierto ? " abierta" : ""}`}
                    onClick={() => onAlternar(docente.personaId)}
                  >
                    <Table.Cell className="select-col" onClick={detener}>
                      <Checkbox
                        aria-label={`Aceptar los cambios de ${nombre}`}
                        checked={aceptado}
                        disabled={!aceptable(docente)}
                        onChange={() => onAceptar([docente], !aceptado)}
                      />
                    </Table.Cell>
                    <Table.Cell className="conc-col-chevron">
                      <button
                        type="button"
                        className="conc-chevron"
                        aria-expanded={abierto}
                        aria-label={`${abierto ? "Ocultar" : "Ver"} el detalle de ${nombre}`}
                        onClick={(evento) => {
                          evento.stopPropagation();
                          onAlternar(docente.personaId);
                        }}
                      >
                        <svg
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2.5"
                          aria-hidden="true"
                        >
                          <path d="m9 6 6 6-6 6" />
                        </svg>
                      </button>
                    </Table.Cell>
                    <Table.Cell>
                      <Docente docente={docente} nueva={docente.personaNueva} />
                    </Table.Cell>
                    <Table.Cell className="conc-col-carrera">
                      <div className="conc-carreras">
                        {carrerasDe(docente).map((carrera) => (
                          <span key={carrera}>{carrera}</span>
                        ))}
                      </div>
                    </Table.Cell>
                    <Table.Cell>
                      <div className="conc-chips">
                        {NOVEDADES.map((novedad) => {
                          const n = lista.filter((p) => p.novedad === novedad).length;
                          return n > 0 ? (
                            <NovedadChip key={novedad} novedad={novedad} cantidad={n} />
                          ) : null;
                        })}
                      </div>
                    </Table.Cell>
                    <Table.Cell className="conc-col-materias">
                      <MateriasDelDocente
                        hoy={docente.vigentes.length}
                        queda={resultado(docente).length}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      <span className="conc-estados">
                        {aceptado ? (
                          <StatusBadge kind="aprobado" label="Aceptado" />
                        ) : (
                          <StatusBadge kind="pendiente" label="Por conciliar" />
                        )}
                        {rechazados > 0 && (
                          <StatusBadge
                            kind="rechazado"
                            label={plural(rechazados, "rechazado", "rechazados")}
                          />
                        )}
                      </span>
                    </Table.Cell>
                  </Table.Row>
                  {abierto && <Detalle docente={docente} {...acciones} />}
                </Fragment>
              );
            })
          )}
        </Table.Body>
      </ContenedorTabla>
    </Table>
  );
}

type Fila = { docente: DocenteConciliacion; pedido: PedidoConciliacion };

export function TablaRechazados({
  filas,
  onEditar,
}: {
  filas: Fila[];
  onEditar: (pedido: PedidoConciliacion) => void;
}) {
  return (
    <Table>
      <ContenedorTabla etiqueta="Cambios rechazados" maxHeight={ALTO_TABLA}>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Docente</Table.HeaderCell>
            <Table.HeaderCell>Materia</Table.HeaderCell>
            <Table.HeaderCell>Novedad</Table.HeaderCell>
            <Table.HeaderCell>Pedido</Table.HeaderCell>
            <Table.HeaderCell>Motivo</Table.HeaderCell>
            <Table.HeaderCell>
              <span className="conc-sr-only">Acciones</span>
            </Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {filas.map(({ docente, pedido }) => (
            <Table.Row key={pedido.id}>
              <Table.Cell>
                <Docente docente={docente} />
              </Table.Cell>
              <Table.Cell>
                {pedido.materia}
                <span className="conc-nota-tabla">{pedido.carrera}</span>
              </Table.Cell>
              <Table.Cell>
                <NovedadChip novedad={pedido.novedad} />
              </Table.Cell>
              <Table.Cell className="conc-numero">{pedido.numero}</Table.Cell>
              <Table.Cell className="conc-col-motivo">{pedido.motivoRechazo}</Table.Cell>
              <Table.Cell className="conc-acciones">
                {pedido.acciones.includes("editar") && (
                  <Button
                    variant="secondary"
                    size="sm"
                    title="Al guardar vuelve a Por conciliar para aceptarlo y sincronizarlo"
                    onClick={() => onEditar(pedido)}
                  >
                    Editar y volver a conciliar
                  </Button>
                )}
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </ContenedorTabla>
    </Table>
  );
}

export function TablaSincronizados({ filas }: { filas: Fila[] }) {
  return (
    <Table>
      <ContenedorTabla etiqueta="Cambios sincronizados" maxHeight={ALTO_TABLA}>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Docente</Table.HeaderCell>
            <Table.HeaderCell>Materia</Table.HeaderCell>
            <Table.HeaderCell>Novedad</Table.HeaderCell>
            <Table.HeaderCell>Quedó</Table.HeaderCell>
            <Table.HeaderCell>Pedido</Table.HeaderCell>
            <Table.HeaderCell>Fecha</Table.HeaderCell>
            <Table.HeaderCell>Estado</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {filas.map(({ docente, pedido }) => (
            <Table.Row key={pedido.id}>
              <Table.Cell>
                <Docente docente={docente} />
              </Table.Cell>
              <Table.Cell>
                {pedido.materia}
                <span className="conc-nota-tabla">{pedido.carrera}</span>
              </Table.Cell>
              <Table.Cell>
                <NovedadChip novedad={pedido.novedad} />
              </Table.Cell>
              <Table.Cell>
                {pedido.novedad === "Baja"
                  ? "Dada de baja"
                  : pedido.queda
                    ? describirValores(pedido.queda)
                    : ""}
              </Table.Cell>
              <Table.Cell className="conc-numero">{pedido.numero}</Table.Cell>
              <Table.Cell>
                <span className="conc-fecha">
                  {pedido.sincronizadoEn ? fechaHora.format(new Date(pedido.sincronizadoEn)) : ""}
                  {pedido.sincronizadoPor ? ` por ${pedido.sincronizadoPor}` : ""}
                </span>
              </Table.Cell>
              <Table.Cell>
                <span title="Ya impactó en la base: no se puede editar">
                  <StatusBadge kind="aprobado" label="Sincronizado" />
                </span>
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </ContenedorTabla>
    </Table>
  );
}
