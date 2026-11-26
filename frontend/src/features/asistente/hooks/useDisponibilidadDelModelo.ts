import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { activarModelo, obtenerDisponibilidadDelModelo } from "../api/asistenteApi";

/** Lo que el chat muestra del modelo. `oculto` es el caso normal. */
export type AvisoDelModelo = "oculto" | "en_reposo" | "activando" | "listo" | "fallo";

/** Lo mismo que cachea el servidor: preguntar más seguido sólo gasta el presupuesto compartido. */
const VIGENCIA_MS = 30_000;
/** Espera creciente entre consultas mientras se activa; la última se repite. */
export const ESPERAS_DE_SONDEO_MS = [2_000, 3_000, 5_000];
/** Más allá de esto el cliente deja de esperar y lo dice como un fallo. */
export const LIMITE_DE_ACTIVACION_MS = 120_000;
/** Cuánto se muestra «El modelo está listo.» antes de desaparecer. */
export const VISIBILIDAD_DE_LISTO_MS = 4_000;

const CLAVE = ["asistente", "modelo", "disponibilidad"] as const;

interface Opciones {
  /** Sin acceso, en mantenimiento o con el modal cerrado no se pregunta nada. */
  habilitado: boolean;
  /** Una respuesta de este mismo hilo prueba que el modelo está cargado. */
  modeloProbado: boolean;
}

/**
 * El estado del modelo propio (asistente-modelo-en-reposo, D2/D4/D5): una consulta al abrir y, sólo
 * mientras se activa, un sondeo con espera creciente. `desconocido` y `no_aplica` no se muestran.
 */
export function useDisponibilidadDelModelo({ habilitado, modeloProbado }: Opciones) {
  const cliente = useQueryClient();
  const sondeos = useRef(0);
  // Este cliente pidió activar: sólo entonces tiene sentido anunciar «listo».
  const [pedido, setPedido] = useState(false);
  const [agotado, setAgotado] = useState(false);

  const disponibilidad = useQuery({
    queryKey: CLAVE,
    queryFn: obtenerDisponibilidadDelModelo,
    enabled: habilitado,
    staleTime: VIGENCIA_MS,
    retry: false,
    refetchInterval: (consulta) =>
      consulta.state.data?.estado === "activando"
        ? ESPERAS_DE_SONDEO_MS[Math.min(sondeos.current++, ESPERAS_DE_SONDEO_MS.length - 1)]
        : false,
  });

  const activacion = useMutation({
    mutationFn: activarModelo,
    onSuccess: (estado) => cliente.setQueryData(CLAVE, estado),
  });

  const estado = disponibilidad.data?.estado;
  const activando = estado === "activando" || activacion.isPending;

  // El límite del cliente corre mientras dure la activación, y nada más.
  useEffect(() => {
    if (!activando) return;
    const temporizador = setTimeout(() => setAgotado(true), LIMITE_DE_ACTIVACION_MS);
    return () => clearTimeout(temporizador);
  }, [activando]);

  // «Listo» se anuncia unos segundos y desaparece.
  const listoTrasPedir = pedido && estado === "listo";
  useEffect(() => {
    if (!listoTrasPedir) return;
    const temporizador = setTimeout(() => setPedido(false), VISIBILIDAD_DE_LISTO_MS);
    return () => clearTimeout(temporizador);
  }, [listoTrasPedir]);

  function activar() {
    sondeos.current = 0;
    setAgotado(false);
    setPedido(true);
    activacion.mutate();
  }

  return { aviso: calcularAviso(), activar };

  function calcularAviso(): AvisoDelModelo {
    if (!habilitado || modeloProbado) return "oculto";
    if (activacion.isError || (agotado && activando)) return "fallo";
    if (activando) return "activando";
    if (listoTrasPedir) return "listo";
    if (estado === "en_reposo") {
      return disponibilidad.data?.ultimaActivacionFallida ? "fallo" : "en_reposo";
    }
    return "oculto";
  }
}
