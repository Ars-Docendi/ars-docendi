import { useState } from "react";
import { Button, Field, InlineAlert, Input, Select } from "@ars-docendi/ui";
import { CampoFecha } from "../../../shared/ui/CampoFecha";
import { CampoHora } from "../../../shared/ui/CampoHora";
import { fechaTextoAISO } from "../../../shared/ui/camposFechaHora";
import { ModalDeFormulario } from "../../../shared/ui/ModalDeFormulario";
import "../../../shared/ui/formulario.css";
import { useCrearSolicitud } from "../hooks/useAccionesSolicitud";
import { useMateriasPropias } from "../hooks/useSolicitudesAula";
import { AlertaConReintento } from "./AlertaConReintento";
import {
  CAMPOS_EN_ORDEN,
  FORMULARIO_VACIO,
  hayCambios,
  validarNuevaSolicitud,
  type FormularioNuevaSolicitud,
} from "./validacionNuevaSolicitud";

interface ModalNuevaSolicitudProps {
  onCerrar: () => void;
}

/**
 * Formulario para generar una solicitud de reserva de aula (Docente). Se monta sólo mientras está
 * abierto y la mutación vive acá: los campos y un error viejo del servidor no sobreviven a un
 * cierre, así que cada apertura empieza vacía. La Materia es un desplegable acotado a las materias
 * asignadas al docente; el backend vuelve a validar el `materiaId`.
 */
export function ModalNuevaSolicitud({ onCerrar }: ModalNuevaSolicitudProps) {
  const materias = useMateriasPropias();
  const crear = useCrearSolicitud();
  const [formulario, setFormulario] = useState<FormularioNuevaSolicitud>(FORMULARIO_VACIO);
  const [enviado, setEnviado] = useState(false);

  const guardando = crear.isPending;
  const errores = validarNuevaSolicitud(formulario);
  const sinMaterias = materias.isSuccess && materias.data.length === 0;
  const error = (campo: keyof FormularioNuevaSolicitud) => (enviado ? errores[campo] : undefined);

  function actualizar(campo: keyof FormularioNuevaSolicitud, valor: string) {
    setFormulario((previo) => ({ ...previo, [campo]: valor }));
  }

  function guardar() {
    if (guardando) return;
    setEnviado(true);
    const primero = CAMPOS_EN_ORDEN.find(([campo]) => errores[campo]);
    if (primero) {
      document.getElementById(primero[1])?.focus();
      return;
    }
    crear.mutate(
      {
        dia: fechaTextoAISO(formulario.dia) as string,
        horarioDesde: formulario.horarioDesde,
        horarioHasta: formulario.horarioHasta,
        cantidadAlumnosAprox: Number(formulario.cantidadAlumnosAprox),
        materiaId: formulario.materiaId,
        comision: formulario.comision.trim(),
      },
      { onSuccess: onCerrar },
    );
  }

  return (
    <ModalDeFormulario
      titulo="Nueva solicitud de reserva de aula"
      tamano="formulario"
      onCerrar={onCerrar}
      pendiente={guardando}
      sucio={hayCambios(formulario)}
      alEnviar={guardar}
      claveFoco={materias.isLoading}
      pie={
        <>
          <Button id="nueva-cancelar" variant="secondary" onClick={onCerrar} disabled={guardando}>
            Cancelar
          </Button>
          <Button
            id="nueva-guardar"
            type="submit"
            variant="primary"
            loading={guardando}
            disabled={sinMaterias}
          >
            Generar solicitud
          </Button>
        </>
      }
    >
      <div className="adoc-form">
        {crear.isError && (
          <InlineAlert severity="danger" title="No se pudo generar la solicitud">
            {crear.error.message}
          </InlineAlert>
        )}
        {materias.isError && (
          <AlertaConReintento
            id="nueva-materias-reintentar"
            titulo="No se pudieron cargar tus materias"
            onReintentar={() => void materias.refetch()}
          >
            Hubo un problema al obtener tus materias asignadas.
          </AlertaConReintento>
        )}
        {sinMaterias && (
          <InlineAlert severity="info" title="No tenés materias asignadas">
            No podés generar una solicitud sin al menos una materia a tu cargo.
          </InlineAlert>
        )}

        <div className="adoc-form-seccion" role="group" aria-labelledby="nueva-sec-examen">
          <h5 id="nueva-sec-examen" className="adoc-form-seccion-titulo">
            Examen
          </h5>
          <div className="adoc-form-fila cols-2-1">
            <Field label="Materia" required error={error("materiaId")}>
              <Select
                id="nueva-materia"
                value={formulario.materiaId}
                disabled={materias.isLoading || sinMaterias}
                onChange={(e) => actualizar("materiaId", e.target.value)}
              >
                <option value="">
                  {materias.isLoading ? "Cargando materias…" : "Seleccioná una materia…"}
                </option>
                {materias.data?.map((materia) => (
                  <option key={materia.id} value={materia.id}>
                    {materia.nombre}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Comisión" required error={error("comision")}>
              <Input
                id="nueva-comision"
                value={formulario.comision}
                placeholder="Ej: K3001"
                onChange={(e) => actualizar("comision", e.target.value)}
              />
            </Field>
          </div>
        </div>

        <div className="adoc-form-seccion" role="group" aria-labelledby="nueva-sec-fecha">
          <h5 id="nueva-sec-fecha" className="adoc-form-seccion-titulo">
            Fecha y horario
          </h5>
          <div className="adoc-form-fila cols-3 horario-compartido">
            <Field label="Día" required error={error("dia")}>
              <CampoFecha
                id="nueva-dia"
                value={formulario.dia}
                onChange={(valor) => actualizar("dia", valor)}
              />
            </Field>
            <Field label="Horario desde" required error={error("horarioDesde")}>
              <CampoHora
                id="nueva-desde"
                value={formulario.horarioDesde}
                onChange={(valor) => actualizar("horarioDesde", valor)}
              />
            </Field>
            <Field label="Horario hasta" required error={error("horarioHasta")}>
              <CampoHora
                id="nueva-hasta"
                value={formulario.horarioHasta}
                onChange={(valor) => actualizar("horarioHasta", valor)}
              />
            </Field>
          </div>
        </div>

        <div className="adoc-form-seccion" role="group" aria-labelledby="nueva-sec-capacidad">
          <h5 id="nueva-sec-capacidad" className="adoc-form-seccion-titulo">
            Capacidad
          </h5>
          <Field
            label="Cantidad aproximada de alumnos"
            required
            error={error("cantidadAlumnosAprox")}
            className="adoc-campo-corto"
          >
            <Input
              id="nueva-alumnos"
              type="number"
              inputMode="numeric"
              min={1}
              value={formulario.cantidadAlumnosAprox}
              onChange={(e) => actualizar("cantidadAlumnosAprox", e.target.value)}
            />
          </Field>
        </div>
      </div>
    </ModalDeFormulario>
  );
}
