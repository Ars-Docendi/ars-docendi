import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

import { TerminosPage } from "../legal/TerminosPage";
import { LoginPage } from "./LoginPage";

vi.mock("./developmentAuth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./developmentAuth")>()),
  developmentAuthEnabled: false,
}));
vi.mock("./sesionMicrosoft", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./sesionMicrosoft")>()),
  microsoftLoginEnabled: true,
  obtenerSesion: vi.fn().mockResolvedValue(null),
}));

function renderizar(ruta: string) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/terminos" element={<TerminosPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("pantalla de ingreso", () => {
  it("da la bienvenida e indica la cuenta institucional y la acción principal", async () => {
    renderizar("/login");

    expect(await screen.findByText(/Te damos la bienvenida a/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Ingresá a tu cuenta" })).toBeInTheDocument();
    expect(screen.getByText(/@docentes\.unlam\.edu\.ar/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ingresar con Microsoft" })).toBeInTheDocument();
    for (const descripcion of [
      "designaciones",
      "reservar aulas",
      "tareas",
      "perfil docente",
      "asistente conversacional",
    ]) {
      expect(screen.getByText(new RegExp(descripcion))).toBeInTheDocument();
    }
  });

  it("no muestra canales de contacto inexistentes ni un bloque de ayuda", async () => {
    renderizar("/login");

    await screen.findByRole("button", { name: "Ingresar con Microsoft" });
    expect(screen.queryByText(/@unlam\.edu\.ar(?!\S)/)).not.toBeInTheDocument();
    expect(screen.queryByText(/soporte/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/¿No podés ingresar\?/)).not.toBeInTheDocument();
    expect(document.querySelector('a[href^="mailto:"]')).toBeNull();
  });

  it("ante un error de autenticación remite a Secretaría Académica", async () => {
    renderizar("/login?error=auth");

    const aviso = await screen.findByText("No pudimos confirmar tu identidad.");
    expect(aviso.closest("[role]") ?? document.body).toHaveTextContent(
      "comunicate con Secretaría Académica",
    );
  });

  it("el pie lleva a los términos de uso y privacidad sin pedir sesión", async () => {
    renderizar("/login");

    await userEvent.click(
      await screen.findByRole("link", { name: "Términos de uso y la Política de privacidad" }),
    );

    expect(
      screen.getByRole("heading", { name: "Términos de uso y política de privacidad" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Agencia de Acceso a la Información Pública/)).toBeInTheDocument();
    expect(screen.getByText(/imágenes del documento de identidad/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "8. Tus derechos" })).toBeInTheDocument();
  });
});
