# Convenciones de datos remotos en frontend

Cada feature mantiene su propio adapter HTTP, tipos y hooks. Sólo `shared/api/client.ts` importa Axios; componentes y páginas consumen funciones tipadas o hooks.

## Estructura

```text
features/<feature>/
├── api/
│   ├── <feature>Api.ts      # request/response y adaptación de DTO
│   └── queryKeys.ts         # factory estable de claves
├── hooks/
│   └── use<Recurso>.ts      # useQuery/useMutation
└── types.ts                 # modelo de presentación, sin tipos Axios
```

No se crea un registro global de query keys: eso acoplaría features independientes. La forma mínima de una factory es:

```ts
export const recursoKeys = {
  all: ["recurso"] as const,
  lists: () => [...recursoKeys.all, "list"] as const,
  list: (filtros: Filtros) => [...recursoKeys.lists(), filtros] as const,
  details: () => [...recursoKeys.all, "detail"] as const,
  detail: (id: string) => [...recursoKeys.details(), id] as const,
};
```

Los filtros incluidos en claves deben ser serializables y normalizados. No se incorporan objetos de usuario completos ni callbacks.

## Consultas

- Carga inicial: indicador accesible; nunca datos fallback.
- Éxito vacío: mensaje específico y distinto de error.
- Error: `InlineAlert` con acción de reintento cuando corresponda.
- Datos previos durante refetch: pueden conservarse si la UI indica actualización y no los presenta como confirmación de una mutación fallida.

## Mutaciones

- No usar optimismo para altas, activaciones, membresías, períodos ni transiciones de pedidos.
- Al confirmar, actualizar el detalle retornado e invalidar listados/catálogos afectados.
- Al fallar, mantener el último estado confirmado y mapear Problem Details por `status`, `type`/código y `errors`.
- Deshabilitar el submit mientras la misma mutación está pendiente y evitar dobles envíos; las transiciones añaden `Idempotency-Key`.

## Frontera de adaptación

Los DTOs usan IDs y códigos canónicos. Si la UI necesita nombres compuestos, badges o agrupaciones, la conversión vive en el adapter o en un helper puro de presentación. Ningún componente interpreta excepciones HTTP ni construye autorización desde datos locales.

## Tests

Las pruebas interceptan HTTP en el límite del cliente y ejercitan los hooks reales. Las fixtures están en código sólo alcanzable desde tests. Se cubren carga, vacío, éxito, error, retry, invalidación y conflicto por cada corte vertical.
