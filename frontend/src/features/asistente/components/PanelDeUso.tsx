import { useState } from "react";
import { Input, Table, Tabs } from "@ars-docendi/ui";
import type { TabItem } from "@ars-docendi/ui";

import { CeldaDeAcceso } from "./CeldaDeAcceso";
import { EditorDeCupoEnFila } from "./EditorDeCupoEnFila";
import { IconoSearch } from "../../../shared/ui/iconos";
import { formatearEntero, formatearLatenciaMs, formatearUsd } from "../utils/formatoDeUso";
import { subtituloDeRoles } from "../utils/etiquetasDeRol";
import type { CupoDeRolPersistido, UsoAgregado, UsoDelAsistente } from "../types";

type Pestana = "usuarios" | "roles";
type Metrica = "turnos" | "costo" | "tokens" | "latencia";

interface PanelDeUsoProps {
  uso: UsoDelAsistente | undefined;
  cargando: boolean;
  /** El cupo default de cada rol, tal como está persistido (tarea 12.8). */
  cuposPorRol: CupoDeRolPersistido[];
  onGuardarCupoDeRol: (rol: string, cupo: number) => Promise<void>;
  onGuardarCupoDeUsuario: (actorId: string, cupo: number) => Promise<void>;
  /** Vuelve el cupo de un usuario al de su rol (asistente-acceso-granular). */
  onRestablecerCupoDeUsuario: (actorId: string) => Promise<void>;
  onCambiarAccesoDeRol: (rol: string, habilitado: boolean) => Promise<void>;
  /** `false` le quita el acceso; `true` sólo restablece el del rol. */
  onCambiarAccesoDeUsuario: (actorId: string, habilitado: boolean) => Promise<void>;
  /** Texto para la región viva compartida de la página (tasks.md 13.2). */
  onGuardado: (mensaje: string) => void;
  /**
   * Pestaña controlada desde afuera: «Exportar CSV» en el encabezado de la
   * pestaña (`PanelAdministracionAsistente`) necesita saber cuál es la vista
   * activa para exportar exactamente lo que se está viendo. Si no se pasa,
   * el panel maneja su propio estado — mismo patrón controlado/no controlado
   * que un `<input>`, para no forzar este prop en cada test que monta el
   * panel de forma aislada.
   */
  pestana?: Pestana;
  onCambiarPestana?: (pestana: Pestana) => void;
}

/** El encabezado de la columna de detalle: el nombre completo de la métrica. */
const ETIQUETAS_METRICA: Record<Metrica, string> = {
  turnos: "Sesiones",
  costo: "Costo estimado",
  tokens: "Tokens",
  latencia: "Latencia",
};

/** El selector de métrica usa etiquetas cortas (mismo canvas: «Costo», no «Costo estimado»). */
const ETIQUETAS_METRICA_CORTAS: Record<Metrica, string> = {
  turnos: "Sesiones",
  costo: "Costo",
  tokens: "Tokens",
  latencia: "Latencia",
};

const METRICAS: Metrica[] = ["turnos", "costo", "tokens", "latencia"];

/** El total de tokens de una fila: entrada + salida + caché, sumados una sola vez acá. */
function tokensTotales(fila: UsoAgregado): number {
  return fila.tokensDeEntrada + fila.tokensDeSalida + fila.tokensDeCache;
}

/** El valor numérico de la métrica elegida — lo que ordena la tabla y dimensiona la barra. */
function valorDeMetrica(fila: UsoAgregado, metrica: Metrica): number {
  if (metrica === "turnos") return fila.turnos;
  if (metrica === "costo") return fila.costoEstimado;
  if (metrica === "tokens") return tokensTotales(fila);
  return fila.latenciaP95Ms;
}

/** El texto principal de la celda de métrica: siempre con su unidad, nunca un número pelado. */
function textoDeMetrica(fila: UsoAgregado, metrica: Metrica): string {
  if (metrica === "turnos") return `${formatearEntero(fila.turnos)} sesiones`;
  if (metrica === "costo") return `${formatearUsd(fila.costoEstimado)} (estimado)`;
  if (metrica === "tokens") return `${formatearEntero(tokensTotales(fila))} tokens`;
  return formatearLatenciaMs(fila.latenciaP95Ms);
}

/** La nota chica debajo del valor: sin uso, promedio de latencia, o turnos sin precio vigente. */
function notaDeMetrica(fila: UsoAgregado, metrica: Metrica): string {
  if (fila.turnos === 0) return "Sin uso";
  if (metrica === "latencia") return `promedio ${formatearLatenciaMs(fila.latenciaPromedioMs)}`;
  if (metrica === "costo" && fila.turnosSinPrecio > 0) {
    return `${fila.turnosSinPrecio} ${fila.turnosSinPrecio === 1 ? "turno" : "turnos"} sin precio`;
  }
  return "";
}

/**
 * El detalle del panel de uso —por usuario y por rol— del rediseño «Uso del
 * asistente» (sistema-seccion-unificada), en su pasada de fidelidad con el
 * canvas de Claude Design: UNA sola tarjeta con las pestañas y el selector de
 * métrica (Sesiones/Costo/Tokens/Latencia) arriba, y una columna de detalle
 * que muestra la métrica elegida con una barra proporcional al máximo de la
 * vista — reemplaza las seis columnas siempre visibles y el ordenamiento
 * manual por encabezado del primer rediseño. Las filas se ordenan solas,
 * descendente por la métrica elegida (mismo criterio que el canvas).
 *
 * El cupo diario sigue editándose DIRECTO EN LA FILA (`EditorDeCupoEnFila`),
 * y desde asistente-acceso-granular cada fila tiene además su columna
 * «Acceso» (`CeldaDeAcceso`): el acceso y el cupo se heredan del rol; a un
 * usuario se le puede quitar el acceso, no darlo, y tanto la revocación como
 * un cupo propio se restablecen al del rol desde la misma fila.
 * El agregado «Organización» no vive acá: está en `KpisDeUso`, arriba.
 *
 * EL COSTO SIEMPRE LLEVA «(estimado)» cuando la métrica elegida es Costo, y
 * un turno sin precio vigente NUNCA se cuenta como costo cero: se ve aparte
 * (asistente-panel-de-uso).
 */
export function PanelDeUso({
  uso,
  cargando,
  cuposPorRol,
  onGuardarCupoDeRol,
  onGuardarCupoDeUsuario,
  onRestablecerCupoDeUsuario,
  onCambiarAccesoDeRol,
  onCambiarAccesoDeUsuario,
  onGuardado,
  pestana: pestanaControlada,
  onCambiarPestana,
}: PanelDeUsoProps) {
  const [pestanaPropia, setPestanaPropia] = useState<Pestana>("usuarios");
  const pestana = pestanaControlada ?? pestanaPropia;
  const [metrica, setMetrica] = useState<Metrica>("turnos");
  const [busqueda, setBusqueda] = useState("");

  function cambiarPestana(siguiente: Pestana) {
    setPestanaPropia(siguiente);
    setBusqueda("");
    onCambiarPestana?.(siguiente);
  }

  if (cargando) {
    return (
      <div className="adoc-asistente-admin-panel-uso-tarjeta adoc-asistente-admin-panel-vacio">
        <p aria-live="polite">Cargando el panel de uso…</p>
      </div>
    );
  }

  if (!uso) {
    return (
      <div className="adoc-asistente-admin-panel-uso-tarjeta adoc-asistente-admin-panel-vacio">
        <p>No se pudo cargar el panel de uso todavía.</p>
      </div>
    );
  }

  const esUsuarios = pestana === "usuarios";
  const filas = esUsuarios ? uso.porUsuario : uso.porRol;
  const q = busqueda.trim().toLowerCase();
  const filtradas = q
    ? filas.filter((f) => (f.nombreParaMostrar ?? f.clave).toLowerCase().includes(q))
    : filas;
  const ordenadas = [...filtradas].sort((a, b) => {
    const diferencia = valorDeMetrica(b, metrica) - valorDeMetrica(a, metrica);
    if (diferencia !== 0) return diferencia;
    return (a.nombreParaMostrar ?? a.clave).localeCompare(b.nombreParaMostrar ?? b.clave, "es");
  });
  const maxValor = Math.max(0.0001, ...ordenadas.map((f) => valorDeMetrica(f, metrica)));
  const guardarCupo = esUsuarios ? onGuardarCupoDeUsuario : onGuardarCupoDeRol;

  // El default de cada rol siempre está persistido (seed de 006). El cupo de
  // una fila de USUARIO ya no se arma acá con un mapa aparte: viene resuelto
  // desde el backend en la propia fila (`cupoEfectivo`/`origenDeCupo`, tarea
  // «rol y cupo efectivo por usuario» de sistema-seccion-unificada) — la
  // misma regla que aplicaría la ejecución real del turno.
  const cuposPorRolMap = new Map(cuposPorRol.map((c) => [c.rol, c.cupoDiarioTurnos]));
  // Un rol sin fila persistida cuenta como habilitado — misma regla que el
  // backend (`ReglaDeAccesoEfectivo`).
  const accesoPorRolMap = new Map(cuposPorRol.map((c) => [c.rol, c.accesoHabilitado]));

  const tabs: TabItem[] = [
    { id: "usuarios", label: "Por usuario", count: uso.porUsuario.length },
    { id: "roles", label: "Por rol", count: uso.porRol.length },
  ];

  return (
    <div className="adoc-asistente-admin-panel-uso-tarjeta">
      <div className="adoc-asistente-admin-panel-uso-encabezado">
        <Tabs
          items={tabs}
          value={pestana}
          onChange={(id) => cambiarPestana(id as Pestana)}
          aria-label="Detalle de uso"
        />
        <div role="radiogroup" aria-label="Métrica" className="adoc-asistente-admin-metrica-switch">
          {METRICAS.map((m) => (
            <button
              key={m}
              type="button"
              role="radio"
              aria-checked={metrica === m}
              className="adoc-asistente-admin-metrica-boton"
              onClick={() => setMetrica(m)}
            >
              {ETIQUETAS_METRICA_CORTAS[m]}
            </button>
          ))}
        </div>
      </div>

      <div className="adoc-asistente-admin-uso-buscador">
        <span className="adoc-asistente-admin-uso-buscador-icono" aria-hidden="true">
          <IconoSearch />
        </span>
        <Input
          aria-label="Buscar usuario o rol"
          placeholder="Buscar usuario o rol"
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
        />
      </div>

      {ordenadas.length === 0 ? (
        <p className="adoc-asistente-admin-panel-vacio-texto">
          No hay uso registrado en este período.
        </p>
      ) : (
        <Table className="adoc-asistente-admin-tabla-uso">
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>{esUsuarios ? "Usuario" : "Rol"}</Table.HeaderCell>
                <Table.HeaderCell>{ETIQUETAS_METRICA[metrica]}</Table.HeaderCell>
                <Table.HeaderCell>Acceso</Table.HeaderCell>
                <Table.HeaderCell>Cupo diario</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {ordenadas.map((fila) => {
                const valor = valorDeMetrica(fila, metrica);
                const anchoBarra = fila.turnos === 0 ? 0 : Math.max(2, (valor / maxValor) * 100);
                const subtitulo = esUsuarios ? subtituloDeRoles(fila.codigosDeRol) : undefined;
                const nombre = fila.nombreParaMostrar ?? fila.clave;
                const acceso = esUsuarios
                  ? (fila.accesoEfectivo ?? true)
                  : (accesoPorRolMap.get(fila.clave) ?? true);
                return (
                  <Table.Row key={fila.clave}>
                    <Table.Cell>
                      <div className="adoc-asistente-admin-uso-nombre-celda">
                        <span>{fila.nombreParaMostrar ?? fila.clave}</span>
                        {subtitulo && (
                          <span className="adoc-asistente-admin-uso-nombre-subtitulo">
                            {subtitulo}
                          </span>
                        )}
                      </div>
                    </Table.Cell>
                    <Table.Cell>
                      <div className="adoc-asistente-admin-metrica-celda">
                        <span
                          className="adoc-asistente-admin-metrica-barra"
                          style={{ width: `${anchoBarra}%` }}
                          aria-hidden="true"
                        />
                        <span className="adoc-asistente-admin-metrica-texto">
                          <span>{textoDeMetrica(fila, metrica)}</span>
                          {notaDeMetrica(fila, metrica) && (
                            <span className="adoc-asistente-admin-metrica-nota">
                              {notaDeMetrica(fila, metrica)}
                            </span>
                          )}
                        </span>
                      </div>
                    </Table.Cell>
                    <Table.Cell>
                      <CeldaDeAcceso
                        nombre={nombre}
                        acceso={acceso}
                        origen={esUsuarios ? (fila.origenDeAcceso ?? "rol") : undefined}
                        onCambiar={async (habilitado) => {
                          if (esUsuarios) {
                            await onCambiarAccesoDeUsuario(fila.clave, habilitado);
                            onGuardado(
                              habilitado
                                ? `Acceso de ${nombre}: restablecido al del rol.`
                                : `Acceso de ${nombre}: quitado.`,
                            );
                          } else {
                            await onCambiarAccesoDeRol(fila.clave, habilitado);
                            onGuardado(
                              `Acceso del rol ${nombre}: ${habilitado ? "habilitado" : "quitado"}.`,
                            );
                          }
                        }}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      <EditorDeCupoEnFila
                        nombre={nombre}
                        deshabilitado={!acceso}
                        onRestablecer={
                          esUsuarios
                            ? async () => {
                                await onRestablecerCupoDeUsuario(fila.clave);
                                onGuardado(`Cupo diario de ${nombre}: restablecido al del rol.`);
                              }
                            : undefined
                        }
                        cupoConocido={
                          esUsuarios
                            ? (fila.cupoEfectivo ?? undefined)
                            : cuposPorRolMap.get(fila.clave)
                        }
                        origen={esUsuarios ? (fila.origenDeCupo ?? undefined) : "rol"}
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
                );
              })}
            </Table.Body>
          </Table.Root>
        </Table>
      )}
      <p className="adoc-asistente-admin-panel-uso-pie">
        Acceso y cupo se heredan del rol; a un usuario se le puede quitar el acceso, no darlo. El
        cupo se reinicia a las 00 h.
      </p>
    </div>
  );
}
