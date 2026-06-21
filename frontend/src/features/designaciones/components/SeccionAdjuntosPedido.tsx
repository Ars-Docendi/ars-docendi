import { FileUpload, InlineAlert } from "@ars-docendi/ui";
import type { UploadedFile } from "@ars-docendi/ui";
import type { Novedad, TipoAdjunto } from "../types";

/** Adjuntos obligatorios de un Alta (replica los 3 dropzones del frame). */
const ADJUNTOS_ALTA: { tipo: TipoAdjunto; titulo: string; hint: string }[] = [
  { tipo: "cv", titulo: "CV (PDF)", hint: "Subir · PDF hasta 10MB" },
  { tipo: "dni_frente", titulo: "DNI · Frente", hint: "Imagen o PDF" },
  { tipo: "dni_dorso", titulo: "DNI · Dorso", hint: "Imagen o PDF" },
];

interface SeccionAdjuntosPedidoProps {
  /** Solo Alta / Baja / Cambio tienen sección de adjuntos. */
  novedad: Novedad;
  errorAdjuntos?: string;
  adjuntoComoUploaded: (tipo: TipoAdjunto) => UploadedFile[];
  onAgregar: (tipo: TipoAdjunto, archivos: FileList) => void;
  onQuitar: (tipo: TipoAdjunto) => void;
}

/**
 * Sección de documentación adjunta. Obligatoria en Alta (CV + DNI frente/dorso)
 * y Baja (justificativo); opcional en Cambio (documento de respaldo).
 */
export function SeccionAdjuntosPedido({
  novedad,
  errorAdjuntos,
  adjuntoComoUploaded,
  onAgregar,
  onQuitar,
}: SeccionAdjuntosPedidoProps) {
  if (novedad === "Alta") {
    return (
      <section className="adoc-pf-sec">
        <h2 className="adoc-pf-sec-h">Documentación obligatoria · Alta</h2>
        {errorAdjuntos && (
          <InlineAlert severity="warning" title="Faltan adjuntos">
            {errorAdjuntos}
          </InlineAlert>
        )}
        <div className="adoc-pf-row-3">
          {ADJUNTOS_ALTA.map(({ tipo, titulo, hint }) => (
            <FileUpload
              key={tipo}
              title={titulo}
              hint={hint}
              files={adjuntoComoUploaded(tipo)}
              onFilesAdded={(archivos) => onAgregar(tipo, archivos)}
              onRemove={() => onQuitar(tipo)}
            />
          ))}
        </div>
      </section>
    );
  }

  if (novedad === "Baja") {
    return (
      <section className="adoc-pf-sec">
        <h2 className="adoc-pf-sec-h">Documentación obligatoria · Baja</h2>
        {errorAdjuntos && (
          <InlineAlert severity="warning" title="Falta el justificativo">
            {errorAdjuntos}
          </InlineAlert>
        )}
        <div className="adoc-pf-dz-single">
          <FileUpload
            title="Documento justificativo de la baja"
            hint="Obligatorio · PDF o imagen hasta 10MB"
            files={adjuntoComoUploaded("justificativo")}
            onFilesAdded={(archivos) => onAgregar("justificativo", archivos)}
            onRemove={() => onQuitar("justificativo")}
          />
        </div>
      </section>
    );
  }

  // Cambio de cargo o dedicación: respaldo opcional.
  return (
    <section className="adoc-pf-sec">
      <h2 className="adoc-pf-sec-h">Adjuntos · Opcional</h2>
      <div className="adoc-pf-dz-single">
        <FileUpload
          title="Documento de respaldo"
          hint="Opcional · PDF o imagen hasta 10MB"
          files={adjuntoComoUploaded("justificativo")}
          onFilesAdded={(archivos) => onAgregar("justificativo", archivos)}
          onRemove={() => onQuitar("justificativo")}
        />
      </div>
    </section>
  );
}
