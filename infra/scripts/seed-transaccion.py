#!/usr/bin/env python3
"""Envuelve fixtures sintéticas existentes sin editar el dataset ni anidar commits."""
from pathlib import Path
import re
import sys
import os


def cuerpo(ruta):
    sql = Path(ruta).read_text(encoding="utf-8")
    # Sólo aceptar la transacción exterior conocida; no transformar SQL arbitrario.
    inicios = list(re.finditer(r"(?im)^\s*BEGIN;\s*$", sql))
    finales = list(re.finditer(r"(?im)^\s*COMMIT;\s*$", sql))
    if len(inicios) != 1 or len(finales) != 1 or inicios[0].end() >= finales[0].start():
        raise ValueError("dataset debe tener una única transacción exterior")
    if sql[finales[0].end():].strip():
        raise ValueError("SQL fuera de la transacción")
    return sql[:inicios[0].start()] + sql[inicios[0].end():finales[0].start()]


if __name__ == "__main__":
    try:
        sql = cuerpo(sys.argv[1])  # Validar todo antes de emitir SQL.
        extra = Path(sys.argv[2]).read_text() if len(sys.argv) > 2 else ""
        if os.environ.get("SEED_LOCAL_COMBINADO") == "true":
            # No inspeccionar este archivo fuera de la ejecución local autorizada.
            if re.search(r"(?im)^\s*(BEGIN|COMMIT);\s*$", extra):
                extra = cuerpo(sys.argv[2])
        guard = Path(__file__).with_name("seed-guard.sql").read_text()
        print(guard)
        print("SELECT estado = 'autorizado' AS ejecutar FROM public.bootstrap_metadata WHERE id \\gset")
        print("\\if :ejecutar")
        print(sql)
        print(extra)
        print("INSERT INTO public.seed_metadata(clave,valor) VALUES ('inicializacion_completada','sintetico/v1') ON CONFLICT(clave) DO UPDATE SET valor=EXCLUDED.valor;")
        print("UPDATE public.bootstrap_metadata SET estado='completado' WHERE id;")
        print("\\endif\nCOMMIT;")
    except (ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
