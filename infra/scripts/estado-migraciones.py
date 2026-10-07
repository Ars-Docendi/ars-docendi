#!/usr/bin/env python3
"""Valida el contrato seguro del CLI y compara estados antes de aplicar."""
import json
import sys


def leer(ruta, base):
    with open(ruta, encoding="utf-8") as archivo:
        estado = json.load(archivo)
    if estado.get("compatible") is not True or estado.get("baseDatos") != base:
        raise ValueError("estado incompatible o destino incorrecto")
    contextos = estado["contextos"]
    if not isinstance(contextos, list) or not contextos:
        raise ValueError("faltan contextos")
    nombres = set()
    total = 0
    for contexto in contextos:
        nombre = contexto["contexto"]
        if not isinstance(nombre, str) or not nombre or nombre in nombres:
            raise ValueError("contexto inválido o duplicado")
        nombres.add(nombre)
        for clave in ("disponibles", "aplicadas", "pendientes"):
            ids = contexto[clave]
            if not isinstance(ids, list) or any(not isinstance(i, str) for i in ids) or len(set(ids)) != len(ids):
                raise ValueError("inventario inválido")
        disponibles, aplicadas, pendientes = (contexto[k] for k in ("disponibles", "aplicadas", "pendientes"))
        if disponibles[:len(aplicadas)] != aplicadas or disponibles[len(aplicadas):] != pendientes:
            raise ValueError("historia discontinua")
        total += len(pendientes)
    if type(estado["pendientes"]) is not int or estado["pendientes"] != total:
        raise ValueError("total de pendientes inválido")
    return estado


if __name__ == "__main__":
    try:
        estado = leer(sys.argv[1], sys.argv[2])
        if len(sys.argv) > 3 and estado != leer(sys.argv[3], sys.argv[2]):
            raise ValueError("estado cambió desde el preview")
        print(estado["pendientes"])
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f"preflight rechazado: {error}", file=sys.stderr)
        sys.exit(1)
