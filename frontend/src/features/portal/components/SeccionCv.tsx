import { useState } from "react";
import { FileUpload, InlineAlert } from "@ars-docendi/ui";

import type { ArchivoCv } from "../types";
import { SeccionPerfil } from "./SeccionPerfil";

interface SeccionCvProps {
  cv: ArchivoCv | null;
  onCargar: (cv: ArchivoCv) => void;
  onEliminar: () => void;
}

function esPdf(archivo: File): boolean {
  return archivo.type === "application/pdf" || archivo.name.toLowerCase().endsWith(".pdf");
}

function hoyIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * CV del docente: un único archivo, sin historial. Vacío se presenta como zona
 * de arrastre en vez de una fila con "+": la forma explica la acción sin texto.
 * Es la carga de mayor valor por menor esfuerzo, porque el PDF ya existe.
 *
 * TODO(backend): hoy solo se registra metadata; el archivo no se sube.
 */
export function SeccionCv({ cv, onCargar, onEliminar }: SeccionCvProps) {
  const [error, setError] = useState<string | undefined>();

  function recibir(archivos: FileList) {
    const archivo = archivos[0];
    if (!archivo) return;
    if (!esPdf(archivo)) {
      setError("El CV tiene que ser un archivo PDF.");
      return;
    }
    setError(undefined);
    onCargar({ nombre: archivo.name, fechaCarga: hoyIso() });
  }

  return (
    <SeccionPerfil titulo="CV">
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
        {error && (
          <InlineAlert severity="danger" title="No se pudo cargar el CV">
            {error}
          </InlineAlert>
        )}
        <FileUpload
          accept="application/pdf,.pdf"
          error={Boolean(error)}
          title="Arrastrá tu CV en PDF o hacé clic para subirlo"
          files={
            cv
              ? [
                  {
                    id: "cv",
                    name: cv.nombre,
                    size: `Actualizado el ${cv.fechaCarga}`,
                    status: "uploaded" as const,
                  },
                ]
              : []
          }
          onFilesAdded={recibir}
          onRemove={onEliminar}
        />
      </div>
    </SeccionPerfil>
  );
}
