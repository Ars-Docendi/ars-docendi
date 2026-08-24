#!/usr/bin/env bash
# Test de humo de los roles de solo lectura del asistente. Read-only: no crea,
# no modifica y no borra nada.
#
# Uso:
#   verificar-roles-asistente.sh <ambiente>
#
# Variables:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (libpq)
#
# Corre en dos momentos de spin-up.sh y comprueba lo mismo en los dos:
#   - después del provisioning (paso 1), con la base vacía: los roles existen y
#     nacieron con los atributos correctos;
#   - después de las migraciones (paso 3), con las tablas ya creadas: el DDL que
#     acaba de correr no le dio al asistente ningún privilegio de escritura.
#
# El control "ninguna contraseña se comparte entre ambientes" NO es verificable
# desde el cluster: SCRAM guarda un hash con salt propio por rol, así que dos
# contraseñas iguales producen verificadores distintos. Esa garantía depende de
# qué valores inyecta quien llama (ver infra/README.md).

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"

base="$(nombre_base "$ambiente")"
rol_ro="$(rol_asistente "$ambiente" basico)"
rol_ro_pii="$(rol_asistente "$ambiente" pii)"
roles_sql="'${rol_ro}', '${rol_ro_pii}'"

existe_base "$base" || fatal "msg=\"no existe la base del ambiente\" base=\"${base}\""

fallas=0
comprobar() {   # comprobar <control> <esperado> <obtenido>
  local control="$1" esperado="$2" obtenido="$3"
  if [[ "$obtenido" == "$esperado" ]]; then
    log_info msg="ok" control="$control"
  else
    log_error msg="FALLA" control="$control" esperado="$esperado" obtenido="$obtenido"
    fallas=$((fallas + 1))
  fi
}

log_info msg="verificando roles del asistente" ambiente="$ambiente" base="$base"

# 1. Los dos roles existen.
comprobar "los dos roles existen" "2" \
  "$(psql_admin -c "SELECT count(*) FROM pg_roles WHERE rolname IN (${roles_sql});")"

# 2. Atributos: LOGIN y ningún privilegio de cluster. NOBYPASSRLS es el que
#    importa de verdad: sin él, las policies de RLS no contienen al asistente.
for rol in "$rol_ro" "$rol_ro_pii"; do
  comprobar "atributos del rol ${rol}" "t" \
    "$(psql_admin -c "SELECT rolcanlogin
                         AND NOT rolsuper
                         AND NOT rolcreatedb
                         AND NOT rolcreaterole
                         AND NOT rolreplication
                         AND NOT rolbypassrls
                         AND NOT rolinherit
                      FROM pg_roles WHERE rolname = '${rol}';")"
done

# 3. Privilegios sobre la base: CONNECT sí, CREATE y TEMPORARY no.
for rol in "$rol_ro" "$rol_ro_pii"; do
  comprobar "CONNECT sobre ${base} para ${rol}" "t" \
    "$(psql_admin -c "SELECT has_database_privilege('${rol}', '${base}', 'CONNECT');")"
  # Se comparan dos booleanos concatenados: `bool || text` los castea a su
  # representación textual, "false", no a la abreviada "f" de una columna sola.
  comprobar "sin CREATE ni TEMPORARY sobre ${base} para ${rol}" "false|false" \
    "$(psql_admin -c "SELECT has_database_privilege('${rol}', '${base}', 'CREATE')
                        || '|' ||
                      has_database_privilege('${rol}', '${base}', 'TEMPORARY');")"
done

# 4. PUBLIC no conserva privilegios sobre la base: si los conservara, el GRANT
#    CONNECT del punto 3 sería decorativo y cualquier rol del cluster —incluido
#    el de otro ambiente— podría conectarse.
comprobar "PUBLIC sin privilegios sobre ${base}" "0" \
  "$(psql_admin -c "SELECT count(*)
                    FROM pg_database d
                    CROSS JOIN LATERAL aclexplode(d.datacl) a
                    WHERE d.datname = '${base}' AND a.grantee = 0;")"

# 5. search_path fijado para los dos roles en esta base.
comprobar "search_path fijado en ${base}" "2" \
  "$(psql_admin -c "SELECT count(*)
                    FROM pg_db_role_setting s
                    JOIN pg_roles r ON r.oid = s.setrole
                    JOIN pg_database d ON d.oid = s.setdatabase
                    WHERE d.datname = '${base}'
                      AND r.rolname IN (${roles_sql})
                      AND EXISTS (SELECT 1 FROM unnest(s.setconfig) c
                                  WHERE c LIKE 'search_path=%');")"

# 6. Ningún privilegio de mutación sobre ninguna tabla, ni ahora ni por default
#    privileges, ni CREATE sobre ningún schema. Se lee de los catálogos de ESTA
#    base (pg_class y pg_namespace son por base, no de cluster).
comprobar "sin privilegios de mutación sobre tablas" "0" \
  "$(psql_base "$base" -c "SELECT count(*)
                           FROM pg_class c
                           CROSS JOIN LATERAL aclexplode(c.relacl) a
                           JOIN pg_roles g ON g.oid = a.grantee
                           WHERE g.rolname IN (${roles_sql})
                             AND a.privilege_type IN ('INSERT','UPDATE','DELETE','TRUNCATE','REFERENCES','TRIGGER');")"

comprobar "sin privilegios de mutación por default privileges" "0" \
  "$(psql_base "$base" -c "SELECT count(*)
                           FROM pg_default_acl d
                           CROSS JOIN LATERAL aclexplode(d.defaclacl) a
                           JOIN pg_roles g ON g.oid = a.grantee
                           WHERE g.rolname IN (${roles_sql})
                             AND a.privilege_type IN ('INSERT','UPDATE','DELETE','TRUNCATE','REFERENCES','TRIGGER');")"

comprobar "sin CREATE sobre ningún schema" "0" \
  "$(psql_base "$base" -c "SELECT count(*)
                           FROM pg_namespace n
                           CROSS JOIN LATERAL aclexplode(n.nspacl) a
                           JOIN pg_roles g ON g.oid = a.grantee
                           WHERE g.rolname IN (${roles_sql})
                             AND a.privilege_type = 'CREATE';")"

# 7. Si las tablas ya existen —o sea, después de las migraciones—, los GRANT del
#    asistente tienen que haber corrido. Este control detecta el caso que los
#    tests de CI no pueden ver: que la migración funcione, pero no se haya
#    ejecutado en ESTE ambiente. Antes de migrar no hay tablas y se saltea.
tablas="$(psql_base "$base" -c "SELECT count(*)
                                FROM information_schema.tables
                                WHERE table_schema IN ('identity', 'designaciones')
                                  AND table_type = 'BASE TABLE';")"

if [[ "$tablas" -gt 0 ]]; then
  comprobar "los GRANT de lectura ya se aplicaron" "t" \
    "$(psql_base "$base" -c "SELECT count(*) > 0
                             FROM information_schema.column_privileges
                             WHERE grantee IN (${roles_sql})
                               AND privilege_type = 'SELECT';")"
else
  log_info msg="base sin tablas todavía: no se controlan los GRANT de lectura" base="$base"
fi

if (( fallas > 0 )); then
  fatal "msg=\"verificación de roles del asistente FALLIDA\" ambiente=\"${ambiente}\" fallas=\"${fallas}\""
fi

log_info msg="verificación de roles del asistente OK" ambiente="$ambiente" base="$base"
