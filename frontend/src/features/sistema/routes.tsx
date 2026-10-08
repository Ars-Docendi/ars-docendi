import type { ComponentType } from "react";
import { Navigate, type RouteObject } from "react-router-dom";

import { RequirePermission } from "../../shared/auth/RequirePermission";

const PERMISOS_SISTEMA = ["sistema.estado.ver", "asistente.administrar", "auditoria.ver"] as const;

interface CrearRutasProps {
  /** El panel de uso del asistente, compuesto por `app/router.tsx` (design D8). */
  PanelAsistente: ComponentType<{ actualizacion?: number }>;
}

/**
 * `crearRutas` en vez de una constante (design D8, sistema-seccion-
 * unificada): `features/sistema` nunca importa `features/asistente` — el
 * componente del panel de uso llega como parámetro desde `app/router.tsx`,
 * la raíz de composición autorizada a importar ambos barrels.
 */
export function crearRutas({ PanelAsistente }: CrearRutasProps): RouteObject[] {
  return [
    {
      element: <RequirePermission permission={PERMISOS_SISTEMA} />,
      children: [
        {
          path: "/sistema",
          lazy: () =>
            import("./pages/SistemaPage").then(({ SistemaPage }) => ({
              Component: () => <SistemaPage PanelAsistente={PanelAsistente} />,
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
