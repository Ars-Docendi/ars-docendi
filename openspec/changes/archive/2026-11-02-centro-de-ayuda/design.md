## Context

OSPESE (backoffice hermano) tiene un Centro de ayuda maduro: sidebar de documentación con scroll-spy, búsqueda local, artículos con pasos y capturas. Se tomó como referencia de estructura y comportamiento (snapshot de solo lectura), no como código a copiar.

Estado actual de Ars Docendi relevante:

- `app/AppLayout.tsx` arma un grid `topbar / sidebar / main` y guarda `collapsed` en `useState`. El botón de contraer de `TopBar` está comentado, así que hoy el shell nunca se contrae; el modo `collapsed` de `Sidebar` (solo íconos) existe y está testeado.
- `app/shell/nav.ts` es el registro de navegación (`NAVEGACION`) con `permiso` por ítem y `filtrarNavegacion(permisos)`. La sidebar tiene un pie fijo (`.adoc-sidebar-foot`, "Versión 1.0.0 · Ars Docendi").
- `RequireAuth` protege todo lo que cuelga de `AppLayout`; `RequirePermission` protege cada ruta por permiso. `useCurrentUser()` entrega `user.permissions`.
- `barra-superior` prohíbe un botón "Ayuda" en la barra superior; `encabezado-paginas` prohíbe el pretitle y fija el breadcrumb desde "Inicio".
- `docs/architecture/stack.md`: las features no se importan entre sí; lo común sube a `shared/`.
- Módulos con pantallas funcionales en `develop`: portal, tareas (+ proyectos), designaciones (mis pedidos, nuevo/editar pedido, detalle, revisión, períodos), usuarios, docentes, roles, sistema (dashboard) y auditoría. `aulas` es un placeholder ("Módulo en construcción") y el Asistente no está en `develop`.

## Goals / Non-Goals

**Goals:**

- Ayuda en la app, por módulo, con la misma estructura y comportamiento que el Centro de ayuda de OSPESE.
- Que cada persona vea solo la ayuda de lo que puede usar.
- Contenido versionado con el código y verificable por test.
- Cero UI ficticia y cero código muerto.

**Non-Goals:**

- Backend, CMS, edición del contenido en caliente, internacionalización o versionado de artículos.
- Búsqueda en servidor, analítica de uso, feedback "¿te sirvió?".
- Videos, "Temas frecuentes" / `featured`, medios placeholder (existen en la referencia, sin uso real aquí).
- Documentar Aulas o el Asistente.
- Un botón de ayuda en la barra superior.

## Decisions

### D1. Una feature nueva `features/ayuda`, sin importar otras features

```text
features/ayuda/
├── routes.tsx                   # { path: "ayuda", element: <LayoutAyuda />, children: [...] }
├── tipos.ts                     # ModuloAyuda, ArticuloAyuda, PasoAyuda, ...
├── contenido/
│   ├── index.ts                 # MODULOS, ARTICULOS (registro explícito)
│   └── <modulo>/<articulo>.ts   # un archivo por artículo (cap ~300 líneas)
├── utils/                       # rutas.ts, visibilidad.ts, busqueda.ts
├── hooks/usePasoActivo.ts       # IntersectionObserver
├── components/                  # BarraLateralAyuda, BuscadorAyuda, VisorImagen, PasoAyuda, ...
└── pages/                       # InicioAyudaPage, ModuloAyudaPage, ArticuloAyudaPage, ResultadosAyudaPage
```

`router.tsx` suma `ayudaRoutes` dentro de `AppLayout`, igual que las demás features. No exige `RequirePermission`: hereda `RequireAuth`.

- **Por qué**: la ayuda es una superficie propia con modelo, utilidades y componentes; mezclarla en `shared/` la volvería dependencia de todo.
- **Cadenas de permiso**: el contenido las duplica como texto (no importa `app/shell/nav.ts` en tiempo de ejecución, para no acoplar una feature con `app/`). El test de integridad importa `NAVEGACION` y verifica que cada permiso declarado exista allí; es la red que impide el desvío.

### D2. Modelo de contenido (identificadores en castellano)

```ts
export interface ModuloAyuda {
  slug: string;
  titulo: string;
  descripcion: string;
  grupos: readonly string[]; // orden de presentación
  permisos: readonly string[]; // any-of; [] = todos
}
export interface PasoAyuda {
  titulo: string;
  etiquetaIndice?: string; // etiqueta corta para la sidebar; por defecto el título
  cuerpo: string;
  captura?: { src: string; alt: string; leyenda?: string };
  nota?: { tipo: "info" | "importante" | "consejo"; titulo?: string; cuerpo: string };
}
export interface ArticuloAyuda {
  slug: string;
  moduloSlug: string;
  grupo: string;
  titulo: string;
  resumen: string;
  introduccion: string;
  permisos: readonly string[];
  palabrasClave?: readonly string[];
  pasos: readonly PasoAyuda[];
  despues?: {
    titulo: string;
    cuerpo: string;
    viñetas?: readonly { termino: string; descripcion: string }[];
  };
  preguntas?: readonly { pregunta: string; respuesta: string }[];
  relacionados: readonly string[]; // "modulo/articulo"
}
```

Orden: por índice de grupo en `ModuloAyuda.grupos` y luego por orden de declaración en `ARTICULOS`. Los textos son strings planos (sin Markdown ni HTML); los saltos de párrafo se modelan como arreglo solo si algún artículo lo necesita al implementar (no se agrega antes).

### D3. Visibilidad por permisos como funciones puras

`utils/visibilidad.ts`:

- `puedeVer(permisosRequeridos, permisosSesion)`: `requeridos.length === 0 || requeridos.some(p => sesion.has(p))`.
- `modulosVisibles(permisos)`, `articulosVisibles(permisos)`, `articuloVisible(moduloSlug, articuloSlug, permisos)` (devuelve `undefined` si el módulo, el artículo o el permiso fallan; también si el artículo no pertenece al módulo).

Todas las páginas y la búsqueda derivan de `articulosVisibles`; ninguna consulta `ARTICULOS` crudo. Las páginas redirigen a `/ayuda` cuando la resolución da `undefined` (D4). Un módulo se oculta si no tiene artículos visibles, aunque la persona cumpla su permiso.

- **Alternativa descartada**: derivar el permiso del módulo solo de sus artículos. El requisito del usuario es que ambos declaren; un módulo con permiso propio permite ocultarlo entero con una sola línea. El test de integridad exige que `modulo.permisos` cubra la unión de los de sus artículos (o sea `[]` si algún artículo es abierto), para que declarar ambos no genere contradicciones.
- **Permiso efectivo**: el de la sesión (`useCurrentUser().user.permissions`), nunca el nombre del rol.

### D4. Rutas y redirecciones

| Ruta                       | Página                |
| -------------------------- | --------------------- |
| `/ayuda`                   | `InicioAyudaPage`     |
| `/ayuda/buscar?q=`         | `ResultadosAyudaPage` |
| `/ayuda/:modulo`           | `ModuloAyudaPage`     |
| `/ayuda/:modulo/:articulo` | `ArticuloAyudaPage`   |

`buscar` es slug reservado (el orden de rutas de React Router prioriza el literal, y el test de integridad lo prohíbe como módulo). Los redirects usan `<Navigate to="/ayuda" replace />`. El hash se resuelve en `ArticuloAyudaPage` con un efecto que hace `scrollIntoView` del elemento `#paso-N` / `#preguntas-frecuentes` tras montar (React Router no desplaza a anclas por sí solo).

### D5. Layout de dos sidebars y contracción derivada de la ruta

`AppLayout` calcula `const colapsado = collapsed || pathname.startsWith("/ayuda")` y se lo pasa a `Sidebar` y al `div` raíz. No hay estado nuevo ni efectos: la contracción forzada se deriva en render, así que al salir de `/ayuda` el shell vuelve solo a su estado. (Hoy ninguna UI cambia `collapsed`, por eso no se ocultan controles: no hay botón que ocultar.)

`LayoutAyuda` (elemento de la ruta `ayuda`) renderiza la sidebar de documentación (264 px, sticky, con scroll propio) junto a un contenedor para `<Outlet />`. Como `.adoc-main` es el área de scroll del shell, `LayoutAyuda` usa una grilla interna de dos columnas y la sidebar de documentación queda a la izquierda del contenido, dentro de `main`. Detalles de CSS (tokens, breakpoint móvil) se resuelven al implementar con el diseño de la design spec; por debajo de ~900 px la sidebar de documentación se apila sobre el contenido (no se agrega menú hamburguesa).

### D6. Ítem "Centro de ayuda" en el pie de la sidebar

Se agrega un `NavLink` a `/ayuda` (`end={false}`, para quedar activo en subrutas) dentro de `.adoc-sidebar-foot`, encima del texto de versión. Vive en `Sidebar.tsx`, no en `NAVEGACION`, porque no depende de permisos y `filtrarNavegacion` solo conoce ítems con `permiso`. Ícono "?" nuevo en `icons.tsx` (un `helpIcon`; `iconos.tsx` de `shared/ui` queda intacto). Con la sidebar contraída muestra solo el ícono, con `title` y `aria-label`. El pie conserva el texto de versión (en modo contraído sigue mostrando "AD").

### D7. Búsqueda cliente

`utils/busqueda.ts` expone `normalizar(texto)`, `terminos(consulta)` y `buscar(consulta, articulos)`:

- `normalizar`: `toLowerCase()` + `normalize("NFD")` quitando marcas diacríticas (ñ se conserva como `n` + tilde combinante removida; se acepta que "año" y "ano" coincidan).
- Stop-words: lista corta en castellano (`el, la, los, las, un, una, de, del, en, y, o, a, al, por, para, con, que, como, se, mi, tu, su`).
- AND de términos por subcadena sobre título, palabras clave, resumen y cuerpo (introducción + pasos + preguntas); puntaje por término = 10 (título), 6 (palabras clave), 3 (resumen), 1 (cuerpo); suma de términos; desempate por orden de contenido (`sort` estable).
- Se indexa una vez por módulo del archivo (memo del texto normalizado por artículo), y se filtra después por visibilidad; el índice no depende de la sesión.

El combobox (`BuscadorAyuda`) sigue el patrón ARIA 1.2 (input `role="combobox"`, listbox, `aria-activedescendant`), 5 resultados máximo (sin opción activa al abrir; la primera flecha activa una), flechas / Enter / Esc, limpiar y "Ver todos los resultados" cuando hay más de 5. El estado de la consulta de la página de resultados vive en la URL (`?q=`).

### D8. Scroll-spy

`usePasoActivo(idsPasos)` crea un `IntersectionObserver` sobre los `#paso-N` con `rootMargin` que privilegia el tope del área visible y devuelve el paso activo; la sidebar lo marca como actual. El clic llama a `scrollIntoView({ behavior: "smooth" })` y actualiza el hash. Se respeta `prefers-reduced-motion` (scroll `auto`). El observer se limpia al desmontar o al cambiar de artículo. En jsdom (sin `IntersectionObserver`) los tests lo mockean.

### D9. Visor de capturas

`VisorImagen` usa el `Modal` de `@ars-docendi/ui` (decisión tomada al implementar, por consistencia con la app; no hay `<dialog>` propio). La miniatura es un `<button>` con la imagen. Esc, el fondo y el botón de cerrar lo cierran. Limitación conocida: el `Modal` no atrapa el foco ni lo devuelve solo, así que el componente enfoca el diálogo al abrir y devuelve el foco a la miniatura al cerrar.

### D10. Capturas y assets

- Formato: WebP calidad 85.
- Ruta: `frontend/public/ayuda/<modulo>/<articulo>-paso-N.webp`; en el contenido `src: "/ayuda/<modulo>/..."`. Vite sirve `public/` tal cual; sin pipeline de imágenes.
- Origen: app corriendo local contra una base descartable creada solo con `infra/scripts/seed-data/sintetico.sql` (sin `sga.sql`; ver `README.md` §3 y `AGENTS.md`). Se verifica primero que ese dataset por sí solo arranque la app y sirva las pantallas a capturar; si falta algo, se resuelve sin tocar el seed compartido. Viewport fijo 1440×900, mismo tema, sesión de desarrollo con el rol adecuado a cada artículo.
- Revisión de privacidad: ninguna captura con correos, nombres o legajos reales. El dataset sintético usa datos ficticios.
- Peso: objetivo ≤ 150 KB por captura.
- Una captura por paso donde aporta (no todos los pasos llevan).

### D11. Propuesta de contenido inicial: 5 módulos, 30 artículos

El texto de cada artículo se escribe durante la implementación, sobre el comportamiento real verificado en la app y en las specs. Esto es solo el inventario.

| Módulo (slug) — permisos del módulo                                                                                                 | Grupo                  | Artículo (slug)                      | Permisos del artículo                              | Resumen de una línea                                                       |
| ----------------------------------------------------------------------------------------------------------------------------------- | ---------------------- | ------------------------------------ | -------------------------------------------------- | -------------------------------------------------------------------------- |
| **Primeros pasos** (`primeros-pasos`) — todos                                                                                       | Empezar                | `ingresar-al-sistema`                | todos                                              | Entrar con tu cuenta institucional de Microsoft y qué hacer si no te deja. |
|                                                                                                                                     | Moverte por el sistema | `recorrer-la-pantalla`               | todos                                              | Barra superior, menú lateral, migas de pan y cierre de sesión.             |
|                                                                                                                                     | Moverte por el sistema | `permisos-y-pantallas-visibles`      | todos                                              | Por qué ves solo algunas pantallas y a quién pedir más acceso.             |
|                                                                                                                                     | Moverte por el sistema | `usar-el-centro-de-ayuda`            | todos                                              | Buscar, leer los pasos y abrir las capturas.                               |
| **Mi portal** (`mi-portal`) — `portal.ver`                                                                                          | Tu perfil              | `entender-tu-perfil`                 | `portal.ver`                                       | Qué datos son institucionales (solo lectura) y cuáles editás vos.          |
|                                                                                                                                     | Tu perfil              | `completar-datos-de-contacto`        | `portal.ver`                                       | Cargar y corregir tu teléfono y tu mail de contacto.                       |
|                                                                                                                                     | Tu trayectoria         | `cargar-educacion-y-certificaciones` | `portal.ver`                                       | Sumar títulos, estudios y certificaciones con su período.                  |
|                                                                                                                                     | Tu trayectoria         | `cargar-experiencia-y-proyectos`     | `portal.ver`                                       | Registrar experiencia laboral y proyectos con mes y año.                   |
|                                                                                                                                     | Tu trayectoria         | `elegir-habilidades-e-intereses`     | `portal.ver`                                       | Elegir del vocabulario o sugerir un término nuevo.                         |
|                                                                                                                                     | Tu CV                  | `subir-tu-cv`                        | `portal.ver`                                       | Subir, reemplazar o quitar tu CV en PDF.                                   |
| **Tareas** (`tareas`) — `tareas.ver`, `tareas.gestionar`                                                                            | Seguir tareas          | `entender-el-listado-de-tareas`      | `tareas.ver`                                       | Cuadros por proyecto, semáforo de vencimiento y tareas en pausa.           |
|                                                                                                                                     | Seguir tareas          | `filtrar-y-ordenar-tareas`           | `tareas.ver`                                       | Filtros por columna, alcance Todas/Propias y orden.                        |
|                                                                                                                                     | Seguir tareas          | `abrir-una-tarea`                    | `tareas.ver`                                       | Detalle, comentarios, tareas hijas y relacionadas.                         |
|                                                                                                                                     | Seguir tareas          | `cambiar-el-estado-de-una-tarea`     | `tareas.ver`                                       | Pendiente, En curso, Pausa (con motivo) y Resuelta (con solución).         |
|                                                                                                                                     | Gestionar tareas       | `crear-una-tarea`                    | `tareas.gestionar`                                 | Alta de una tarea, responsable y vencimiento.                              |
|                                                                                                                                     | Proyectos              | `crear-un-proyecto`                  | `tareas.gestionar`                                 | Alta de un proyecto y quién puede hacerlo.                                 |
|                                                                                                                                     | Proyectos              | `seguir-un-proyecto`                 | `tareas.gestionar`                                 | Detalle, edición y cambio de estado del proyecto.                          |
| **Designaciones** (`designaciones`) — `designaciones.gestionar`, `designaciones.revisar`, `periodos.administrar`                    | Pedir una designación  | `crear-un-pedido`                    | `designaciones.gestionar`                          | Nuevo pedido paso a paso, según el tipo de novedad.                        |
|                                                                                                                                     | Pedir una designación  | `adjuntar-documentacion`             | `designaciones.gestionar`                          | Formatos y tamaños admitidos y cómo quitar un adjunto.                     |
|                                                                                                                                     | Pedir una designación  | `editar-un-pedido`                   | `designaciones.gestionar`                          | Qué se puede cambiar de un pedido en borrador o devuelto.                  |
|                                                                                                                                     | Pedir una designación  | `seguir-el-estado-de-un-pedido`      | `designaciones.gestionar`, `designaciones.revisar` | Mis pedidos, detalle, cadena de revisión y estados.                        |
|                                                                                                                                     | Revisar pedidos        | `revisar-el-tablero`                 | `designaciones.revisar`                            | Pestañas por área, filtros por encabezado y orden de los pedidos.          |
|                                                                                                                                     | Revisar pedidos        | `resolver-un-pedido`                 | `designaciones.revisar`                            | Aprobar, rechazar, devolver o priorizar un pedido, y su efecto.            |
|                                                                                                                                     | Revisar pedidos        | `descargar-el-lote-de-finalizados`   | `designaciones.revisar`                            | Exportar el libro de pedidos finalizados del período activo.               |
|                                                                                                                                     | Períodos               | `gestionar-periodos`                 | `periodos.administrar`                             | Crear, editar, activar y eliminar períodos de designación.                 |
| **Configuración y sistema** (`administracion`) — `usuarios.ver`, `docentes.ver`, `roles.ver`, `sistema.estado.ver`, `auditoria.ver` | Personas               | `administrar-usuarios`               | `usuarios.ver`                                     | Alta, edición, roles por ámbito, activación y desactivación.               |
|                                                                                                                                     | Personas               | `administrar-docentes`               | `docentes.ver`                                     | Alta y edición de docentes ("Mis docentes" para Jefe de Cátedra).          |
|                                                                                                                                     | Roles y permisos       | `administrar-roles-y-permisos`       | `roles.ver`                                        | Crear roles, asignar permisos y qué cambia al guardar.                     |
|                                                                                                                                     | Sistema                | `leer-el-dashboard-del-sistema`      | `sistema.estado.ver`                               | Estado de módulos, base de datos e inferencia.                             |
|                                                                                                                                     | Sistema                | `consultar-la-auditoria`             | `auditoria.ver`                                    | Filtrar y leer los registros de auditoría.                                 |

Notas: los artículos de edición reales (crear, editar) pueden exigir `*.administrar` en la app; la ayuda usa el permiso de **ver** del módulo para visibilidad y lo aclara en el texto de los pasos cuando una acción requiere uno mayor. El listado sale del código de `develop` y de las specs vigentes; al redactar se corrige con lo que muestre la pantalla.

### D12. Qué se descarta de la referencia

`video` (artículo y paso), `kind: "placeholder"`, `featured` y "Temas frecuentes", `formatArticleCount` y variantes de componentes sin uso. Un solo tipo de captura (`imagen`). Cada tipo/componente existe porque algún artículo o página lo usa hoy.

### D13. Encabezados y la convención existente

Las páginas de ayuda siguen `encabezado-paginas`: breadcrumb desde "Inicio" + `PageHeader`, sin pretitle. El breadcrumb es un `MigasAyuda` local con `Link` del router, porque el `Breadcrumbs` de la librería recarga la página al navegar. La referencia muestra un "eyebrow" con el módulo sobre el título del artículo; aquí se omite (el módulo ya está en el breadcrumb de tres niveles), de modo que **no** hace falta modificar `encabezado-paginas`.

### D14. Docs y tests

- Tests Vitest + Testing Library: integridad del contenido (D2/`contenido-ayuda`), `visibilidad.test.ts`, `busqueda.test.ts`, páginas (módulo, artículo, redirects, hash), `BuscadorAyuda` (teclado), `VisorImagen`, `Sidebar.test.tsx` (ítem de ayuda), `AppLayout` (contracción por ruta) y un test de navegación con `MemoryRouter`/`createMemoryRouter` que recorre inicio → módulo → artículo → paso.
- Docs: `docs/product/designs/centro-de-ayuda-design-spec.md` (regla 9), `docs/architecture/stack.md` (ruta `/ayuda`, feature sin módulo de backend), `README.md` (mención). No cambia API, schema ni dependencias.

## Risks / Trade-offs

- [El contenido envejece cuando cambian las pantallas] → regla de actualizar el artículo en el mismo diff (spec `contenido-ayuda`), tests de integridad, y la regla 9 de `AGENTS.md` ampliada para incluir el artículo de ayuda.
- [Las capturas se desactualizan visualmente] → una por paso solo donde aporta; viewport y datos fijos para que re-tomarlas sea repetible.
- [Cadenas de permiso duplicadas en el contenido] → test que las valida contra `NAVEGACION`; ante un permiso que no está en la navegación (p. ej. `designaciones.ver`) el test obliga a justificarlo o corregirlo.
- [Peso del repo por imágenes] → ≤ 150 KB por captura y un tope de una por paso.
- [Filtrado solo presentacional] → es intencional; el backend y los guards siguen siendo la autoridad. La ayuda no expone datos, solo texto estático ya distribuido en el bundle (el bundle contiene el texto de toda la ayuda; no contiene datos sensibles, y el contenido no debe incluir PII).
- [Buscar indexa también contenido no visible] → el índice se construye una vez, pero el resultado se filtra por visibilidad antes de devolverse.

## Migration Plan

Aditivo. Sin migraciones ni datos. Despliegue normal del frontend. Rollback: revertir el PR; el ítem del pie desaparece y `/ayuda` deja de existir.

## Open Questions

Ninguna abierta. El visor de capturas se resolvió con el `Modal` de `@ars-docendi/ui` (ver D9).
