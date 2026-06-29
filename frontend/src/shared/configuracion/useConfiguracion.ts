import { createContext, useContext } from "react";
import type { RolMock } from "../../features/roles/mock/mockStore";
import type { MapaMembresias } from "../../features/membresia-roles/mock/mockStore";

export interface ConfiguracionContextValue {
  roles: RolMock[];
  membresias: MapaMembresias;
  agregarRol: (datos: Omit<RolMock, "id">, rolBaseId: string | null) => void;
  editarRol: (id: string, datos: Omit<RolMock, "id">) => void;
  eliminarRol: (id: string) => void;
  togglePermiso: (rolId: string, permisoId: string) => void;
}

export const ConfiguracionContext = createContext<ConfiguracionContextValue | null>(null);

export function useConfiguracion(): ConfiguracionContextValue {
  const ctx = useContext(ConfiguracionContext);
  if (!ctx) throw new Error("useConfiguracion debe usarse dentro de ConfiguracionProvider");
  return ctx;
}
