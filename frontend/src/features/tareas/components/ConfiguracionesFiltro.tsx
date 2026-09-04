import { useState } from "react";
import { Button, Field, Input, Modal, Select } from "@ars-docendi/ui";
import type { ConfiguracionFiltro } from "../api/filtrosGuardadosStore";

interface ConfiguracionesFiltroProps {
  configuraciones: ConfiguracionFiltro[];
  onAplicar: (config: ConfiguracionFiltro) => void;
  onGuardar: (nombre: string) => void;
  guardando?: boolean;
}

/**
 * Selector para aplicar una configuración de filtros guardada + botón
 * "Guardar filtros" que abre un modal simple para nombrar la combinación
 * actual. Las configuraciones ya vienen filtradas por usuario (ver
 * `useFiltrosGuardados`).
 */
export function ConfiguracionesFiltro({
  configuraciones,
  onAplicar,
  onGuardar,
  guardando = false,
}: ConfiguracionesFiltroProps) {
  const [modalAbierto, setModalAbierto] = useState(false);
  const [nombre, setNombre] = useState("");
  const [enviado, setEnviado] = useState(false);

  function cerrarModal() {
    setModalAbierto(false);
    setNombre("");
    setEnviado(false);
  }

  function confirmarGuardar() {
    setEnviado(true);
    if (!nombre.trim()) return;
    onGuardar(nombre.trim());
    cerrarModal();
  }

  return (
    <div className="adoc-tareas-config-filtros">
      {configuraciones.length > 0 && (
        <Select
          value=""
          aria-label="Aplicar configuración de filtros guardada"
          onChange={(e) => {
            const config = configuraciones.find((c) => c.id === e.target.value);
            if (config) onAplicar(config);
          }}
        >
          <option value="">Configuraciones guardadas…</option>
          {configuraciones.map((c) => (
            <option key={c.id} value={c.id}>
              {c.nombre}
            </option>
          ))}
        </Select>
      )}
      <Button variant="secondary" size="sm" onClick={() => setModalAbierto(true)}>
        Guardar filtros
      </Button>

      <Modal
        open={modalAbierto}
        onOpenChange={(open) => {
          if (!open) cerrarModal();
        }}
        title="Guardar configuración de filtros"
        footer={
          <>
            <Button variant="secondary" onClick={cerrarModal}>
              Cancelar
            </Button>
            <Button variant="primary" onClick={confirmarGuardar} loading={guardando}>
              Guardar
            </Button>
          </>
        }
      >
        <Field
          label="Nombre de la configuración"
          required
          error={enviado && !nombre.trim() ? "Campo obligatorio" : undefined}
        >
          <Input
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Ej: Mis pausas"
          />
        </Field>
      </Modal>
    </div>
  );
}
