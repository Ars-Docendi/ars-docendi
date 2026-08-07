// ============================================================
// API mock de pedidos de designación — EL SEAM DEL BACKEND.
// Cada función es async (Promise + latencia simulada), opera sobre el
// store mock y delega las transiciones de estado a `maquinaEstados.ts`.
// Cuando llegue el backend, se reemplaza el CUERPO de cada función por
// llamadas `apiClient.get/post(...)` MANTENIENDO LA FIRMA, y los
// hooks/componentes no cambian. Por eso los `// TODO(backend)` viven
// SOLO acá (regla del §7 del plan): si aparece uno fuera de `api/`, la
// lógica se filtró del seam.
// ============================================================
import type {
  ActorContexto,
  DatosEditablesPedido,
  EventoHistorial,
  PedidoDesignacion,
} from "../types";
import { ErrorDominioPedido, actorAlcanzaAmbito, aplicarAccion } from "./maquinaEstados";
import { PERIODO_ABIERTO_ID } from "./pedidosSeed";
import * as store from "./pedidosStore";

/** Latencia simulada para que la UI ejercite los estados de carga. */
function demora(ms = 250): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

function requerirPedido(id: string): PedidoDesignacion {
  const pedido = store.buscar(id);
  if (!pedido) {
    throw new Error(`No se encontró el pedido con id "${id}".`);
  }
  return pedido;
}

/** Un pedido pertenece a "Mis pedidos" del JC si es de alguna de sus cátedras. */
function esDelJefeDeCatedra(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  if (actor.catedras?.length) {
    return actor.catedras.includes(pedido.catedra);
  }
  return pedido.carrera === actor.carrera;
}

// TODO(backend): el número de trámite lo asigna el backend al persistir (secuencia por año).
//   Mock actual: calcula el siguiente "N°-AAAA-NNNN" a partir del máximo en el store.
function siguienteNumeroTramite(): string {
  const anio = new Date().getFullYear();
  const maximo = store.leerTodos().reduce((max, p) => {
    const n = Number(p.numero?.split("-").at(-1));
    return Number.isFinite(n) && n > max ? n : max;
  }, 123);
  return `N°-${anio}-${String(maximo + 1).padStart(4, "0")}`;
}

// TODO(backend): GET /api/designaciones/pedidos?ambito=catedra con autorización rol+ámbito (RNF-1) — SCRUM-7.
//   Mock actual: lee el store en localStorage y filtra los pedidos de la cátedra del JC. Mantener la firma.
export async function listarMisPedidos(actor: ActorContexto): Promise<PedidoDesignacion[]> {
  await demora();
  return store.leerTodos().filter((pedido) => esDelJefeDeCatedra(pedido, actor));
}

// TODO(backend): GET /api/designaciones/pedidos/:id (SCRUM-7).
//   Mock actual: busca en el store. Mantener la firma.
export async function obtenerPedido(id: string): Promise<PedidoDesignacion> {
  await demora();
  return requerirPedido(id);
}

// TODO(backend): POST /api/designaciones/pedidos (SCRUM-7).
//   Mock actual: crea un pedido en borrador en el store con un evento "crear". Mantener la firma.
export async function crearPedido(
  datos: DatosEditablesPedido,
  actor: ActorContexto,
): Promise<PedidoDesignacion> {
  await demora();
  const eventoCrear: EventoHistorial = {
    id: crypto.randomUUID(),
    accion: "crear",
    porRol: actor.rol,
    porNombre: actor.nombre,
    etapa: "borrador",
    fecha: new Date().toISOString(),
  };
  const nuevo: PedidoDesignacion = {
    id: crypto.randomUUID(),
    numero: siguienteNumeroTramite(),
    periodoId: PERIODO_ABIERTO_ID,
    // La cátedra viaja en los datos del form, no se deduce del actor: con varias
    // cátedras a cargo, cuál es la del pedido sólo lo sabe la pantalla.
    catedra: datos.catedra,
    carrera: actor.carrera ?? "",
    docente: datos.docente,
    horas: datos.horas,
    cargoActual: datos.cargoActual,
    dedicacionActual: datos.dedicacionActual,
    novedad: datos.novedad,
    cargoSolicitado: datos.cargoSolicitado,
    dedicacionSolicitada: datos.dedicacionSolicitada,
    justificacion: datos.justificacion,
    tipoBaja: datos.tipoBaja,
    tipoBajaDetalle: datos.tipoBajaDetalle,
    horasExternas: datos.horasExternas,
    horasInvestigacion: datos.horasInvestigacion,
    // TODO(backend): persistir adjuntos en File Storage y guardar URL (RNF-4) — SCRUM-7. Hoy solo metadata mock.
    adjuntos: datos.adjuntos,
    estado: "borrador",
    prioritario: false,
    historial: [eventoCrear],
  };
  return store.guardar(nuevo);
}

// TODO(backend): PUT /api/designaciones/pedidos/:id (SCRUM-7).
//   Mock actual: valida el guard de edición con la máquina de estados y guarda. Mantener la firma.
export async function editarPedido(
  id: string,
  datos: DatosEditablesPedido,
  actor: ActorContexto,
): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "editar", datos }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/designaciones/pedidos/:id/enviar (SCRUM-7).
//   Mock actual: transiciona borrador → en_revision_coordinador vía la máquina de estados. Mantener la firma.
export async function enviarPedido(id: string, actor: ActorContexto): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "enviar" }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): DELETE /api/designaciones/pedidos/:id (mis-pedidos-simplificado).
//   Mock actual: solo borradores propios del JC; borra del store (no es una transición de estado).
export async function eliminarPedido(id: string, actor: ActorContexto): Promise<void> {
  await demora();
  const actual = requerirPedido(id);
  if (actual.estado !== "borrador") {
    throw new ErrorDominioPedido(
      `Solo se puede eliminar un pedido en borrador (estado actual: "${actual.estado}").`,
    );
  }
  if (actor.rol !== "Jefe de Cátedra") {
    throw new ErrorDominioPedido("Solo el Jefe de Cátedra puede eliminar el pedido.");
  }
  store.eliminar(id);
}

// ============================================================
// SCRUM-8 — Circuito de revisión. Lectura por ámbito + acciones del revisor.
// ============================================================

// TODO(backend): GET /api/designaciones/pedidos?ambito=depto con autorización rol+ámbito (RNF-1) — SCRUM-8.
//   Mock actual: lee el store y filtra por el ámbito del revisor (Coordinador→su carrera;
//   Secretaría/Decanato/Administración→todo el depto). Mantener la firma.
export async function listarPedidosPorAmbito(actor: ActorContexto): Promise<PedidoDesignacion[]> {
  await demora();
  return store.leerTodos().filter((pedido) => actorAlcanzaAmbito(pedido, actor));
}

// TODO(backend): POST /api/designaciones/pedidos/:id/aceptar (SCRUM-8).
//   Mock actual: avanza la cadena (coordinador→secretaría→decanato→en_lote) vía la máquina de estados. Mantener la firma.
export async function aceptarPedido(
  id: string,
  actor: ActorContexto,
  comentario?: string,
): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "aceptar", comentario }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/designaciones/pedidos/:id/rechazar (SCRUM-8).
//   Mock actual: transiciona en_revision_* → rechazado (terminal) con justificativo. Mantener la firma.
export async function rechazarPedido(
  id: string,
  actor: ActorContexto,
  comentario: string,
): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "rechazar", comentario }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/designaciones/pedidos/:id/devolver (SCRUM-8).
//   Mock actual: retrocede un nivel (→ devuelto) fijando propietario y etapa de retorno. Mantener la firma.
export async function devolverPedido(
  id: string,
  actor: ActorContexto,
  comentario: string,
): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "devolver", comentario }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/designaciones/pedidos/:id/reenviar (SCRUM-8).
//   Mock actual: un devuelto del propietario vuelve a su etapa de retorno. Mantener la firma.
export async function reenviarPedido(id: string, actor: ActorContexto): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "reenviar" }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/designaciones/pedidos/:id/priorizar (SCRUM-8).
//   Mock actual: marca prioritario=true (sin cambiar el estado) con justificativo. Mantener la firma.
export async function priorizarPedido(
  id: string,
  actor: ActorContexto,
  comentario: string,
): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "priorizar", comentario }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/designaciones/pedidos/:id/despriorizar (tema E).
//   Mock actual: marca prioritario=false (sin cambiar el estado), sin exigir comentario. Mantener la firma.
export async function despriorizarPedido(
  id: string,
  actor: ActorContexto,
  comentario?: string,
): Promise<PedidoDesignacion> {
  await demora();
  const actual = requerirPedido(id);
  const siguiente = aplicarAccion(actual, { tipo: "despriorizar", comentario }, actor);
  return store.guardar(siguiente);
}
