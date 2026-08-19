import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Breadcrumbs, Field, InlineAlert, Select } from "@ars-docendi/ui";
import { PedidoForm } from "../components/PedidoForm";
import { useMisPedidos, usePedido } from "../hooks/usePedidos";
import {
  useCrearPedido,
  useEditarPedido,
  useEnviarPedido,
  useReenviarPedido,
} from "../hooks/useAccionesPedido";
import { useCatalogosDesignaciones } from "../hooks/useCatalogosDesignaciones";
import { docentesDesdeCatalogo, personasDesdeCatalogo } from "../api/catalogos";
import type { DatosEditablesPedido } from "../types";

const RUTA_MIS_PEDIDOS = "/designaciones/mis-pedidos";

/** Etiqueta corta del período a partir de su id. */
function etiquetaPeriodo(periodoId: string, periodos: { id: string; nombre: string }[]): string {
  const periodo = periodos.find((item) => item.id === periodoId);
  return periodo?.nombre ?? "Período sin definir";
}

export function PedidoFormPage() {
  const { id } = useParams();
  const esEdicion = Boolean(id);
  const navegar = useNavigate();

  const { data: pedidos } = useMisPedidos();
  const catalogos = useCatalogosDesignaciones();
  const { data: pedidoInicial, isLoading, isError } = usePedido(id);
  const crear = useCrearPedido();
  const editar = useEditarPedido();
  const enviar = useEnviarPedido();
  const reenviar = useReenviarPedido();
  const [materiaSeleccionadaId, setMateriaSeleccionadaId] = useState("");
  const materiaSeleccionada =
    catalogos.data?.materias.find((materia) => materia.id === materiaSeleccionadaId) ??
    catalogos.data?.materias[0];

  function volver() {
    navegar(RUTA_MIS_PEDIDOS);
  }

  function handleGuardar(datos: DatosEditablesPedido, opciones?: { enviar?: boolean }) {
    if (esEdicion && id) {
      editar.mutate(
        {
          id,
          datos: {
            ...datos,
            version: pedidoInicial?.version,
            periodoId: pedidoInicial?.periodoId,
            personaId: pedidoInicial?.personaId,
            materiaId: pedidoInicial?.materiaId,
          },
        },
        {
          onSuccess: () => {
            if (!opciones?.enviar) {
              volver();
              return;
            }
            const mutacion = pedidoInicial?.estado === "devuelto" ? reenviar : enviar;
            mutacion.mutate(id, { onSuccess: volver });
          },
        },
      );
    } else {
      crear.mutate(
        { ...datos, materiaId: materiaSeleccionada?.id },
        {
          onSuccess: (creado) => {
            if (!opciones?.enviar) {
              volver();
              return;
            }
            enviar.mutate(creado.id, { onSuccess: volver });
          },
        },
      );
    }
  }

  const guardando = crear.isPending || editar.isPending || enviar.isPending || reenviar.isPending;
  const periodoLabel = etiquetaPeriodo(
    pedidoInicial?.periodoId ?? catalogos.data?.periodoActivo?.id ?? "",
    catalogos.data?.periodos ?? [],
  );
  const crumbEdicion = pedidoInicial?.numero ? `Editar · ${pedidoInicial.numero}` : "Editar";

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Designaciones", href: "/designaciones" },
          { label: "Mis pedidos", href: RUTA_MIS_PEDIDOS },
          { label: esEdicion ? crumbEdicion : "Nuevo pedido" },
        ]}
      />

      {esEdicion && isLoading && (
        <p style={{ color: "var(--color-text-secondary)" }}>Cargando el pedido…</p>
      )}

      {esEdicion && isError && (
        <InlineAlert severity="danger" title="No se encontró el pedido">
          No pudimos cargar el pedido solicitado.{" "}
          <a href={RUTA_MIS_PEDIDOS}>Volver a Mis pedidos</a>.
        </InlineAlert>
      )}

      {esEdicion && pedidoInicial && !pedidoInicial.accionesPermitidas?.includes("editar") && (
        <InlineAlert severity="info" title="Este pedido no es editable">
          El pedido ya fue enviado a revisión y quedó de solo lectura para el Jefe de Cátedra (salvo
          que sea devuelto). <a href={RUTA_MIS_PEDIDOS}>Volver a Mis pedidos</a>.
        </InlineAlert>
      )}

      {(crear.isError || editar.isError || enviar.isError || reenviar.isError) && (
        <InlineAlert severity="danger" title="No se pudo guardar el pedido">
          Ocurrió un error al guardar. Revisá los datos e intentá de nuevo.
        </InlineAlert>
      )}

      {!esEdicion && catalogos.data && (
        <>
          <Field label="Materia del pedido">
            <Select
              value={materiaSeleccionada?.id ?? ""}
              onChange={(event) => setMateriaSeleccionadaId(event.target.value)}
            >
              {catalogos.data.materias.map((materia) => (
                <option key={materia.id} value={materia.id}>
                  {materia.nombre}
                </option>
              ))}
            </Select>
          </Field>
          <PedidoForm
            key={materiaSeleccionada?.id}
            catedra={materiaSeleccionada?.nombre ?? ""}
            pedidosExistentes={pedidos ?? []}
            periodoLabel={periodoLabel}
            guardando={guardando}
            onGuardar={handleGuardar}
            onCancelar={volver}
            docentes={docentesDesdeCatalogo(catalogos.data)}
            personas={personasDesdeCatalogo(catalogos.data)}
            cargos={catalogos.data.cargos.map((c) => c.nombre)}
            dedicaciones={catalogos.data.dedicaciones}
            tiposBaja={catalogos.data.tiposBaja}
          />
        </>
      )}

      {esEdicion &&
        pedidoInicial &&
        pedidoInicial.accionesPermitidas?.includes("editar") &&
        catalogos.data && (
          <PedidoForm
            pedidoInicial={pedidoInicial}
            catedra={pedidoInicial.catedra}
            pedidosExistentes={pedidos ?? []}
            esEdicion
            periodoLabel={periodoLabel}
            guardando={guardando}
            onGuardar={handleGuardar}
            onCancelar={volver}
            docentes={docentesDesdeCatalogo(catalogos.data)}
            personas={personasDesdeCatalogo(catalogos.data)}
            cargos={catalogos.data.cargos.map((c) => c.nombre)}
            dedicaciones={catalogos.data.dedicaciones}
            tiposBaja={catalogos.data.tiposBaja}
          />
        )}
    </>
  );
}
