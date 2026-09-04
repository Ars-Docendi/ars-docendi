import type { DatosEducacion, Educacion } from "../types";
import { agregarEducacion, editarEducacion, eliminarPorId } from "../mock/mockStore";
import { ordenarPorPeriodo } from "../formato";
import { useSeccionLista } from "../hooks/useSeccionLista";
import { SeccionLista } from "./SeccionLista";
import { ItemEducacion } from "./ItemsPerfil";
import { ModalEducacion } from "./ModalEducacion";
import { ModalConfirmarEliminar } from "./ModalConfirmarEliminar";

interface SeccionEducacionProps {
  items: Educacion[];
  onCambio: (items: Educacion[]) => void;
}

export function SeccionEducacion({ items, onCambio }: SeccionEducacionProps) {
  const s = useSeccionLista<Educacion>();

  function guardar(datos: DatosEducacion) {
    onCambio(
      s.enEdicion ? editarEducacion(items, s.enEdicion.id, datos) : agregarEducacion(items, datos),
    );
    s.cerrarModal();
  }

  return (
    <>
      <SeccionLista
        titulo="Educación"
        variante="cronologia"
        items={ordenarPorPeriodo(items)}
        etiquetaItem={(item) => item.carrera}
        renderItem={(item) => <ItemEducacion item={item} />}
        onAgregar={s.abrirAlta}
        onEditar={s.abrirEdicion}
        onEliminar={s.pedirBorrado}
      />
      {s.modalAbierto && (
        <ModalEducacion educacion={s.enEdicion} onCerrar={s.cerrarModal} onGuardar={guardar} />
      )}
      <ModalConfirmarEliminar
        open={s.aEliminar !== null}
        onOpenChange={(abierto) => !abierto && s.cancelarBorrado()}
        titulo="Eliminar formación"
        nombre={s.aEliminar?.carrera ?? ""}
        onConfirmar={() => {
          if (s.aEliminar) onCambio(eliminarPorId(items, s.aEliminar.id));
          s.cancelarBorrado();
        }}
      />
    </>
  );
}
