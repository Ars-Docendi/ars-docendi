// ============================================================
// Shell icon set — inline SVG, currentColor.
// The @ars-docendi/ui icon set is internal and not exported, so
// the shell carries its own small set (same approach LoginPage
// took with MsGlyph). Sized by the consuming CSS (width/height).
// ============================================================
import type { ReactNode } from "react";

/* Topbar + chrome */
export const searchIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <circle cx="8" cy="8" r="5" />
    <path d="M11.5 11.5l3 3" />
  </svg>
);

export const bellIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M4 8a5 5 0 0110 0v3l2 2H2l2-2V8z" />
    <path d="M7 15a2 2 0 004 0" />
  </svg>
);

export const helpIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <circle cx="9" cy="9" r="7" />
    <path d="M7 7a2 2 0 014 0c0 1-2 1.5-2 3M9 13v.5" />
  </svg>
);

export const collapseIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M11 4L6 9l5 5" />
    <rect x="2" y="3" width="14" height="12" />
  </svg>
);

/* Chevron del toggle de grupos colapsables del nav (apunta hacia abajo). */
export const chevronIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M5 7l4 4 4-4" />
  </svg>
);

/**
 * Panel con flecha hacia adentro. Va en «Colapsar conversaciones», el
 * control que angosta el rail de conversaciones del asistente a 60 px
 * (asistente-superficie-frontend).
 */
export const railColapsarIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="2.5" y="3" width="13" height="12" rx="1.5" />
    <path d="M7 3v12" />
    <path d="M11.5 7l-2 2 2 2" />
  </svg>
);

/**
 * El mismo panel, con la flecha hacia afuera. Va en «Expandir
 * conversaciones», el par del ícono anterior.
 */
export const railExpandirIcon: ReactNode = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="2.5" y="3" width="13" height="12" rx="1.5" />
    <path d="M7 3v12" />
    <path d="M9.5 7l2 2-2 2" />
  </svg>
);

/* Sidebar nav module icons, keyed for the nav config */
export const navIcons = {
  pedidos: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <rect x="3" y="3" width="12" height="13" />
      <path d="M6 7h6M6 10h6M6 13h4" />
    </svg>
  ),
  revision: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <rect x="4" y="3" width="10" height="13" rx="1" />
      <path d="M7 3.2V2.4a.8.8 0 01.8-.8h2.4a.8.8 0 01.8.8v.8" />
      <path d="M6.5 9.5l1.6 1.6 3-3.6" />
    </svg>
  ),
  periodos: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <rect x="3" y="4" width="12" height="11" rx="1" />
      <path d="M3 7.5h12M6 2.5v3M12 2.5v3" />
    </svg>
  ),
  designaciones: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <circle cx="9" cy="6" r="3" />
      <path d="M3 16c0-3 3-5 6-5s6 2 6 5" />
    </svg>
  ),
  aulas: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <rect x="3" y="3" width="12" height="12" />
      <path d="M3 9h12M9 3v12" />
    </svg>
  ),
  tareas: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <rect x="3" y="3" width="12" height="12" />
      <path d="M6 9l2 2 4-4" />
    </svg>
  ),
  portal: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <circle cx="9" cy="6.5" r="2.5" />
      <path d="M3 16a6 6 0 0112 0" />
    </svg>
  ),
  reportes: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <path d="M3 14V4M3 14h12M6 11V7M9 11V5M12 11v-3" />
    </svg>
  ),
  settings: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <circle cx="9" cy="9" r="2" />
      <path d="M9 2v2M9 14v2M2 9h2M14 9h2M4 4l1.5 1.5M12.5 12.5L14 14M4 14l1.5-1.5M12.5 5.5L14 4" />
    </svg>
  ),
  usuarios: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <circle cx="6" cy="6" r="2.5" />
      <circle cx="13" cy="7" r="2" />
      <path d="M1.5 15.5c.4-2.7 2-4 4.5-4s4.1 1.3 4.5 4M10.5 13c2.8-1.6 5.3-.4 6 2.5" />
    </svg>
  ),
  docentes: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <path d="M2 6.5L9 3l7 3.5-7 3.5-7-3.5Z" />
      <path d="M5 8.5v3.2c2.3 1.7 5.7 1.7 8 0V8.5M16 7v4" />
    </svg>
  ),
  roles: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <path d="M9 2.5l5.5 2v4.1c0 3.2-2.3 5.5-5.5 6.9-3.2-1.4-5.5-3.7-5.5-6.9V4.5l5.5-2Z" />
      <path d="M6.5 8.5h5M9 6v5" />
    </svg>
  ),
  membresiaRoles: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <path d="M9 2.5l5.5 2v4.1c0 3.2-2.3 5.5-5.5 6.9-3.2-1.4-5.5-3.7-5.5-6.9V4.5l5.5-2Z" />
      <path d="m6 8.7 2 2 4-4" />
    </svg>
  ),
  historial: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <circle cx="9" cy="9.5" r="6" />
      <path d="M9 6v3.5l2.5 1.5" />
      <path d="M3.5 4.5v2.5H6" />
    </svg>
  ),
} satisfies Record<string, ReactNode>;

/**
 * Destello. Marca lo que resuelve un modelo, no una consulta determinista.
 *
 * Se repite en el lanzador y en el campo de entrada a propósito: es la misma
 * promesa en los dos lugares —«acá le hablás al asistente»—, y usar dos símbolos
 * distintos para lo mismo obliga a aprender dos.
 */
export const sparkIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M9 2.5l1.3 3.4L13.7 7l-3.4 1.1L9 11.5 7.7 8.1 4.3 7l3.4-1.1z" />
    <path d="M13.8 11.6l.6 1.5 1.5.6-1.5.6-.6 1.5-.6-1.5-1.5-.6 1.5-.6z" />
  </svg>
);

/**
 * Candado. Marca una columna que trae un dato personal.
 *
 * Va junto al nombre de la columna en la tabla de resultados del asistente. Dice
 * QUÉ es sensible, no por dónde viajó: el enmascaramiento es mecánica interna.
 */
export const lockIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="4" y="8" width="10" height="7" rx="1" />
    <path d="M6.5 8V6a2.5 2.5 0 015 0v2" />
  </svg>
);

/**
 * Avión de papel. Va delante de «Enviar» en el composer del asistente.
 *
 * Acompaña a la etiqueta, no la reemplaza: un ícono solo obliga a descubrir qué
 * hace, y Enter es un atajo y no la única vía de envío.
 */
export const sendIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M15.5 2.5L2.5 8l5.5 2 2 5.5z" />
    <path d="M15.5 2.5L8 10" />
  </svg>
);

/**
 * Más. Va delante de «Nueva conversación» en el asistente.
 */
export const plusIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M9 3.5v11M3.5 9h11" />
  </svg>
);

/**
 * Cuadrado de parar. Va delante de «Dejar de esperar» en el asistente: es el
 * símbolo que el usuario ya conoce, y acá para sólo la espera de este lado.
 */
export const stopIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="4.5" y="4.5" width="9" height="9" rx="1.5" />
  </svg>
);

/**
 * Dos hojas superpuestas. Va delante de «Copiar respuesta» y «Copiar tabla» en
 * el asistente.
 */
export const copyIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="6.5" y="6.5" width="9" height="9" rx="1" />
    <path d="M4.5 11.5h-1a1 1 0 01-1-1v-7a1 1 0 011-1h7a1 1 0 011 1v1" />
  </svg>
);

/**
 * Flecha hacia abajo. Va delante de «Ir al final» en el hilo del asistente.
 */
export const arrowDownIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M9 3.5v11M4.5 10l4.5 4.5 4.5-4.5" />
  </svg>
);

/**
 * Pulgar hacia arriba. Va en el botón «Me sirvió» de la calificación de un
 * turno del asistente.
 */
export const thumbUpIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M6 8v7H3.5V8H6zm0 0l2.5-5a1.5 1.5 0 0 1 2.9.6L11 6.5h2.5A1.5 1.5 0 0 1 15 8.2l-.9 5.3a1.5 1.5 0 0 1-1.5 1.5H6" />
  </svg>
);

/**
 * Pulgar hacia abajo. Va en el botón «No me sirvió» de la calificación de un
 * turno del asistente.
 */
export const thumbDownIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M6 10V3h-2.5v7H6zm0 0l2.5 5a1.5 1.5 0 0 0 2.9-.6L11 11.5h2.5A1.5 1.5 0 0 0 15 9.8l-.9-5.3a1.5 1.5 0 0 0-1.5-1.5H6" />
  </svg>
);

/**
 * Flecha hacia una bandeja. Va delante de la acción de exportar el resultado a
 * CSV en el asistente.
 */
export const downloadIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M9 3v8M5.5 8L9 11.5 12.5 8" />
    <path d="M3.5 13.5h11" />
  </svg>
);

/**
 * Reloj con flecha atrás. Va en «Historial» —conversaciones propias del
 * asistente— y en el ítem de navegación de la lectura de soporte del
 * historial ajeno.
 */
export const historyIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <circle cx="9" cy="9.5" r="6" />
    <path d="M9 6v3.5l2.5 1.5" />
    <path d="M3.5 4.5v2.5H6" />
  </svg>
);

/**
 * Lápiz sobre una línea. Va en «Renombrar», en el «⋮» del rail de
 * conversaciones del asistente.
 */
export const pencilIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M11.5 2.5a1.4 1.4 0 0 1 2 2L5.5 12.5l-3 1 1-3z" />
    <path d="M10 4l2 2" />
  </svg>
);

/**
 * Caja con flecha hacia adentro. Va en «Archivar», en el «⋮» del rail.
 */
export const archiveIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="2.5" y="3" width="13" height="3.5" rx="1" />
    <path d="M3.5 6.5v6.5a1 1 0 0 0 1 1h9a1 1 0 0 0 1-1V6.5" />
    <path d="M7.2 9.5h3.6" />
  </svg>
);

/**
 * La misma caja, con la flecha hacia afuera. Va en «Desarchivar».
 */
export const archiveRestoreIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <rect x="2.5" y="3" width="13" height="3.5" rx="1" />
    <path d="M3.5 6.5v6.5a1 1 0 0 0 1 1h9a1 1 0 0 0 1-1V6.5" />
    <path d="M9 11.5V8M7.2 9.7 9 8l1.8 1.7" />
  </svg>
);

/**
 * Tacho de basura. Va en «Eliminar», en el «⋮» del rail —en
 * `--color-text-danger` (design spec § v3).
 */
export const trashIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M3.5 5h11" />
    <path d="M6 5V3.5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1V5" />
    <path d="M4.5 5v9a1 1 0 0 0 1 1h7a1 1 0 0 0 1-1V5" />
    <path d="M7.5 8v4M10.5 8v4" />
  </svg>
);

/**
 * Flecha hacia arriba. Va en el encabezado de una columna de la tabla del
 * asistente ordenada ascendente (asistente-tabla-de-resultado).
 */
export const ordenAscendenteIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M9 13.5V4.5M5.5 8l3.5-3.5L12.5 8" />
  </svg>
);

/**
 * Flecha hacia abajo. El par del anterior, para la columna ordenada
 * descendente.
 */
export const ordenDescendenteIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M9 4.5v9M5.5 10l3.5 3.5L12.5 10" />
  </svg>
);

/**
 * Flecha doble, «⇅». Va en el encabezado de una columna que todavía no está
 * ordenada, tenue hasta el hover o el foco (el mismo ahorro de ruido visual
 * que el «⋮» del rail).
 */
export const ordenNeutroIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M6.5 2.5v4M4 5l2.5-2.5L9 5" />
    <path d="M11.5 15.5v-4M14 13l-2.5 2.5L9 13" />
  </svg>
);

/**
 * Cuatro esquinas hacia afuera. Va en «Ampliar tabla», en la tabla de
 * resultados del asistente.
 */
export const ampliarTablaIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M11 3h4v4" />
    <path d="M15 3l-4.5 4.5" />
    <path d="M7 15H3v-4" />
    <path d="M3 15l4.5-4.5" />
  </svg>
);

/**
 * Las mismas cuatro esquinas, hacia adentro. Va en «Contraer», el par del
 * ícono anterior en la tabla ampliada.
 */
export const contraerTablaIcon = (
  <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
    <path d="M15 7h-4V3" />
    <path d="M11 7l4-4" />
    <path d="M3 11h4v4" />
    <path d="M7 11l-4 4" />
  </svg>
);

export type NavIconKey = keyof typeof navIcons;
