import { RequirePermission } from "../../shared/auth/RequirePermission";

export const routes = {
  element: <RequirePermission permission="roles.ver" />,
  children: [
    {
      path: "/roles",
      lazy: () =>
        import("./pages/ListadoPage").then(({ ListadoPage }) => ({ Component: ListadoPage })),
    },
    {
      // Alta: sólo quien administra roles (el backend lo exige igual al guardar).
      element: <RequirePermission permission="roles.administrar" />,
      children: [
        {
          path: "/roles/nuevo",
          lazy: () =>
            import("./pages/EditorPage").then(({ EditorPage }) => ({ Component: EditorPage })),
        },
      ],
    },
    {
      path: "/roles/:rolId",
      lazy: () =>
        import("./pages/EditorPage").then(({ EditorPage }) => ({ Component: EditorPage })),
    },
  ],
};
