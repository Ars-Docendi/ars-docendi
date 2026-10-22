/**
 * Sustituto de `useCurrentUser` para los tests de ayuda: se instala con
 * `vi.mock("<ruta>/shared/auth/useCurrentUser", () => import("<ruta>/testing/sesionPrueba"))`
 * y cada test fija `sesion.permisos`.
 */
export const sesion: { permisos: string[] } = { permisos: [] };

export function useCurrentUser() {
  return {
    user: {
      id: "1",
      name: "Vidal, Ernesto",
      initials: "EV",
      upn: "evidal@unlam.edu.ar",
      role: "Docente",
      roleCode: "docente",
      permissions: sesion.permisos,
    },
    isLoading: false,
    error: null,
    retry: () => {},
  };
}
