# Convenciones de datos remotos en frontend

Cada feature mantiene su propio adapter HTTP, tipos y hooks. Sólo `shared/api/client.ts` crea la instancia de Axios; el resto del código importa de `axios` únicamente tipos o `isAxiosError` para inspeccionar errores. Componentes y páginas consumen funciones tipadas o hooks.

## Estructura

```text
features/<feature>/
├── api/
│   └── <feature>Api.ts      # request/response y adaptación de DTO
├── hooks/
│   └── use<Recurso>.ts      # useQuery/useMutation y su factory de claves (<recurso>Keys)
└── types.ts                 # modelo de presentación, sin tipos Axios
```

No se crea un registro global de query keys: eso acoplaría features independientes. Cada factory vive junto al hook que la usa (por ejemplo `usuariosKeys` en `useUsuarios.ts`). La forma mínima es:

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

## Datos locales admitidos

El runtime obtiene personas, usuarios, docentes, roles, permisos, materias, cargos, períodos, pedidos y perfiles desde las APIs. Ningún array TypeScript, store en memoria ni `localStorage` es autoridad para esos registros.

### Preferencias locales admitidas

La autenticación de desarrollo puede guardar únicamente el identificador del usuario y el código de rol elegidos. El backend vuelve a resolver la cuenta, sus permisos y su ámbito en cada solicitud.

El estado efímero de formularios, modales, filtros y navegación también permanece en el cliente.

### Constantes admitidas

Pueden vivir en código:

- etiquetas, tonos, íconos y textos de estados;
- configuración de columnas, filtros y paginación;
- vocabularios cerrados modelados por tipos;
- helpers puros de formato o presentación.

Una constante deja de pertenecer a esta categoría si representa una fila identificable o si un operador puede modificarla.

### Fixtures de tests

Los tests pueden construir respuestas HTTP y entidades locales dentro de archivos `*.test.*` o utilidades importadas sólo por tests. Ningún entrypoint de runtime puede alcanzarlas.

El build ejecuta `frontend/scripts/check-no-runtime-mocks.mjs` y falla si el runtime importa directorios `/mock/` o las fuentes locales de negocio retiradas.
