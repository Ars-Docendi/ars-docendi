import { useState } from "react";
import { Input, Table, Tabs } from "@ars-docendi/ui";
import type { TabItem } from "@ars-docendi/ui";

import { EditorDeCupoEnFila } from "./EditorDeCupoEnFila";
import { formatearUsd } from "../utils/formatoDeUso";
import type {
  CupoDeRolPersistido,
  OverrideDeUsuarioPersistido,
  UsoAgregado,
  UsoDelAsistente,
} from "../types";

type Pestana = "usuarios" | "roles";
type CampoOrdenable = "turnos" | "costoEstimado" | "latenciaP95Ms";
interface OrdenDeUso {
  campo: CampoOrdenable;
  direccion: "ascendente" | "descendente";
}

interface PanelDeUsoProps {
  uso: UsoDelAsistente | undefined;
  cargando: boolean;
  /** El cupo default de cada rol, tal como está persistido (tarea 12.8). */
  cuposPorRol: CupoDeRolPersistido[];
  /** Cada override de usuario vigente, tal como está persistido (tarea 12.8). */
  overridesPorUsuario: OverrideDeUsuarioPersistido[];
  onGuardarCupoDeRol: (rol: string, cupo: number) => Promise<void>;
  onGuardarCupoDeUsuario: (actorId: string, cupo: number) => Promise<void>;
  /** Texto para la región viva compartida de la página (tasks.md 13.2). */
  onGuardado: (mensaje: string) => void;
}

const ENCABEZADOS: { campo: CampoOrdenable; etiqueta: string }[] = [
  { campo: "turnos", etiqueta: "Turnos" },
  { campo: "costoEstimado", etiqueta: "Costo estimado" },
  { campo: "latenciaP95Ms", etiqueta: "Latencia" },
];

/**
 * El detalle del panel de uso —por usuario y por rol— del rediseño «Uso del
 * asistente» (sistema-seccion-unificada): dos pestañas con buscador y
 * columnas ordenables en vez de tres tablas siempre apiladas, y el cupo
 * diario editable DIRECTO EN LA FILA (`EditorDeCupoEnFila`) en vez de un
 * formulario aparte que pedía escribir un código de rol o un UUID.
 *
 * El agregado «Organización» ya NO vive acá: pasó a `KpisDeUso`, arriba.
 *
 * EL COSTO SIEMPRE LLEVA «(estimado)» AL LADO, nunca sólo en un título de
 * sección, y un turno sin precio vigente NUNCA se cuenta como costo cero:
 * se ve aparte, con su propio texto (asistente-panel-de-uso).
 */
export function PanelDeUso({
  uso,
  cargando,
  cuposPorRol,
  overridesPorUsuario,
  onGuardarCupoDeRol,
  onGuardarCupoDeUsuario,
  onGuardado,
}: PanelDeUsoProps) {
  const [pestana, setPestana] = useState<Pestana>("usuarios");
  const [busqueda, setBusqueda] = useState("");
  const [orden, setOrden] = useState<OrdenDeUso | null>(null);

  if (cargando) {
    return <p aria-live="polite">Cargando el panel de uso…</p>;
  }

  if (!uso) {
    return <p>No se pudo cargar el panel de uso todavía.</p>;
  }

  const esUsuarios = pestana === "usuarios";
  const filas = esUsuarios ? uso.porUsuario : uso.porRol;
  const q = busqueda.trim().toLowerCase();
  const filtradas = q
    ? filas.filter((f) => (f.nombreParaMostrar ?? f.clave).toLowerCase().includes(q))
    : filas;
  const ordenadas = ordenarUso(filtradas, orden);
  const guardarCupo = esUsuarios ? onGuardarCupoDeUsuario : onGuardarCupoDeRol;

  // El default de cada rol siempre está persistido (seed de 006); el override
  // de un usuario sólo existe si un admin ya lo fijó — de ahí que el mapa de
  // roles use el valor directo y el de usuarios distinga "no hay override"
  // (tarea 12.8 de sistema-seccion-unificada).
  const cuposPorRolMap = new Map(cuposPorRol.map((c) => [c.rol, c.cupoDiarioTurnos]));
  const overridesPorUsuarioMap = new Map(
    overridesPorUsuario.map((o) => [o.actorId, o.cupoDiarioTurnos]),
  );

  const tabs: TabItem[] = [
    { id: "usuarios", label: "Por usuario", count: uso.porUsuario.length },
    { id: "roles", label: "Por rol", count: uso.porRol.length },
  ];

  function alOrdenar(campo: CampoOrdenable) {
    setOrden((actual) =>
      actual?.campo === campo
        ? { campo, direccion: actual.direccion === "descendente" ? "ascendente" : "descendente" }
        : { campo, direccion: "descendente" },
    );
  }

  return (
    <div className="adoc-asistente-admin-panel-uso">
      <Tabs
        items={tabs}
        value={pestana}
        onChange={(id) => {
          setPestana(id as Pestana);
          setBusqueda("");
        }}
        aria-label="Detalle de uso"
      />

      <div className="adoc-asistente-admin-uso-buscador">
        <Input
          aria-label="Buscar usuario o rol"
          placeholder="Buscar usuario o rol"
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
        />
      </div>

      {ordenadas.length === 0 ? (
        <p>No hay uso registrado en este período.</p>
      ) : (
        <Table className="adoc-asistente-admin-tabla-uso">
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>{esUsuarios ? "Usuario" : "Rol"}</Table.HeaderCell>
                {ENCABEZADOS.map((h) => (
                  <EncabezadoOrdenableDeUso
                    key={h.campo}
                    etiqueta={h.etiqueta}
                    campo={h.campo}
                    orden={orden}
                    onOrdenar={alOrdenar}
                  />
                ))}
                <Table.HeaderCell>Llamadas al modelo</Table.HeaderCell>
                <Table.HeaderCell>Tokens entrada/salida/caché</Table.HeaderCell>
                <Table.HeaderCell>Cupo diario</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {ordenadas.map((fila) => (
                <Table.Row key={fila.clave}>
                  <Table.Cell>{fila.nombreParaMostrar ?? fila.clave}</Table.Cell>
                  <Table.Cell numeric>{fila.turnos}</Table.Cell>
                  <Table.Cell numeric>
                    <span>{formatearUsd(fila.costoEstimado)} (estimado)</span>
                    {fila.turnosSinPrecio > 0 && (
                      <span className="adoc-asistente-admin-sin-precio">
                        {fila.turnosSinPrecio} {fila.turnosSinPrecio === 1 ? "turno" : "turnos"} sin
                        precio
                      </span>
                    )}
                  </Table.Cell>
                  <Table.Cell numeric>
                    {Math.round(fila.latenciaPromedioMs)} ms / {Math.round(fila.latenciaP95Ms)} ms
                  </Table.Cell>
                  <Table.Cell numeric>{fila.llamadasAlModelo}</Table.Cell>
                  <Table.Cell numeric>
                    {fila.tokensDeEntrada} / {fila.tokensDeSalida} / {fila.tokensDeCache}
                  </Table.Cell>
                  <Table.Cell>
                    <EditorDeCupoEnFila
                      nombre={fila.nombreParaMostrar ?? fila.clave}
                      cupoConocido={
                        esUsuarios
                          ? overridesPorUsuarioMap.get(fila.clave)
                          : cuposPorRolMap.get(fila.clave)
                      }
                      origen={
                        esUsuarios
                          ? overridesPorUsuarioMap.has(fila.clave)
                            ? "override"
                            : undefined
                          : "rol"
                      }
                      onGuardar={(cupo) => guardarCupo(fila.clave, cupo)}
                      onGuardado={(cupo) => {
                        // El valor persistido lo vuelve a traer la próxima
                        // vez que se invaliden las queries del panel (el
                        // padre lo hace tras un guardado exitoso) — ya NO
                        // hace falta un estado local "guardado en esta
                        // sesión" (tarea 12.8 de sistema-seccion-unificada).
                        onGuardado(
                          `Cupo diario de ${fila.nombreParaMostrar ?? fila.clave}: ${
                            cupo === 0 ? "sin tope" : `${cupo} turnos`
                          }.`,
                        );
                      }}
                    />
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table.Root>
        </Table>
      )}
    </div>
  );
}

function ordenarUso(filas: UsoAgregado[], orden: OrdenDeUso | null): UsoAgregado[] {
  if (!orden) return filas;
  const signo = orden.direccion === "ascendente" ? 1 : -1;
  return [...filas].sort((a, b) => signo * (a[orden.campo] - b[orden.campo]));
}

interface EncabezadoOrdenableDeUsoProps {
  etiqueta: string;
  campo: CampoOrdenable;
  orden: OrdenDeUso | null;
  onOrdenar: (campo: CampoOrdenable) => void;
}

function EncabezadoOrdenableDeUso({
  etiqueta,
  campo,
  orden,
  onOrdenar,
}: EncabezadoOrdenableDeUsoProps) {
  const activo = orden?.campo === campo;
  return (
    <Table.HeaderCell
      className="adoc-asistente-th-orden"
      aria-sort={
        activo ? (orden!.direccion === "ascendente" ? "ascending" : "descending") : undefined
      }
    >
      <button
        type="button"
        className="adoc-asistente-orden-boton"
        title={`Ordenar por «${etiqueta}»`}
        onClick={() => onOrdenar(campo)}
      >
        <span>{etiqueta}</span>
        <span className="adoc-sr"> ordenar por esta columna</span>
      </button>
    </Table.HeaderCell>
  );
}
