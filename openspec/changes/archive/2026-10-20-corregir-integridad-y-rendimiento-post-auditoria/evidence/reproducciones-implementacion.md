# Ejecución de reproducciones persistentes

## Referencia y alcance

- Referencia anterior: `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`.
- Fixtures: `scripts/tests/fixtures/auditoria-integridad/`.
- Resultado real: `resultado-anterior.json`, incluido junto con las fixtures.
- Ejecución: `AUDITORIA_DOCKER=1 AUDITORIA_EVIDENCIA=scripts/tests/fixtures/auditoria-integridad/resultado-anterior.json node --test scripts/tests/auditoria-integridad.test.mjs`.
- Resultado observado: 15 pruebas, 15 aprobadas, cero fallos/omitidas; 13 probes SQL reproducidos. PostgreSQL desechable eliminado y ausencia verificada por Docker.

Las reproducciones recuperan SQL y wrappers desde el SHA fijo y comprueban sus hashes, sin depender de `.artifacts` ni leer SGA. La clasificación de estos probes es SQL; no demuestra carreras HTTP. Cada escenario declara el límite y los hallazgos cubiertos. Las carreras y la corrección GREEN requieren las suites específicas de módulos y no se declaran completadas por este ensayo.

## Diagnóstico: RED observado

`dotnet vstest backend/tests/ArsDocendi.IntegrationTests/bin/Release/net10.0/ArsDocendi.IntegrationTests.dll --TestAdapterPath:backend/tests/ArsDocendi.IntegrationTests/bin/Release/net10.0 --TestCaseFilter:FullyQualifiedName~DiagnosticoIntegridadTests --logger:'trx;LogFileName=diagnostico-red.trx' --ResultsDirectory:.artifacts/apply-post-auditoria`

Resultado anterior a implementar el runner: 2 pruebas descubiertas, 1 aprobada, 1 fallida. `Cli_detecta_persona_huerfana_sin_escribir_ni_exponer_valores` esperaba exit 1 y recibió exit 0. Es el fallo causal del modo diagnóstico, no una reproducción basada en error de compilación.

## Diagnóstico: GREEN verificado

`diagnostico-arquitectura-2.trx`: 11 pruebas ejecutadas, 11 aprobadas, cero
fallos/omitidas. Ocho prueban el CLI y composición; tres verifican arquitectura
(controllers, fronteras de módulos y ausencia de escrituras Identity desde módulos).
El CLI verifica base limpia, perfil huérfano, UPN normalizados duplicados, scope
incoherente, ciclo legado, permisos insuficientes y consumidores incompletos. La
huella antes/después cubre todas las tablas de Identity/Audit/Storage/Designaciones/
Portal/Tareas, incluidos histories, sin mostrar los valores.

La composición acepta los nombres de contextos sin distinguir case, pero rechaza
duplicados bajo esa misma comparación. Todos los adaptadores locales usan
`REPEATABLE READ READ ONLY`; un lector faltante/fallido mantiene exit no cero.
El diagnóstico no certifica ausencia de drift ni una fotografía global entre
módulos: ese límite figura en documentación y salida del CLI.

## Correcciones del harness

- Literal raw C# previo mal cerrado en `DiagnosticoIdentity`: reparado para permitir compilar y ejecutar la regresión.
- Docker emite `no such object` en minúsculas en este entorno. El check de limpieza ahora comprueba esa frase sin distinguir case, manteniendo exit no cero obligatorio; no acepta cualquier error como prueba de ausencia.

No se desplegó ni se modificó una base compartida. No se hizo commit/push ni sync/archive.
