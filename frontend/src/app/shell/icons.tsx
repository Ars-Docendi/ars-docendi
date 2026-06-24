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

/* Sidebar nav module icons, keyed for the nav config */
export const navIcons = {
  pedidos: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <rect x="3" y="3" width="12" height="13" />
      <path d="M6 7h6M6 10h6M6 13h4" />
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
      <circle cx="6.5" cy="6" r="2.5" />
      <path d="M1 16c0-2.5 2.5-4 5.5-4s5.5 1.5 5.5 4" />
      <circle cx="13" cy="5.5" r="2" />
      <path d="M13 10c1.5 0 4 .75 4 3" />
    </svg>
  ),
  docentes: (
    <svg viewBox="0 0 18 18" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
      <circle cx="7" cy="5.5" r="2.5" />
      <path d="M2 16c0-2.5 2.5-4 5-4s5 1.5 5 4" />
      <rect x="11" y="8" width="6" height="5" rx="0.5" />
      <path d="M13 10h2M13 12h1" />
    </svg>
  ),
} satisfies Record<string, ReactNode>;

export type NavIconKey = keyof typeof navIcons;
