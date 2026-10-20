# Validación del propose

## Alcance comprobado

`openspec validate corregir-integridad-y-rendimiento-post-auditoria --strict --json` finalizó con exit 0, `valid: true` y lista de issues vacía después de reconciliar explícitamente requisitos obsoletos. Esto valida los artefactos de planificación, no certifica que las correcciones ya estén implementadas.

Los diez capabilities declarados en proposal tienen sus deltas. La matriz de design cubre A01–A14 y P01–P04. Ninguna tarea de implementación se marca realizada durante propose.

## Validación global preexistente

`openspec validate --all --strict --json` inspeccionó 60 ítems: el cambio nuevo pasó; de las 59 specs principales, 27 pasaron y 32 fallaron por advertencias de textos de requisitos de más de 500 caracteres que strict considera bloqueantes.

Las specs principales no se modificaron durante este flujo y el delta nuevo no tiene esas advertencias. La limpieza global de longitud no forma parte de este cambio; no se recortaron requisitos ajenos ni se declararon resueltos esos problemas. La integración/archivo deberá registrar esa condición conforme al workflow y no presentarla como validación global exitosa.

## Límites

- Solo proposal, design, deltas, checklist y evidencia de planificación: no apply, código productivo, migraciones, bases compartidas, commit, push ni deploy.
- La evidencia de auditoría tiene el alcance y las incertidumbres descritos en `auditoria.md`.
- La política intradía y el mantenimiento de referencias lógicas fueron seleccionados explícitamente por el usuario.
- La PII histórica permanece sujeta a redacción/acceso y autorización específica; no se autoriza purga ni se promete aislamiento frente al owner de la base.
