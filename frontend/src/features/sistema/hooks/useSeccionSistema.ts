import { useEffect } from "react";
import { useLocation, useNavigate } from "react-router-dom";

export type IdPestanaSistema = "estado" | "asistente" | "auditoria";

const ORDEN: readonly IdPestanaSistema[] = ["estado", "asistente", "auditoria"];

const PERMISO_POR_PESTANA: Record<IdPestanaSistema, string> = {
  estado: "sistema.estado.ver",
  asistente: "asistente.administrar",
  auditoria: "auditoria.ver",
};

function esIdPestana(valor: string): valor is IdPestanaSistema {
  return (ORDEN as readonly string[]).includes(valor);
}

export interface UseSeccionSistemaResultado {
  /** La pestaña efectivamente mostrada: siempre una de `pestanasPermitidas`. */
  pestanaActiva: IdPestanaSistema;
  /** Subconjunto de `ORDEN` que el usuario puede ver, en el orden fijo. */
  pestanasPermitidas: readonly IdPestanaSistema[];
  /** Cambia de pestaña empujando una entrada de historial (Atrás vuelve). */
  cambiarPestana: (id: IdPestanaSistema) => void;
}

/**
 * El estado de pestaña de la sección «Sistema», leído del hash de la URL
 * (design D9, sistema-seccion-unificada).
 *
 * La pestaña inicial es la del hash cuando es válida Y permitida; si no, la
 * primera permitida en el orden fijo Estado → Asistente → Auditoría. Cuando
 * tuvo que corregir, reemplaza la entrada de historial (no agrega una nueva)
 * para que la URL siempre nombre la pestaña que se ve.
 *
 * Se asume que quien llama ya tiene al menos un permiso de los tres — el
 * guard sin ninguno lo aplica `RequirePermission` en `routes.tsx`, antes de
 * montar esta sección.
 */
export function useSeccionSistema(permisos: readonly string[]): UseSeccionSistemaResultado {
  const location = useLocation();
  const navigate = useNavigate();

  const pestanasPermitidas = ORDEN.filter((id) => permisos.includes(PERMISO_POR_PESTANA[id]));
  const hash = location.hash.replace(/^#/, "");
  const hashValidoYPermitido = esIdPestana(hash) && pestanasPermitidas.includes(hash);
  const pestanaActiva = hashValidoYPermitido ? (hash as IdPestanaSistema) : pestanasPermitidas[0];

  useEffect(() => {
    if (pestanaActiva && hash !== pestanaActiva) {
      // Preserva `location.search`: un `navigate({ hash })` sin `search`
      // explícito lo resuelve como vacío (no como "el actual"), lo que
      // borraba los filtros de auditoría recién aplicados cada vez que esta
      // corrección se disparaba (bug real-app, task 11.4).
      navigate({ hash: `#${pestanaActiva}`, search: location.search }, { replace: true });
    }
    // Sólo cuando cambia lo que se ve o el hash crudo entrante; `navigate` es
    // estable entre renders de react-router.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pestanaActiva, hash]);

  function cambiarPestana(id: IdPestanaSistema) {
    if (!pestanasPermitidas.includes(id)) return;
    navigate({ hash: `#${id}`, search: location.search });
  }

  return { pestanaActiva, pestanasPermitidas, cambiarPestana };
}
