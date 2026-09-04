import type { Certificacion, DatosCertificacion } from "../types";
import { agregarCertificacion, editarCertificacion, eliminarPorId } from "../mock/mockStore";
import { ordenarPorFecha } from "../formato";
import { useSeccionLista } from "../hooks/useSeccionLista";
import { SeccionLista } from "./SeccionLista";
import { ItemCertificacion } from "./ItemsPerfil";
import { ModalCertificacion } from "./ModalCertificacion";
import { ModalConfirmarEliminar } from "./ModalConfirmarEliminar";

interface SeccionCertificacionesProps {
  items: Certificacion[];
  onCambio: (items: Certificacion[]) => void;
}

export function SeccionCertificaciones({ items, onCambio }: SeccionCertificacionesProps) {
  const s = useSeccionLista<Certificacion>();

  function guardar(datos: DatosCertificacion) {
    onCambio(
      s.enEdicion
        ? editarCertificacion(items, s.enEdicion.id, datos)
        : agregarCertificacion(items, datos),
    );
    s.cerrarModal();
  }

  return (
    <>
      <SeccionLista
        titulo="Certificaciones"
        items={ordenarPorFecha(items)}
        etiquetaItem={(item) => item.nombre}
        renderItem={(item) => <ItemCertificacion item={item} />}
        onAgregar={s.abrirAlta}
        onEditar={s.abrirEdicion}
        onEliminar={s.pedirBorrado}
      />
      {s.modalAbierto && (
        <ModalCertificacion
          certificacion={s.enEdicion}
          onCerrar={s.cerrarModal}
          onGuardar={guardar}
        />
      )}
      <ModalConfirmarEliminar
        open={s.aEliminar !== null}
        onOpenChange={(abierto) => !abierto && s.cancelarBorrado()}
        titulo="Eliminar certificación"
        nombre={s.aEliminar?.nombre ?? ""}
        onConfirmar={() => {
          if (s.aEliminar) onCambio(eliminarPorId(items, s.aEliminar.id));
          s.cancelarBorrado();
        }}
      />
    </>
  );
}
