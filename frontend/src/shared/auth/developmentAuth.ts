interface DevelopmentAuthEnvironment {
  dev: boolean;
  configuredValue?: string;
}

export function resolverDevelopmentAuthEnabled({
  dev,
  configuredValue,
}: DevelopmentAuthEnvironment): boolean {
  return dev || configuredValue === "true";
}

export const developmentAuthEnabled = resolverDevelopmentAuthEnabled({
  dev: import.meta.env.DEV,
  configuredValue: import.meta.env.VITE_DEVELOPMENT_AUTH_ENABLED,
});
