/**
 * Destino validado del botón "Volver". Quien abre un detalle pasa `state.volverA` (ruta + query de
 * la lista de origen); acá se acepta sólo si es una ruta relativa de la lista permitida de la
 * entidad y el usuario puede abrirla. Si no, se usa un destino fijo. Nunca se navega con `-1`.
 */
export type TipoVolver = "solicitud" | "pedido" | "tarea" | "proyecto" | "persona" | "rol";

interface DestinoPermitido {
  /** Ruta exacta, o con segmentos `:id` que aceptan cualquier valor. */
  patron: string;
  etiqueta: string;
  /** Con uno alcanza; vacío = sin restricción. */
  permisos: readonly string[];
}

interface ConfiguracionVolver {
  destinos: readonly DestinoPermitido[];
  respaldo: (permisos: readonly string[]) => string;
}

const PERSONAS = {
  patron: "/personas",
  etiqueta: "Personas",
  permisos: ["personas.ver", "personas.ver_ambito"],
};
const TAREAS = { patron: "/tareas", etiqueta: "Tareas", permisos: ["tareas.ver"] };

const CONFIGURACION: Record<TipoVolver, ConfiguracionVolver> = {
  solicitud: {
    destinos: [
      {
        patron: "/aulas",
        etiqueta: "Reserva de aulas",
        permisos: ["aulas.solicitar", "aulas.aprobar"],
      },
    ],
    respaldo: () => "/aulas",
  },
  pedido: {
    destinos: [
      {
        patron: "/designaciones/revision",
        etiqueta: "Revisión",
        permisos: ["designaciones.revisar"],
      },
      {
        patron: "/designaciones/conciliacion",
        etiqueta: "Conciliación",
        permisos: ["designaciones.conciliar"],
      },
      {
        patron: "/designaciones/mis-pedidos",
        etiqueta: "Mis pedidos",
        permisos: ["designaciones.gestionar"],
      },
    ],
    // Un link directo no debe llevar a una pantalla que el usuario no puede ver.
    respaldo: (permisos) =>
      permisos.includes("designaciones.revisar")
        ? "/designaciones/revision"
        : "/designaciones/mis-pedidos",
  },
  tarea: {
    destinos: [
      TAREAS,
      { patron: "/tareas/proyectos/:id", etiqueta: "Proyecto", permisos: ["tareas.ver"] },
    ],
    respaldo: () => "/tareas",
  },
  proyecto: {
    destinos: [
      { patron: "/tareas/proyectos", etiqueta: "Proyectos", permisos: ["tareas.gestionar"] },
      TAREAS,
    ],
    respaldo: () => "/tareas/proyectos",
  },
  persona: { destinos: [PERSONAS], respaldo: () => "/personas" },
  rol: {
    destinos: [{ patron: "/roles", etiqueta: "Roles", permisos: ["roles.ver"] }],
    respaldo: () => "/roles",
  },
};

export interface DestinoVolver {
  /** Ruta con su query, lista para un `Link`. */
  ruta: string;
  etiqueta: string;
  /** "Volver a <etiqueta>". */
  texto: string;
  /** `true` si salió del estado de navegación (y no del destino fijo). */
  desdeEstado: boolean;
}

/** Sólo `/a-b/c` con un query opcional de caracteres inocuos: sin esquema, `//`, `\` ni hash. */
const FORMA_VALIDA = /^\/[a-z0-9\-/]*(\?[\w%.\-:,=&~+]*)?$/i;
const LONGITUD_MAXIMA = 500;

function coincide(patron: string, ruta: string): boolean {
  const a = patron.split("/");
  const b = ruta.split("/");
  return (
    a.length === b.length && a.every((parte, i) => (parte === ":id" ? b[i] !== "" : parte === b[i]))
  );
}

function destinoDe(config: ConfiguracionVolver, path: string, permisos: readonly string[]) {
  return config.destinos.find(
    (d) =>
      coincide(d.patron, path) &&
      (d.permisos.length === 0 || d.permisos.some((p) => permisos.includes(p))),
  );
}

export function resolverVolver(
  tipo: TipoVolver,
  candidata: unknown,
  permisos: readonly string[],
): DestinoVolver {
  const config = CONFIGURACION[tipo];
  if (
    typeof candidata === "string" &&
    candidata.length <= LONGITUD_MAXIMA &&
    !candidata.includes("//") &&
    FORMA_VALIDA.test(candidata)
  ) {
    const path = candidata.split("?")[0].replace(/(.)\/$/, "$1");
    const destino = destinoDe(config, path, permisos);
    if (destino) {
      return {
        ruta: candidata,
        etiqueta: destino.etiqueta,
        texto: `Volver a ${destino.etiqueta}`,
        desdeEstado: true,
      };
    }
  }
  const fijo = config.respaldo(permisos);
  const etiqueta = config.destinos.find((d) => coincide(d.patron, fijo))?.etiqueta ?? "el inicio";
  return { ruta: fijo, etiqueta, texto: `Volver a ${etiqueta}`, desdeEstado: false };
}

/** Estado de navegación que se pasa al abrir un detalle desde la ubicación actual. */
export function estadoVolver(ubicacion: { pathname: string; search: string }) {
  return { volverA: ubicacion.pathname + ubicacion.search };
}

/** Estado que propaga un destino ya resuelto (detalle → hijo, detalle → edición). */
export function propagarVolver(destino: DestinoVolver) {
  return { volverA: destino.ruta };
}

/** Marca que la navegación es un "Volver": el shell restaura el scroll de esa lista. */
export const ESTADO_ES_VOLVER = "esVolver";

/** Miga de la lista de origen, con el mismo destino que el botón "Volver". */
export function migaDeVolver(destino: DestinoVolver) {
  return { label: destino.etiqueta, href: destino.ruta };
}
