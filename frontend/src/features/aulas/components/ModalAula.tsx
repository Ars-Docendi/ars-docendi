import { useState } from "react";
import { Button, Field, InlineAlert, Input, Select } from "@ars-docendi/ui";
import { ModalDeFormulario } from "../../../shared/ui/ModalDeFormulario";
import type { Aula, DatosAula, TipoAula } from "../types";

interface ModalAulaProps {
  /** Sin aula, el modal da de alta una nueva. */
  aula?: Aula;
  guardando: boolean;
  error?: string;
  onCerrar: () => void;
  onGuardar: (datos: DatosAula) => void;
}

/** Alta y edición de un aula o laboratorio. Las reglas las valida también el backend. */
export function ModalAula({ aula, guardando, error, onCerrar, onGuardar }: ModalAulaProps) {
  const [nombre, setNombre] = useState(aula?.nombre ?? "");
  const [tipo, setTipo] = useState<TipoAula>(aula?.tipo ?? "aula");
  const [capacidad, setCapacidad] = useState(aula ? String(aula.capacidad) : "");
  const [equipamiento, setEquipamiento] = useState(aula?.equipamiento ?? "");
  const [intentado, setIntentado] = useState(false);

  const capacidadNumero = Number(capacidad);
  const errores = {
    nombre: nombre.trim() ? undefined : "Indicá el nombre.",
    capacidad:
      Number.isInteger(capacidadNumero) && capacidadNumero > 0
        ? undefined
        : "Indicá una capacidad mayor a cero.",
  };
  const sucio =
    nombre !== (aula?.nombre ?? "") ||
    tipo !== (aula?.tipo ?? "aula") ||
    capacidad !== (aula ? String(aula.capacidad) : "") ||
    equipamiento !== (aula?.equipamiento ?? "");

  function enviar() {
    setIntentado(true);
    if (errores.nombre || errores.capacidad) return;
    onGuardar({
      nombre: nombre.trim(),
      tipo,
      capacidad: capacidadNumero,
      equipamiento: equipamiento.trim() || undefined,
      version: aula?.version,
    });
  }

  return (
    <ModalDeFormulario
      titulo={aula ? "Editar aula" : "Nueva aula"}
      tamano="formulario"
      onCerrar={onCerrar}
      pendiente={guardando}
      sucio={sucio}
      alEnviar={enviar}
      pie={
        <>
          <Button variant="secondary" onClick={onCerrar} disabled={guardando}>
            Volver
          </Button>
          <Button type="submit" variant="primary" loading={guardando}>
            {aula ? "Guardar cambios" : "Crear aula"}
          </Button>
        </>
      }
    >
      {error && (
        <InlineAlert severity="danger" title="No se pudo guardar el aula">
          {error}
        </InlineAlert>
      )}
      <Field label="Nombre" required error={intentado ? errores.nombre : undefined}>
        <Input
          id="aula-nombre"
          value={nombre}
          placeholder="Ej: Aula 204"
          onChange={(e) => setNombre(e.target.value)}
        />
      </Field>
      <Field label="Tipo" required>
        <Select id="aula-tipo" value={tipo} onChange={(e) => setTipo(e.target.value as TipoAula)}>
          <option value="aula">Aula</option>
          <option value="laboratorio">Laboratorio</option>
        </Select>
      </Field>
      <Field label="Capacidad" required error={intentado ? errores.capacidad : undefined}>
        <Input
          id="aula-capacidad"
          type="number"
          min={1}
          inputMode="numeric"
          value={capacidad}
          onChange={(e) => setCapacidad(e.target.value)}
        />
      </Field>
      <Field label="Equipamiento">
        <Input
          id="aula-equipamiento"
          value={equipamiento}
          placeholder="Ej: proyector, 30 PC"
          onChange={(e) => setEquipamiento(e.target.value)}
        />
      </Field>
    </ModalDeFormulario>
  );
}
