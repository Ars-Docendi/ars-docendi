import type { ComponentType, ReactNode } from "react";
import { Navigate, type RouteObject } from "react-router-dom";

import { RequirePermission } from "../../shared/auth/RequirePermission";

const PERMISOS_SISTEMA = ["sistema.estado.ver", "asistente.administrar", "auditoria.ver"] as const;

interface CrearRutasProps {
  /** El panel de uso del asistente, compuesto por `app/router.tsx` (design D8). */
  PanelAsistente: ComponentType<{ actualizacion?: number }>;
  /**
   * La pestaña «Modelo», también compuesta por `app/router.tsx`: recibe como
   * `cambios` la lista de cambios del modelo que arma esta feature.
   */
  PanelModelo: ComponentType<{ cambios?: ReactNode }>;
  /** La pestaña «Calidad», compuesta desde `app/router.tsx` como las otras dos. */
  PanelCalidad: ComponentType;
  /**
   * Hook que dice si la tasa de respuestas negativas sube; pone en advertencia el
   * punto de estado de la pestaña «Calidad». Llega por acá por la misma razón.
   */
  useCalidadEnAlza: (habilitado: boolean) => boolean;
}

/**
 * `crearRutas` en vez de una constante (design D8, sistema-seccion-
 * unificada): `features/sistema` nunca importa `features/asistente` — el
 * componente del panel de uso llega como parámetro desde `app/router.tsx`,
 * la raíz de composición autorizada a importar ambos barrels.
 */
export function crearRutas({
  PanelAsistente,
  PanelModelo,
  PanelCalidad,
  useCalidadEnAlza,
}: CrearRutasProps): RouteObject[] {
  return [
    {
      element: <RequirePermission permission={PERMISOS_SISTEMA} />,
      children: [
        {
          path: "/sistema",
          lazy: () =>
            import("./pages/SistemaPage").then(({ SistemaPage }) => ({
              Component: () => (
                <SistemaPage
                  PanelAsistente={PanelAsistente}
                  PanelModelo={PanelModelo}
                  PanelCalidad={PanelCalidad}
                  useCalidadEnAlza={useCalidadEnAlza}
                />
              ),
            })),
        },
      ],
    },
    {
      path: "/auditoria",
      element: <Navigate to={{ pathname: "/sistema", hash: "#auditoria" }} replace />,
    },
  ];
}
