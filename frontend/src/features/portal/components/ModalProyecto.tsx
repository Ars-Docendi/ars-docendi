import { useState } from "react";
import { Button, Field, FileUpload, Input, Modal, Textarea } from "@ars-docendi/ui";

import type { DatosProyecto, Proyecto } from "../types";
import { CampoPeriodo } from "./CampoPeriodo";
import "./portal.css";

const VACIO: DatosProyecto = {
  nombre: "",
  rol: "",
  desde: "",
  hasta: "",
  descripcion: "",
  documento: null,
  doi: "",
};

interface ModalProyectoProps {
  proyecto: Proyecto | null;
  onCerrar: () => void;
  onGuardar: (datos: DatosProyecto) => void;
}

/**
 * Alta y edición de un proyecto. Los trabajos de investigación se cargan acá:
 * cada proyecto puede llevar su PDF, su DOI, ambos o ninguno.
 *
 * TODO(backend): el adjunto es metadata mock; el archivo no se sube.
 */
export function ModalProyecto({ proyecto, onCerrar, onGuardar }: ModalProyectoProps) {
  const [datos, setDatos] = useState<DatosProyecto>(() => (proyecto ? { ...proyecto } : VACIO));
  const [actual, setActual] = useState(() => proyecto?.hasta === null);
  const [errores, setErrores] = useState<Record<string, string>>({});

  function guardar() {
    const nuevos: Record<string, string> = {};
    if (!datos.nombre.trim()) nuevos.nombre = "Ingresá el nombre del proyecto.";
    if (!datos.rol.trim()) nuevos.rol = "Ingresá tu rol.";
    if (!datos.desde.trim()) nuevos.desde = "Ingresá desde cuándo.";
    if (!datos.descripcion.trim()) nuevos.descripcion = "Contá de qué se trata.";
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) return;

    onGuardar({
      nombre: datos.nombre.trim(),
      rol: datos.rol.trim(),
      desde: datos.desde.trim(),
      hasta: actual ? null : datos.hasta?.trim() || null,
      descripcion: datos.descripcion.trim(),
      documento: datos.documento,
      doi: datos.doi?.trim() ?? "",
    });
  }

  return (
    <Modal
      open
      onOpenChange={(abierto) => !abierto && onCerrar()}
      title={proyecto ? "Editar proyecto" : "Agregar proyecto"}
      footer={
        <>
          <Button variant="secondary" onClick={onCerrar}>
            Cancelar
          </Button>
          <Button variant="primary" onClick={guardar}>
            Guardar
          </Button>
        </>
      }
    >
      <div className="portal-form">
        <Field label="Nombre" required error={errores.nombre}>
          <Input
            value={datos.nombre}
            onChange={(e) => setDatos({ ...datos, nombre: e.target.value })}
          />
        </Field>
        <Field label="Tu rol" required error={errores.rol}>
          <Input value={datos.rol} onChange={(e) => setDatos({ ...datos, rol: e.target.value })} />
        </Field>
        <CampoPeriodo
          desde={datos.desde}
          hasta={datos.hasta}
          enCurso={actual}
          etiquetaEnCurso="Sigue en curso"
          errorDesde={errores.desde}
          onDesde={(desde) => setDatos({ ...datos, desde })}
          onHasta={(hasta) => setDatos({ ...datos, hasta })}
          onEnCurso={setActual}
        />
        <Field label="De qué se trata" required error={errores.descripcion}>
          <Textarea
            rows={3}
            value={datos.descripcion}
            onChange={(e) => setDatos({ ...datos, descripcion: e.target.value })}
          />
        </Field>
        <Field label="DOI">
          <Input
            value={datos.doi ?? ""}
            placeholder="10.1000/ejemplo.2025.114"
            onChange={(e) => setDatos({ ...datos, doi: e.target.value })}
          />
        </Field>
        <Field label="Documento">
          <FileUpload
            accept="application/pdf,.pdf"
            title="Arrastrá el PDF o hacé clic para subirlo"
            files={
              datos.documento
                ? [{ id: "doc", name: datos.documento.nombre, status: "uploaded" as const }]
                : []
            }
            onFilesAdded={(archivos) => {
              const archivo = archivos[0];
              if (archivo) setDatos({ ...datos, documento: { nombre: archivo.name } });
            }}
            onRemove={() => setDatos({ ...datos, documento: null })}
          />
        </Field>
      </div>
    </Modal>
  );
}
