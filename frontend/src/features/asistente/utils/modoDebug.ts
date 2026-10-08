interface AsistenteDebugEnvironment {
  configuredValue?: string;
}

/**
 * Resolver puro: sólo el opt-in explícito prende el modo debug.
 *
 * A diferencia de `resolverDevelopmentAuthEnabled`, ACÁ NO HAY FALLBACK a
 * `import.meta.env.DEV`. El razonamiento crudo no debe verse por el solo hecho
 * de correr `vite dev`; hace falta poner la variable a mano.
 */
export function resolverModoDebugAsistente({
  configuredValue,
}: AsistenteDebugEnvironment): boolean {
  return configuredValue === "true";
}

/**
 * Prende «Cómo lo interpreté» (el razonamiento del asistente). Off por
 * defecto: hay que poner `VITE_ASISTENTE_DEBUG=true` y reiniciar/rebuildear
 * Vite, que inlinea las env vars en build time.
 */
export const modoDebugAsistente = resolverModoDebugAsistente({
  configuredValue: import.meta.env.VITE_ASISTENTE_DEBUG,
});
