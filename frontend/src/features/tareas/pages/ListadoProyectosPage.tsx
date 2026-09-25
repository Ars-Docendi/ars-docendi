import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { ModalProyecto } from "../components/ModalProyecto";
import { TablaProyectos } from "../components/TablaProyectos";
import { IconoPlus } from "../components/lucide";
import { usePermisosTareas } from "../hooks/useActorTareas";
import { useEstadosProyecto, useListadoProyectos } from "../hooks/useProyectos";
import { useCrearProyecto } from "../hooks/useAccionesProyecto";

const RUTA_TAREAS = "/tareas";

/**
 * Pestaña Proyectos: listado con TODOS los proyectos, sin importar su Estado — es
 * también la vía de acceso a los Finalizados/Cancelados, que no tienen cuadro en la
 * pantalla inicial (esa solo cubre los Abiertos). Usa `TablaProyectos`: filtros por columna
 * y orden por estado y luego fecha de inicio. Quien gestiona proyectos ve además el botón
 * "Nuevo Proyecto".
 */
export function ListadoProyectosPage() {
  const navegar = useNavigate();
  const { data: proyectos, isLoading, isError } = useListadoProyectos();
  const { data: estados = [] } = useEstadosProyecto();
  const { puedeGestionarProyectos } = usePermisosTareas();
  const crearProyecto = useCrearProyecto();
  const [modalAbierto, setModalAbierto] = useState(false);

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Tareas", href: RUTA_TAREAS },
          { label: "Proyectos" },
        ]}
      />

      <PageHeader
        pretitle="Tareas"
        title="Proyectos"
        meta={isLoading ? "Cargando…" : `${proyectos?.length ?? 0} proyecto(s)`}
        actions={
          puedeGestionarProyectos ? (
            <Button
              variant="primary"
              leadingIcon={<IconoPlus />}
              onClick={() => setModalAbierto(true)}
            >
              Nuevo Proyecto
            </Button>
          ) : undefined
        }
      />

      {isLoading && (
        <p role="status" aria-live="polite" style={{ color: "var(--color-text-secondary)" }}>
          Cargando los proyectos…
        </p>
      )}

      {isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar los proyectos">
          Hubo un problema al obtener el listado de proyectos. Recargá la página para reintentar.
        </InlineAlert>
      )}

      {!isLoading && !isError && proyectos && (
        <TablaProyectos
          proyectos={proyectos}
          estados={estados}
          onSeleccionar={(proyecto) => navegar(`/tareas/proyectos/${proyecto.id}`)}
        />
      )}

      <ModalProyecto
        open={modalAbierto}
        onCerrar={() => setModalAbierto(false)}
        onGuardar={(datos) => {
          crearProyecto.mutate(datos, { onSuccess: () => setModalAbierto(false) });
        }}
        guardando={crearProyecto.isPending}
        error={crearProyecto.isError ? crearProyecto.error.message : undefined}
      />
    </>
  );
}
