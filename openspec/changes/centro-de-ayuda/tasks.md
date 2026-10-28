## 1. Modelo, visibilidad y búsqueda (red-green, sin UI)

- [x] 1.1 Crear `features/ayuda/tipos.ts` con `ModuloAyuda`, `ArticuloAyuda`, `PasoAyuda` (design D2)
- [x] 1.2 Red: `utils/visibilidad.test.ts` (any-of, lista vacía, módulo sin artículos visibles, artículo de otro módulo, permiso ausente)
- [x] 1.3 Green: `utils/visibilidad.ts` (`puedeVer`, `modulosVisibles`, `articulosVisibles`, `articuloVisible`)
- [x] 1.4 Red: `utils/busqueda.test.ts` (acentos y mayúsculas, stop-words, AND, ponderación título/palabras clave/resumen/cuerpo, desempate estable, consulta vacía)
- [x] 1.5 Green: `utils/busqueda.ts` (`normalizar`, `terminos`, `buscar`) con texto normalizado memoizado por artículo
- [x] 1.6 `utils/rutas.ts` con los constructores de URL (`/ayuda`, módulo, artículo, `#paso-N`, `/ayuda/buscar?q=`) y el slug reservado `buscar`

## 2. Contenido: infraestructura e integridad

- [x] 2.1 `contenido/index.ts` con `MODULOS` y `ARTICULOS` (registro explícito) y el orden por grupo y declaración
- [x] 2.2 Red: `contenido/integridad.test.ts` — slugs únicos y kebab-case, `buscar` reservado, módulo y grupo existentes, relacionados válidos, `alt` no vacío, archivos de captura existentes en `frontend/public`, permisos presentes en `NAVEGACION`, `modulo.permisos` cubre la unión de los de sus artículos, todo artículo con pasos
- [x] 2.3 Verificar que el test falla con cada caso inválido (contenido de prueba temporal) y no deja residuos

## 3. Shell: entrada, rutas y layout

- [x] 3.1 Red: `Sidebar.test.tsx` — "Centro de ayuda" visible sin permisos, activo bajo `/ayuda/*`, solo ícono con `aria-label` y `title` al contraer
- [x] 3.2 Green: `helpIcon` en `app/shell/icons.tsx` e ítem en el pie de `Sidebar.tsx` (design D6) con su CSS en `shell.css`
- [x] 3.3 Red/Green: `AppLayout` deriva `colapsado` de la ruta (`/ayuda*`) y vuelve al estado previo al salir; test
- [x] 3.4 `TopBar.test.tsx` — se mantiene la ausencia de botones "Ayuda" y "Notificaciones" (spec `barra-superior`)
- [x] 3.5 `features/ayuda/routes.tsx` y registro en `app/router.tsx` (sin `RequirePermission`); test de que un anónimo va a `/login`
- [x] 3.6 `LayoutAyuda` con grilla de dos columnas, sidebar de documentación de 264 px y comportamiento apilado en pantallas angostas

## 4. Páginas y componentes

- [x] 4.1 `InicioAyudaPage`: módulos visibles con título, descripción y chevron; breadcrumb y `PageHeader` según `encabezado-paginas`
- [x] 4.2 `ModuloAyudaPage`: breadcrumb, descripción y artículos agrupados (grupos vacíos omitidos); redirect a `/ayuda` si no existe o no es visible
- [x] 4.3 `ArticuloAyudaPage`: breadcrumb de tres niveles, `h1`, introducción, pasos con `id="paso-N"`, nota, "¿Qué sucede después?", preguntas frecuentes (acordeón con `id="preguntas-frecuentes"`) y relacionados visibles; sin secciones vacías; redirect para slug desconocido, de otro módulo o no visible
- [x] 4.4 Resolución de hash (`#paso-N`, `#preguntas-frecuentes`) con scroll al montar; test
- [x] 4.5 `BarraLateralAyuda`: título, buscador, módulos colapsables con artículos visibles, pasos bajo el artículo activo; test de estructura y de expansión del módulo activo
- [x] 4.6 `usePasoActivo` (IntersectionObserver, `rootMargin`, limpieza, `prefers-reduced-motion`) y clic con scroll suave + hash; test con mock de `IntersectionObserver`
- [x] 4.7 `BuscadorAyuda` combobox (ARIA, 5 resultados, ↑/↓/Enter/Esc, limpiar, "Ver todos los resultados", sin resultados); test de teclado
- [x] 4.8 `ResultadosAyudaPage` (`?q=`, cantidad, estado vacío con enlace al inicio); test
- [x] 4.9 `VisorImagen` (clic y teclado, Esc, fondo, foco de retorno); decidir `<dialog>` vs `Modal` de `@ars-docendi/ui` (design D9); test
- [x] 4.10 Test de navegación con router en memoria: inicio → módulo → artículo → clic en paso; y de permisos: un usuario sin `roles.ver` no ve ni encuentra el artículo de roles ni por URL directa

## 5. Entorno descartable y capturas

- [x] 5.1 Levantar un entorno local descartable (base nueva) sembrado solo con `infra/scripts/seed-data/sintetico.sql`; verificar que arranca y sirve las pantallas a capturar sin `sga.sql`; documentar cualquier faltante sin tocar el seed compartido
- [x] 5.2 Fijar viewport 1440×900 y la sesión de desarrollo por rol necesaria para cada módulo
- [x] 5.3 Tomar las capturas por paso (solo donde aportan) y guardarlas en `frontend/public/ayuda/<modulo>/` con el formato y peso acordados (design D10)
- [x] 5.4 Revisar cada captura: sin correos, nombres ni legajos reales; escribir su `alt`

## 6. Contenido (por módulo; un archivo por artículo, cap ~300 líneas)

Cada artículo verifica su texto contra la pantalla real y declara permisos y relacionados (inventario en `design.md` D11).

- [x] 6.1 Primeros pasos (4): `ingresar-al-sistema`, `recorrer-la-pantalla`, `permisos-y-pantallas-visibles`, `usar-el-centro-de-ayuda`
- [x] 6.2 Mi portal (6): `entender-tu-perfil`, `completar-datos-de-contacto`, `cargar-educacion-y-certificaciones`, `cargar-experiencia-y-proyectos`, `elegir-habilidades-e-intereses`, `subir-tu-cv`
- [x] 6.3 Tareas (7): `entender-el-listado-de-tareas`, `filtrar-y-ordenar-tareas`, `abrir-una-tarea`, `cambiar-el-estado-de-una-tarea`, `crear-una-tarea`, `crear-un-proyecto`, `seguir-un-proyecto`
- [x] 6.4 Designaciones (8): `crear-un-pedido`, `adjuntar-documentacion`, `editar-un-pedido`, `seguir-el-estado-de-un-pedido`, `revisar-el-tablero`, `resolver-un-pedido`, `descargar-el-lote-de-finalizados`, `gestionar-periodos`
- [x] 6.5 Configuración y sistema (5): `administrar-usuarios`, `administrar-docentes`, `administrar-roles-y-permisos`, `leer-el-dashboard-del-sistema`, `consultar-la-auditoria`
- [x] 6.6 Corregir el inventario de `design.md` D11 si al redactar cambia algún artículo, permiso o resumen
- [x] 6.7 Suite de integridad (2.2) en verde con todo el contenido

## 7. Docs (mismo diff)

- [x] 7.1 `docs/product/designs/centro-de-ayuda-design-spec.md` desde `_design-spec-template.md`: layout, estados (cargando no aplica, vacío, sin resultados, slug desconocido), accesibilidad, responsive; indexar en `docs/product/designs/README.md` si lista los specs
- [x] 7.2 `docs/architecture/stack.md`: ruta `/ayuda` y feature `ayuda` sin módulo de backend (tabla de `frontend/src/`)
- [x] 7.3 `README.md`: mención del Centro de ayuda y cómo se agrega/actualiza un artículo (breve)
- [x] 7.4 Ampliar la regla 9 de `AGENTS.md`: los cambios de UX o de permisos actualizan también el artículo de ayuda que los documenta
- [x] 7.5 Confirmar que no hay cambios de API, schema ni dependencias que exijan otros docs

## 8. Verificación y cierre

- [x] 8.1 `pnpm --filter frontend test:run`, `pnpm --filter frontend lint` y `pnpm --filter frontend build` en verde
- [x] 8.2 `pnpm format:check` y `pnpm exec openspec validate --all --strict` en verde
- [x] 8.3 Verificación manual en navegador, con un usuario de cada rol del dataset sintético: entrada, contracción, módulos visibles por rol, búsqueda, scroll-spy, deep links, visor de capturas y redirect de URL directa a contenido no visible
- [x] 8.4 Confirmar que Aulas y el Asistente no aparecen en la ayuda
