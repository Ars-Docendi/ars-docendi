import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";

/**
 * Estado de una pantalla en el query string: filtros, orden y pestañas. Los valores por defecto
 * no se escriben, así la URL de una lista sin tocar queda limpia, y recargar o volver restaura el
 * estado. Dos tablas en una misma página usan prefijos (`mias.`, `todas.`) para no pisarse.
 */
type Valor = string | string[];

/** Cuánto sostiene el eco local si la URL no llega a coincidir con él. */
const ECO_MAXIMO_MS = 1000;

function clave(prefijo: string | undefined, nombre: string) {
  return prefijo ? `${prefijo}.${nombre}` : nombre;
}

function iguales(a: Valor, b: Valor) {
  return Array.isArray(a) && Array.isArray(b) ? a.join(",") === b.join(",") : a === b;
}

/**
 * Filtros como un objeto plano de `string` y `string[]` (los arreglos van separados por coma).
 * `actualizar` recibe el estado completo nuevo y reemplaza la entrada del historial: escribir en
 * un filtro de texto no debe llenar el botón Atrás.
 */
export function useFiltrosURL<T extends { [K in keyof T]: Valor }>(
  porDefecto: T,
  prefijo?: string,
) {
  const [params, setParams] = useSearchParams();
  const lectura = params.toString();

  const desdeURL = useMemo(() => {
    const actuales = new URLSearchParams(lectura);
    const resultado: Record<string, Valor> = {};
    for (const [nombre, defecto] of Object.entries(porDefecto) as [string, Valor][]) {
      const crudo = actuales.get(clave(prefijo, nombre));
      if (crudo === null) resultado[nombre] = defecto;
      else resultado[nombre] = Array.isArray(defecto) ? crudo.split(",").filter(Boolean) : crudo;
    }
    return resultado as T;
  }, [lectura, porDefecto, prefijo]);

  // Eco local de lo último que escribió el usuario: la URL se actualiza con una navegación
  // asíncrona y, si el campo esperara a que vuelva, las teclas siguientes se pisarían y se
  // perderían letras. El eco se suelta cuando la URL lo alcanza (o, si la URL tomó otro rumbo
  // —Atrás, un enlace— tras una breve espera).
  const [eco, setEco] = useState<T | null>(null);
  const alcanzado = eco !== null && JSON.stringify(eco) === JSON.stringify(desdeURL);
  const filtros = eco !== null && !alcanzado ? eco : desdeURL;
  useEffect(() => {
    if (eco === null) return;
    const reloj = window.setTimeout(() => setEco(null), alcanzado ? 0 : ECO_MAXIMO_MS);
    return () => window.clearTimeout(reloj);
  }, [eco, alcanzado]);

  const actualizar = useCallback(
    (nuevos: T) => {
      setEco(nuevos);
      setParams(
        (previos) => {
          const siguientes = new URLSearchParams(previos);
          for (const [nombre, defecto] of Object.entries(porDefecto) as [string, Valor][]) {
            const valor = (nuevos as Record<string, Valor>)[nombre];
            const k = clave(prefijo, nombre);
            const vacio = Array.isArray(valor) ? valor.length === 0 : valor === "";
            if (vacio || iguales(valor, defecto)) siguientes.delete(k);
            else siguientes.set(k, Array.isArray(valor) ? valor.join(",") : valor);
          }
          return siguientes;
        },
        { replace: true },
      );
    },
    [porDefecto, prefijo, setParams],
  );

  return [filtros, actualizar] as const;
}

export interface OrdenURL<C extends string> {
  columna: C;
  direccion: "asc" | "desc";
}

/** Orden como `columna:direccion`; una columna que no existe se ignora. */
export function useOrdenURL<C extends string>(columnas: readonly C[], prefijo?: string) {
  const [params, setParams] = useSearchParams();
  const k = clave(prefijo, "orden");
  const crudo = params.get(k);

  const orden = useMemo<OrdenURL<C> | null>(() => {
    if (!crudo) return null;
    const [columna, direccion] = crudo.split(":");
    if (!columnas.includes(columna as C)) return null;
    return { columna: columna as C, direccion: direccion === "desc" ? "desc" : "asc" };
  }, [crudo, columnas]);

  const actualizar = useCallback(
    (nuevo: OrdenURL<C> | null) => {
      setParams(
        (previos) => {
          const siguientes = new URLSearchParams(previos);
          if (nuevo) siguientes.set(k, `${nuevo.columna}:${nuevo.direccion}`);
          else siguientes.delete(k);
          return siguientes;
        },
        { replace: true },
      );
    },
    [k, setParams],
  );

  return [orden, actualizar] as const;
}

/**
 * Una pestaña o selección única en el query. Por defecto cambiar de pestaña agrega una entrada
 * al historial (Atrás vuelve a la pestaña anterior); `reemplazar` la evita para vistas de un
 * mismo formulario.
 */
export function usePestanaURL<T extends string>(
  validas: readonly T[],
  porDefecto: T,
  opciones: { nombre?: string; prefijo?: string; reemplazar?: boolean } = {},
) {
  const { nombre = "pestania", prefijo, reemplazar = false } = opciones;
  const [params, setParams] = useSearchParams();
  const k = clave(prefijo, nombre);
  const crudo = params.get(k);
  const pestana = validas.includes(crudo as T) ? (crudo as T) : porDefecto;

  const elegir = useCallback(
    (id: T) => {
      setParams(
        (previos) => {
          const siguientes = new URLSearchParams(previos);
          if (id === porDefecto) siguientes.delete(k);
          else siguientes.set(k, id);
          return siguientes;
        },
        { replace: reemplazar },
      );
    },
    [k, porDefecto, reemplazar, setParams],
  );

  return [pestana, elegir] as const;
}
