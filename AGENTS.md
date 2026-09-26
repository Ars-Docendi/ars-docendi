# Guía del repositorio

Esta es la fuente de instrucciones para cualquier asistente o herramienta de desarrollo. Los adaptadores específicos deben enlazar este archivo, no copiarlo.

## Contexto

Ars Docendi es un sistema institucional de la UNLaM: backend ASP.NET Core 10, frontend React 19/Vite 8 y PostgreSQL 18. El monorepo contiene cuatro contextos: Designaciones, Aulas, Portal y Tareas.

Leé antes de cambiar:

- [README.md](README.md) para setup y comandos.
- [docs/architecture/](docs/architecture/) para estructura, dependencias, API y datos.
- [openspec/specs/](openspec/specs/) para comportamiento vigente.
- [docs/business-rules/](docs/business-rules/) para reglas institucionales y sus tests.

## Reglas de implementación

1. `Modules.X` no referencia implementaciones `Modules.Y`; la comunicación pública cruza proyectos `Modules.Y.Contracts`.
2. El grafo de dependencias debe permanecer acíclico.
3. Cada módulo expone `GET /api/<modulo>/ping` con `{ module, status: "ok" }`.
4. `ArsDocendi.Shared` se limita a utilidades transversales e infraestructura `identity`/`audit`. Los módulos leen identidad mediante `IConsultasIdentity`; sólo administración la escribe.
5. Una feature nueva requiere un change OpenSpec listo antes del código. Un bug confirmado se corrige con el test de regresión mínimo.
6. Cambios de API, schema o dependencias actualizan la documentación correspondiente en el mismo diff.
7. No crear UI ficticia, botones muertos, abstracciones especulativas ni dependencias innecesarias.
8. Las reglas normativas usan `BR-<modulo>-NNN`, fuente y mapping a tests.
9. Los cambios de UX actualizan su design spec cuando existe.
10. Identificadores, comentarios y documentación propios se escriben en español; los símbolos de frameworks conservan sus nombres.
11. Un módulo puede consultar schemas ajenos sin pasar por `Contracts` únicamente si la frontera la sostiene el motor de base de datos y es **falsable**: rol de PostgreSQL sin GRANT de mutación, GRANT enumerados columna por columna contra un manifiesto versionado, policies RLS que conjunten el permiso de dominio, y tests que fallen si cualquiera de esas condiciones se degrada. Hoy aplica a `Modules.Asistente` y sólo a él. Ratificado el 2026-09-08.

Es la excepción a la regla 1, y existe porque el Asistente no puede cumplirla: pasar por `Contracts` significaría que el modelo generara llamadas a métodos en vez de SQL, que es otro sistema. Se enmienda explícitamente en vez de reinterpretar la regla 1, porque una regla reinterpretada deja de restringir a nadie.

**La regla no es este párrafo: es este párrafo más lo que lo verifica** — `ManifiestoPrivilegiosTests`, `PrivilegiosLecturaTests`, `RlsAlcanceTests` y `ArquitecturaAsistenteTests`. Un GRANT que nadie re-verifica se degrada en silencio y el sistema sigue funcionando; sólo deja de estar contenido. Si alguno de esos tests deja de fallar ante una degradación, el invariante deja de valer aunque el texto siga acá.

Nota sobre la regla 4: el Asistente **no** lee identidad por `IConsultasIdentity` sino por SQL directo con un rol de solo lectura, y es exactamente lo que esta regla autoriza.

## Flujo de trabajo

- Preservá cambios existentes que no pertenezcan a la tarea.
- Buscá usos y llamadores antes de modificar una API compartida.
- Preferí código concreto y funciones estándar. Una interfaz interna necesita más de una implementación o una frontera real.
- Las autorizaciones y reglas de negocio se validan en backend aunque el frontend las anticipe.
- No expongas PII, credenciales ni datos de ámbitos ajenos en errores o logs.
- Usá migraciones/SQL versionado para datos y constraints; no edites una base manualmente como solución.

## Verificación

```bash
dotnet test backend/ArsDocendi.slnx
pnpm --filter frontend test:run
pnpm --filter frontend lint
pnpm --filter frontend build
pnpm format:check
pnpm exec openspec validate --all --strict
```

Ejecutá el subconjunto proporcional al cambio y dejá constancia de cualquier check que el entorno no permita completar.
