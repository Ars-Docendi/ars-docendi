import type { DatosProyecto, Proyecto } from "../types";
import { agregarProyecto, editarProyecto, eliminarPorId } from "../mock/mockStore";
import { ordenarPorPeriodo } from "../formato";
import { useSeccionLista } from "../hooks/useSeccionLista";
import { SeccionLista } from "./SeccionLista";
import { ItemProyecto } from "./ItemsPerfil";
import { ModalProyecto } from "./ModalProyecto";
import { ModalConfirmarEliminar } from "./ModalConfirmarEliminar";

interface SeccionProyectosProps {
  items: Proyecto[];
  onCambio: (items: Proyecto[]) => void;
}

/** Los trabajos de investigación y su documentación se cargan como proyectos. */
export function SeccionProyectos({ items, onCambio }: SeccionProyectosProps) {
  const s = useSeccionLista<Proyecto>();

  function guardar(datos: DatosProyecto) {
    onCambio(
      s.enEdicion ? editarProyecto(items, s.enEdicion.id, datos) : agregarProyecto(items, datos),
    );
    s.cerrarModal();
  }

  return (
    <>
      <SeccionLista
        titulo="Proyectos"
        items={ordenarPorPeriodo(items)}
        etiquetaItem={(item) => item.nombre}
        renderItem={(item) => <ItemProyecto item={item} />}
        onAgregar={s.abrirAlta}
        onEditar={s.abrirEdicion}
        onEliminar={s.pedirBorrado}
      />
      {s.modalAbierto && (
        <ModalProyecto proyecto={s.enEdicion} onCerrar={s.cerrarModal} onGuardar={guardar} />
      )}
      <ModalConfirmarEliminar
        open={s.aEliminar !== null}
        onOpenChange={(abierto) => !abierto && s.cancelarBorrado()}
        titulo="Eliminar proyecto"
        nombre={s.aEliminar?.nombre ?? ""}
        onConfirmar={() => {
          if (s.aEliminar) onCambio(eliminarPorId(items, s.aEliminar.id));
          s.cancelarBorrado();
        }}
      />
    </>
  );
}
