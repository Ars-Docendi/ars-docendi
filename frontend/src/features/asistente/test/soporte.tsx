import type { ReactNode } from "react";
import { render } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { CapacidadesDelAsistente, RespuestaDelAsistente } from "../types";

// ============================================================
// Lo que comparten los archivos de test de la feature.
//
// `asistente.test.tsx` ya roza el cap de ~300 líneas, así que cada componente o
// hook nuevo se prueba en un archivo hermano. Todos montan igual —un
// `QueryClientProvider` propio, sin reintentos ni caché— y usan las mismas
// fixtures, para que un test no pase por una diferencia de montaje que otro no
// tiene.
// ============================================================

export const CAPACIDADES: CapacidadesDelAsistente = {
  // `descripcion` llega como el backend la manda de verdad: el `COMMENT ON TABLE`
  // que se escribió para el modelo, con nombres de tablas y «NO confundir con…».
  // La fixture lo trae así a propósito para que muerda cualquier test que monte
  // el panel vacío si alguien lo pinta.
  cubre: [
    {
      nombre: "designaciones.pedidos",
      descripcion:
        "Pedidos de novedad docente. Aprobado produce una fila en designaciones.designaciones.",
      columnas: 12,
    },
    {
      nombre: "identity.personas",
      descripcion: "Padrón de personas (identity.users). NO confundir con identity.roles.",
      columnas: 5,
    },
  ],
  tablas: 2,
  columnas: 17,
  ejemplos: ["¿Qué carreras están vigentes?", "¿Cuántos pedidos hay en cada estado?"],
  noPuede: ["No modifica nada: solo consulta."],
  alcance: "Ves los datos de todo el Departamento.",
};

export function respuesta(parcial: Partial<RespuestaDelAsistente> = {}): RespuestaDelAsistente {
  return {
    estado: "respondida",
    respuesta: "Hay 4 docentes designados.",
    hilo: "11111111-1111-4111-8111-111111111111",
    opciones: [],
    sugerencias: [],
    columnas: [],
    filas: [],
    truncado: false,
    metricas: { llamadasAlModelo: 2 },
    ...parcial,
  };
}

/** `contenedor`: dónde montar, ya colgado del `body`; sin él, RTL crea uno. */
export function montar(nodo: ReactNode, contenedor?: HTMLElement) {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });

  return render(
    <QueryClientProvider client={cliente}>{nodo}</QueryClientProvider>,
    contenedor && { container: contenedor },
  );
}
