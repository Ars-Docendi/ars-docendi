import { useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Button, InlineAlert } from "@ars-docendi/ui";
import { useFiltrosURL, useOrdenURL } from "../../../shared/navegacion/estadoURL";
import { Migas } from "../../../shared/navegacion/Migas";
import { useAbrirDetalle } from "../../../shared/navegacion/useEstadoVolver";

import { mensajeProblema } from "../../../shared/api/problemDetails";
import { PageHeader } from "../../../shared/ui/PageHeader";
import type { Persona } from "../api/personasApi";
import { TablaPersonas } from "../components/TablaPersonas";
import {
  COLUMNAS_ORDEN_PERSONAS,
  FILTROS_VACIOS,
  filtrarPersonas,
  nombreCompleto,
  opcionesRol,
  ordenarPersonas,
  siguienteOrden,
} from "../filtrosPersonas";
import { usePermisosPersonas, usePersonas } from "../hooks/usePersonas";
import { Modal } from "../../../shared/ui/Modal";

export function ListadoPage() {
  const navegar = useNavigate();
  const abrir = useAbrirDetalle();
  const aviso = (useLocation().state as { aviso?: string } | null)?.aviso;
  const { administrar } = usePermisosPersonas();
  const { consulta, cambiarEstado } = usePersonas();
  const [filtros, setFiltros] = useFiltrosURL(FILTROS_VACIOS);
  const [orden, setOrden] = useOrdenURL(COLUMNAS_ORDEN_PERSONAS);
  const [aConfirmar, setAConfirmar] = useState<Persona | null>(null);

  const personas = useMemo(() => consulta.data ?? [], [consulta.data]);
  const visibles = useMemo(
    () => ordenarPersonas(filtrarPersonas(personas, filtros), orden),
    [personas, filtros, orden],
  );
  const activas = personas.filter((p) => p.activo).length;
  const conteo =
    visibles.length === personas.length
      ? `${personas.length} personas`
      : `${visibles.length} de ${personas.length} personas`;
  const activar = aConfirmar ? !aConfirmar.activo : false;

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Personas" }]} />
      <PageHeader
        title="Administrar personas"
        meta={`${conteo} · ${activas} activas`}
        actions={
          administrar && (
            <Button variant="primary" onClick={() => navegar("/personas/nueva")}>
              Nueva persona
            </Button>
          )
        }
      />
      {aviso && <InlineAlert severity="success" title={aviso} />}
      {consulta.isError && (
        <InlineAlert severity="danger" title="No se pudieron cargar las personas">
          Reintentá en unos segundos.
        </InlineAlert>
      )}
      {cambiarEstado.isError && (
        <InlineAlert
          severity="danger"
          title={mensajeProblema(cambiarEstado.error, "No se pudo cambiar el estado.")}
        />
      )}
      {consulta.isLoading ? (
        <p role="status">Cargando personas…</p>
      ) : (
        <TablaPersonas
          personas={visibles}
          opcionesRol={opcionesRol(personas)}
          filtros={filtros}
          orden={orden}
          onFiltrosChange={setFiltros}
          onOrdenar={(columna) => setOrden(siguienteOrden(orden, columna))}
          onAbrir={(p) => abrir(`/personas/${p.personaId}`)}
          onCambiarEstado={administrar ? setAConfirmar : undefined}
          textoAbrir={administrar ? "Editar" : "Ver"}
        />
      )}

      <Modal
        open={aConfirmar !== null}
        tamano="confirmacion"
        pendiente={cambiarEstado.isPending}
        onOpenChange={(abierto) => !abierto && setAConfirmar(null)}
        title={activar ? "Activar persona" : "Desactivar persona"}
        footer={
          <>
            <Button variant="secondary" onClick={() => setAConfirmar(null)}>
              Cancelar
            </Button>
            <Button
              variant={activar ? "primary" : "destructive"}
              loading={cambiarEstado.isPending}
              onClick={() =>
                aConfirmar &&
                cambiarEstado.mutate(
                  { persona: aConfirmar, activar },
                  { onSuccess: () => setAConfirmar(null) },
                )
              }
            >
              {activar ? "Activar" : "Desactivar"}
            </Button>
          </>
        }
      >
        {aConfirmar && (
          <p>
            ¿Seguro que querés {activar ? "activar" : "desactivar"} a{" "}
            <strong>{nombreCompleto(aConfirmar)}</strong>?{" "}
            {activar
              ? "Va a poder volver a ingresar con sus roles."
              : "No va a poder ingresar al sistema; sus datos se conservan."}
          </p>
        )}
      </Modal>
    </>
  );
}
