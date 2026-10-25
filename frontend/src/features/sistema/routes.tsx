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
}

/**
 * `crearRutas` en vez de una constante (design D8, sistema-seccion-
 * unificada): `features/sistema` nunca importa `features/asistente` — el
 * componente del panel de uso llega como parámetro desde `app/router.tsx`,
 * la raíz de composición autorizada a importar ambos barrels.
 */
export function crearRutas({ PanelAsistente, PanelModelo }: CrearRutasProps): RouteObject[] {
  return [
    {
      element: <RequirePermission permission={PERMISOS_SISTEMA} />,
      children: [
        {
          path: "/sistema",
          lazy: () =>
            import("./pages/SistemaPage").then(({ SistemaPage }) => ({
              Component: () => (
                <SistemaPage PanelAsistente={PanelAsistente} PanelModelo={PanelModelo} />
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
