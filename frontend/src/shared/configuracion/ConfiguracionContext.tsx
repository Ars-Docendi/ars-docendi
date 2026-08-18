import { useState } from "react";
import { Outlet } from "react-router-dom";
import {
  ROLES_INICIALES,
  editarRol as editarRolPuro,
  generarCodigoRol,
  type DatosRolEditables,
  type DatosRolNuevo,
  type RolMock,
} from "../../features/roles/mock/mockStore";
import {
  MEMBRESIAS_INICIALES,
  actualizarMembresia,
  type MapaMembresias,
} from "../../features/membresia-roles/mock/mockStore";
import { ConfiguracionContext } from "./useConfiguracion";

export function ConfiguracionProvider() {
  const [roles, setRoles] = useState<RolMock[]>(ROLES_INICIALES);
  const [membresias, setMembresias] = useState<MapaMembresias>(MEMBRESIAS_INICIALES);

  function agregarRol(datos: DatosRolNuevo, rolBaseId: string | null) {
    const nuevoId = crypto.randomUUID();
    setRoles((prev) => [
      ...prev,
      {
        ...datos,
        id: nuevoId,
        codigo: generarCodigoRol(datos.nombre),
        es_sistema: false,
      },
    ]);
    if (rolBaseId) {
      const permisosBase = membresias[rolBaseId] ?? [];
      setMembresias((prev) => actualizarMembresia(prev, nuevoId, [...permisosBase]));
    }
  }

  function editarRol(id: string, datos: DatosRolEditables) {
    setRoles((prev) => editarRolPuro(prev, id, datos));
  }

  function eliminarRol(id: string) {
    if (roles.some((rol) => rol.id === id && rol.es_sistema)) return;
    setRoles((prev) => prev.filter((r) => r.id !== id || r.es_sistema));
    setMembresias((prev) => {
      const siguiente = { ...prev };
      delete siguiente[id];
      return siguiente;
    });
  }

  function togglePermiso(rolId: string, permisoId: string) {
    setMembresias((prev) => {
      const actuales = prev[rolId] ?? [];
      const nuevos = actuales.includes(permisoId)
        ? actuales.filter((p) => p !== permisoId)
        : [...actuales, permisoId];
      return actualizarMembresia(prev, rolId, nuevos);
    });
  }

  return (
    <ConfiguracionContext.Provider
      value={{ roles, membresias, agregarRol, editarRol, eliminarRol, togglePermiso }}
    >
      <Outlet />
    </ConfiguracionContext.Provider>
  );
}
