---
name: check-deploy
description: Verificar health del sistema post-deploy. Llama health endpoints (/api/*/ping), valida códigos de respuesta y latencia. Aplica a VMs universitarias via curl o SSH. Read-only.
argument-hint: [<env: local | staging | prod>]
---

# Check deploy

## Cuándo usar

- Inmediatamente después de un release.
- Periódico (uptime check manual).
- Cuando se sospecha que algo está caído pero sin alarma.

## Flujo

### 1. Determinar base URL del ambiente

| Env       | Base URL                  |
| --------- | ------------------------- |
| `local`   | `http://localhost:5000`   |
| `staging` | TBD — depende del cliente |
| `prod`    | TBD — depende del cliente |

(Si no está definida, parar y pedir info al equipo.)

### 2. Health checks

Por cada módulo, llamar `/api/<modulo>/ping`:

```bash
for modulo in designaciones aulas portal tareas; do
  echo "=== $modulo ==="
  curl -sS -w "HTTP %{http_code} | %{time_total}s\n" -o /tmp/ping-$modulo.json "$BASE_URL/api/$modulo/ping"
  cat /tmp/ping-$modulo.json
  echo
done
```

Validar:

- HTTP 200 ✓
- Latencia razonable (depende SLA del cliente; default warning > 1s)
- Payload tiene `{ "module": "<modulo>", "timestamp": "..." }`
- Timestamp es reciente (no cacheado de hace horas).

### 3. Frontend health (si aplica)

```bash
curl -sS -w "HTTP %{http_code}\n" -o /dev/null "$BASE_URL_FRONTEND"
```

Validar HTTP 200 + el bundle se sirve.

### 4. DB connectivity (si tenés acceso)

Via SSH a la VM:

```bash
sudo -u postgres psql -d arsdocendi -c "SELECT 1;"
```

### 5. Reporte

| Módulo        | HTTP | Latencia | Status   |
| ------------- | ---- | -------- | -------- |
| designaciones | 200  | 120ms    | OK       |
| aulas         | 200  | 95ms     | OK       |
| portal        | 503  | 5000ms   | **DOWN** |
| tareas        | 200  | 110ms    | OK       |

Si hay módulos down → escalar a `/debug-production`.

## Hard rules

- **Read-only**: NO modificar estado.
- Si todo OK: registrar timestamp en log de operación.
- Si algo no OK: notificar al equipo + escalar.

## Arguments

`$ARGUMENTS` — env target (`local`, `staging`, `prod`). Default: `local`.
