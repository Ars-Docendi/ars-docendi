import { RequirePermission } from "../../shared/auth/RequirePermission";
import { RedireccionPersonas } from "./pages/RedireccionPersonas";

export const routes = {
  element: <RequirePermission permission={["personas.ver", "personas.ver_ambito"]} />,
  children: [
    {
      path: "/personas",
      lazy: () =>
        import("./pages/ListadoPage").then(({ ListadoPage }) => ({ Component: ListadoPage })),
    },
    {
      // Alta: sólo quien administra personas (el backend lo exige igual al guardar).
      element: <RequirePermission permission="personas.administrar" />,
      children: [
        {
          path: "/personas/nueva",
          lazy: () =>
            import("./pages/EditorPage").then(({ EditorPage }) => ({ Component: EditorPage })),
        },
      ],
    },
    {
      path: "/personas/:personaId",
      lazy: () =>
        import("./pages/EditorPage").then(({ EditorPage }) => ({ Component: EditorPage })),
    },
  ],
};

export const redireccionesAnteriores = [
  { path: "/usuarios", element: <RedireccionPersonas /> },
  { path: "/docentes", element: <RedireccionPersonas /> },
];
