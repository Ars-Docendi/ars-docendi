## 1. Catálogo y plan

- [ ] 1.1 Tests primero: esquema JSON derivado del catálogo, interpretación del plan desde JSON, forma canónica y acuerdo entre muestras.
- [ ] 1.2 `CatalogoDelPlan` (campos, operadores, cargos, términos de anclaje, vocabulario fuera de catálogo) y `PlanDeConsulta` con su forma canónica.

## 2. Puerta, anclaje y validación

- [ ] 2.1 Tests primero: puerta léxica (fuera de catálogo, sin población, antigüedad sin calificar), anclaje de condiciones y términos sin consumir.
- [ ] 2.2 `PuertaDelPlan` y `ValidadorDePlan`.

## 3. Entidades, compilación y ejecución

- [ ] 3.1 Tests primero (Postgres): resolución de materias y carreras dentro del alcance del actor; el compilador produce SQL que pasa `ValidadorDeSql` y no usa el reloj.
- [ ] 3.2 `ResolutorDeEntidadesDelPlan` y `CompiladorDePlan`.
- [ ] 3.3 Test sin modelo: cada `plan_referencia` de `compuestas.json`, compilado y ejecutado contra el fixture con suplemento, devuelve lo mismo que su `sql_referencia`.

## 4. Carril y redacción

- [ ] 4.1 Tests primero con `ProveedorGuionado`: acuerdo → respuesta por plantilla; desacuerdo → aclaración; no expresable → carril SQL; primera muestra inválida → abstención; techo de llamadas respetado.
- [ ] 4.2 `GeneradorDePlan`, `RedaccionDelPlan` y `CarrilDelPlan`, y el desvío opcional en `CarrilSql`.
- [ ] 4.3 Opciones `PlanCompilado`, `MuestrasDelPlan` y `TemperaturaDeMuestrasDelPlan`, con validación y fila en la tabla de configuración del README del módulo.

## 5. Evaluación

- [ ] 5.1 Suplemento opcional del fixture (`conSuplementoCompuesto`) sin cambiar la huella del fixture de siempre.
- [ ] 5.2 `backend/eval/datasets/compuestas.json` con `sql_referencia` y `plan_referencia`.
- [ ] 5.3 Opción `--compuestas` del evaluador (fixture y corrida), con reporte sin gate.

## 6. Documentación y verificación

- [ ] 6.1 Sección del plan compilado en `backend/src/Modules.Asistente/README.md` y en `backend/eval/README.md`.
- [ ] 6.2 `dotnet test backend/ArsDocendi.slnx` en verde y `openspec validate --all --strict` (o constancia de lo que el entorno no permita).
- [ ] 6.3 Corrida en la RTX 3070 (a cargo del equipo): `--compuestas` con la opción apagada (control) y encendida, comparación ítem por ítem de respuestas falsas, aclaraciones y abstenciones.
