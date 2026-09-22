import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { TablaTareas } from "../components/TablaTareas";
import { ModalNuevaTarea } from "../components/ModalNuevaTarea";
import { IconoPlus } from "../components/lucide";
import { puedeCrearTarea } from "../api/maquinaEstadosTarea";
import { useActorTareas } from "../hooks/useActorTareas";
import { useListadoTareas } from "../hooks/useTareas";
import { useCrearTarea } from "../hooks/useAccionesTarea";
import type { Tarea } from "../types";

export function IndexPage() {
  const navegar = useNavigate();
  const actor = useActorTareas();
  const { data: tareas, isLoading, isError } = useListadoTareas();
  const crear = useCrearTarea(actor);

  const [modalNuevaAbierto, setModalNuevaAbierto] = useState(false);

  const total = tareas?.length ?? 0;

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Tareas" }]} />
      <PageHeader
        pretitle="Cuatrimestre 2026 · 1C"
        title="Tareas"
        meta={isLoading ? "Cargando…" : `${total} tarea${total !== 1 ? "s" : ""}`}
        actions={
          puedeCrearTarea(actor) ? (
            <Button
              variant="primary"
              leadingIcon={<IconoPlus />}
              onClick={() => setModalNuevaAbierto(true)}
            >
              Nueva Tarea
            </Button>
          ) : undefined
        }
      />

      {isLoading && <p style={{ color: "var(--color-text-secondary)" }}>Cargando las tareas…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar las tareas">
          Hubo un problema al obtener el listado de tareas. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && total === 0 && (
        <InlineAlert severity="info" title="Todavía no hay tareas cargadas">
          {puedeCrearTarea(actor)
            ? 'Empezá creando la primera tarea con "Nueva Tarea".'
            : "Cuando una autoridad cree una tarea, la vas a ver acá."}
        </InlineAlert>
      )}

      {!isLoading && !isError && total > 0 && tareas && (
        <TablaTareas
          tareas={tareas}
          onSeleccionar={(tarea: Tarea) => navegar(`/tareas/${tarea.id}`)}
        />
      )}

      <ModalNuevaTarea
        open={modalNuevaAbierto}
        onCerrar={() => setModalNuevaAbierto(false)}
        onGuardar={(datos) => {
          crear.mutate(datos, { onSuccess: () => setModalNuevaAbierto(false) });
        }}
        guardando={crear.isPending}
        error={crear.isError ? crear.error.message : undefined}
      />
    </>
  );
}
