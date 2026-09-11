import type { RouteObject } from "react-router-dom";

export const routes: RouteObject = {
  path: "portal",
  children: [
    {
      index: true,
      lazy: () => import("./pages/IndexPage").then(({ IndexPage }) => ({ Component: IndexPage })),
    },
  ],
};
