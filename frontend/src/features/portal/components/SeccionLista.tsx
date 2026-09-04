import type { ReactNode } from "react";

import { MenuAcciones } from "../../../shared/ui/MenuAcciones";
import { IconoSquarePen, IconoTrash2 } from "../../../shared/ui/iconos";
import { SeccionPerfil } from "./SeccionPerfil";
import "./portal.css";

/** Un ítem con período abierto (`hasta: null`) está vigente. */
function esVigente(item: unknown): boolean {
  return typeof item === "object" && item !== null && "hasta" in item && item.hasta === null;
}

interface SeccionListaProps<T extends { id: string }> {
  titulo: string;
  items: T[];
  /** Texto accesible del kebab de cada fila. */
  etiquetaItem: (item: T) => string;
  renderItem: (item: T) => ReactNode;
  onAgregar: () => void;
  onEditar: (item: T) => void;
  onEliminar: (item: T) => void;
  /** "cronologia" dibuja los ítems sobre una línea de tiempo vertical. */
  variante?: "lista" | "cronologia";
}

/**
 * Sección del perfil que contiene una lista administrable. La comparten
 * Experiencia, Educación, Certificaciones y Proyectos: misma mecánica de alta,
 * edición y borrado, y misma presentación cuando está vacía.
 */
export function SeccionLista<T extends { id: string }>({
  titulo,
  items,
  etiquetaItem,
  renderItem,
  onAgregar,
  onEditar,
  onEliminar,
  variante = "lista",
}: SeccionListaProps<T>) {
  const accion = { etiqueta: "+ Agregar", onClick: onAgregar };

  if (items.length === 0) {
    return <SeccionPerfil titulo={titulo} vacia accion={accion} />;
  }

  return (
    <SeccionPerfil titulo={titulo} accion={accion}>
      <div
        className={variante === "cronologia" ? "portal-items portal-cronologia" : "portal-items"}
      >
        {items.map((item) => (
          <div
            className={"portal-item" + (esVigente(item) ? " portal-item-vigente" : "")}
            key={item.id}
          >
            <div className="portal-item-cuerpo">{renderItem(item)}</div>
            <MenuAcciones
              etiquetaAria={`Acciones de ${etiquetaItem(item)}`}
              acciones={[
                {
                  etiqueta: "Editar",
                  icono: <IconoSquarePen />,
                  onSelect: () => onEditar(item),
                },
                {
                  etiqueta: "Eliminar",
                  icono: <IconoTrash2 />,
                  peligro: true,
                  onSelect: () => onEliminar(item),
                },
              ]}
            />
          </div>
        ))}
      </div>
    </SeccionPerfil>
  );
}
