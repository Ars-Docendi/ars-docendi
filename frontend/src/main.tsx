import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import "@ars-docendi/ui/theme.css";
import "@ars-docendi/ui/components.css";
import "./index.css";
import "./tema-oscuro.css";
import { router } from "./app/router";
import { iniciarTema } from "./shared/tema/tema";

iniciarTema();

const queryClient = new QueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
