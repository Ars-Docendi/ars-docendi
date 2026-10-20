-- Fixture v1 exclusivamente sintético. :n = 20000 o 100000.
-- UUID determinístico por namespace; sin SGA, secretos ni cuentas reales.
CREATE FUNCTION public.bench_id(text, integer) RETURNS uuid LANGUAGE sql IMMUTABLE AS $$ SELECT md5($1 || ':' || $2)::uuid $$;
INSERT INTO identity.personas(id,documento,nombre,apellido)
SELECT bench_id('persona',i),'fixture-'||i,'Sintético','Fixture' FROM generate_series(1,:n) i;
INSERT INTO identity.users(id,azure_oid,azure_tid,upn,display_name,persona_id)
SELECT bench_id('usuario',i),bench_id('oid',i),bench_id('tenant',1),'fixture-'||i||'@example.invalid','Fixture',bench_id('persona',i) FROM generate_series(1,2000) i;
INSERT INTO identity.materias(id,code,name) SELECT bench_id('materia',i),lpad(i::text,5,'0'),'Fixture' FROM generate_series(1,1000) i;
INSERT INTO identity.carreras(id,code,name) SELECT bench_id('carrera',i),'fixture-'||i,'Fixture' FROM generate_series(1,20) i;
INSERT INTO identity.roles(id,code,name,scope) SELECT bench_id('rol',i),'fixture-'||i,'Fixture '||i,'global' FROM generate_series(1,10) i;
INSERT INTO designaciones.periodos(id,nombre,carga_desde,carga_hasta,impacto_desde,impacto_hasta)
SELECT bench_id('periodo',i),'Fixture '||i,'2020-01-01','2020-12-31','2020-01-01','2020-12-31' FROM generate_series(1,10) i;
INSERT INTO designaciones.pedidos(id,numero,periodo_id,persona_id,novedad,estado,prioritario,cargo_solicitado_id,dedicacion_solicitada_id,horas,materia_id,carrera_id,created_at)
SELECT bench_id('pedido',i),'fixture-'||i,bench_id('periodo',1+(i-1)/10000%10),bench_id('persona',i),'Alta',
CASE WHEN i%20=0 THEN 'en_revision_coordinador' WHEN i%2=0 THEN 'cancelado' ELSE 'rechazado' END,
i%7=0,(SELECT id FROM designaciones.cargos ORDER BY codigo LIMIT 1),
(SELECT id FROM designaciones.dedicaciones ORDER BY codigo LIMIT 1),10,
bench_id('materia',1+i%1000),bench_id('carrera',1+i%20),'2020-01-01'::timestamptz+i*interval '1 minute'
FROM generate_series(1,:n) i;
INSERT INTO designaciones.designaciones(id,persona_id,cargo_id,dedicacion_id,horas,vigente_desde,vigente_hasta,materia_id,carrera_id,origen_pedido_id)
SELECT bench_id('designacion',i),bench_id('persona',1+(i-1)%20000),
(SELECT id FROM designaciones.cargos ORDER BY codigo LIMIT 1),(SELECT id FROM designaciones.dedicaciones ORDER BY codigo LIMIT 1),10,
'2010-01-01'::date+((i-1)/20000)*400,
CASE WHEN i> :n-20000 THEN NULL ELSE '2010-01-01'::date+((i-1)/20000)*400+300 END,
bench_id('materia',1+i%1000),bench_id('carrera',1+i%20),CASE WHEN i%5=0 THEN bench_id('pedido',i) END
FROM generate_series(1,:n) i;
INSERT INTO designaciones.pedido_historial(id,pedido_id,accion,rol_id,actor_id,etapa)
SELECT bench_id('historial',i),bench_id('pedido',i),'crear',bench_id('rol',CASE WHEN i%100=0 THEN 10 ELSE 1 END),
bench_id('usuario',1+i%2000),'borrador' FROM generate_series(1,:n) i;
INSERT INTO designaciones.idempotencia_comandos(id,clave,actor_id,ruta,pedido_id,request_hash,status_code,response_body)
SELECT bench_id('idempotencia',i),bench_id('clave',i),bench_id('usuario',1+i%2000),'/fixture',bench_id('pedido',i),'fixture',200,'{}' FROM generate_series(1,:n) i;
INSERT INTO tareas.proyectos(id,nombre,fecha_inicio,fecha_fin,responsable_id,estado_id)
SELECT bench_id('proyecto',i),'Fixture','2020-01-01','2021-01-01',bench_id('usuario',1+i%2000),
(SELECT id FROM tareas.estados_proyecto WHERE codigo='abierto') FROM generate_series(1,1000) i;
INSERT INTO tareas.tareas(id,titulo,fecha_inicio,fecha_fin,responsable_id,creado_por_id,proyecto_id,tarea_padre_id,estado_id,prioridad_id,tipo_id)
SELECT bench_id('tarea',i),'Fixture','2020-01-01'::date+i%365,'2022-01-01',
bench_id('usuario',CASE WHEN i<=50 THEN 2000 ELSE 1+i%1999 END),bench_id('usuario',1),
bench_id('proyecto',1+i%1000),CASE WHEN i%10<>1 THEN bench_id('tarea',1+(i-1)/10*10) END,
(SELECT id FROM tareas.estados_tarea WHERE codigo='pendiente'),(SELECT id FROM tareas.prioridades WHERE codigo='media'),
(SELECT id FROM tareas.tipos_tarea WHERE codigo='academica') FROM generate_series(1,:n) i;
INSERT INTO tareas.tarea_relaciones(tarea_id,relacionada_id)
SELECT least(bench_id('tarea',i),bench_id('tarea',i+1)),greatest(bench_id('tarea',i),bench_id('tarea',i+1)) FROM generate_series(1,:n-1) i;
VACUUM ANALYZE;
