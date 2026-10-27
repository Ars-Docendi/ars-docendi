interface AsistenteDebugEnvironment {
  configuredValue?: string;
}

/** Resolver puro: sólo el opt-in explícito prende el modo debug. */
export function resolverModoDebugAsistente({
  configuredValue,
}: AsistenteDebugEnvironment): boolean {
  return configuredValue === "true";
}

/** Prende «Cómo lo interpreté» (el razonamiento del asistente). */
export const modoDebugAsistente = resolverModoDebugAsistente({
  configuredValue: import.meta.env.VITE_ASISTENTE_DEBUG,
});
