import { createContext, useContext } from "react";

/**
 * Cómo se muestra el perfil. En Mi Portal es editable y sin descargas; desde la administración
 * de personas puede ser de sólo lectura (sin acciones, como el bloque Perfil) y ofrecer descargar
 * el CV y los documentos de proyecto.
 */
export interface ModoPerfil {
  soloLectura: boolean;
  descargarCv?: () => void;
  descargarDocumento?: (proyectoId: string) => void;
}

export const ModoPerfilContexto = createContext<ModoPerfil>({ soloLectura: false });

export function useModoPerfil() {
  return useContext(ModoPerfilContexto);
}
