import type { DatosExperiencia, Experiencia } from "../types";
import { agregarExperiencia, editarExperiencia, eliminarPorId } from "../helpers";
import { ordenarPorPeriodo } from "../formato";
import { useSeccionLista } from "../hooks/useSeccionLista";
import { SeccionLista } from "./SeccionLista";
import { ItemExperiencia } from "./ItemsPerfil";
import { ModalExperiencia } from "./ModalExperiencia";
import { ModalConfirmarEliminar } from "./ModalConfirmarEliminar";

interface SeccionExperienciaProps {
  items: Experiencia[];
  onCambio: (items: Experiencia[]) => void;
}

export function SeccionExperiencia({ items, onCambio }: SeccionExperienciaProps) {
  const s = useSeccionLista<Experiencia>();

  function guardar(datos: DatosExperiencia) {
    onCambio(
      s.enEdicion
        ? editarExperiencia(items, s.enEdicion.id, datos)
        : agregarExperiencia(items, datos),
    );
    s.cerrarModal();
  }

  return (
    <>
      <SeccionLista
        titulo="Experiencia"
        variante="cronologia"
        items={ordenarPorPeriodo(items)}
        etiquetaItem={(item) => item.puesto}
        renderItem={(item) => <ItemExperiencia item={item} />}
        onAgregar={s.abrirAlta}
        onEditar={s.abrirEdicion}
        onEliminar={s.pedirBorrado}
      />
      {s.modalAbierto && (
        <ModalExperiencia experiencia={s.enEdicion} onCerrar={s.cerrarModal} onGuardar={guardar} />
      )}
      <ModalConfirmarEliminar
        open={s.aEliminar !== null}
        onOpenChange={(abierto) => !abierto && s.cancelarBorrado()}
        titulo="Eliminar experiencia"
        nombre={s.aEliminar?.puesto ?? ""}
        onConfirmar={() => {
          if (s.aEliminar) onCambio(eliminarPorId(items, s.aEliminar.id));
          s.cancelarBorrado();
        }}
      />
    </>
  );
}
