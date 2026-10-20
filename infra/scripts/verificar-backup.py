#!/usr/bin/env python3
"""Verifica manifiesto y bytes locales sin ejecutar rutas del checksum recibido."""
import hashlib
import json
from pathlib import Path
import re
import sys


def verificar(ruta):
    raiz = Path(ruta).resolve()
    if (raiz / 'status').read_text() != 'complete':
        raise ValueError('backup incompleto')
    m = json.loads((raiz / 'manifest.json').read_text())
    if m['format'] != 'arsdocendi-storage-backup/v1' or m['object_count'] != len(m['objects']):
        raise ValueError('manifiesto inválido')
    entradas = [m['database']] + m['objects']
    nombres = set()
    claves = set()
    for entrada in entradas:
        nombre = entrada['file']
        if nombre in nombres or not re.fullmatch(r'postgres\.dump|objects/[0-9]{8}\.bin', nombre):
            raise ValueError('ruta insegura o duplicada')
        nombres.add(nombre)
        archivo = raiz / nombre
        if archivo.is_symlink() or archivo.resolve().parent not in (raiz, raiz / 'objects'):
            raise ValueError('archivo fuera del backup')
        with archivo.open('rb') as f:
            digest = hashlib.file_digest(f, 'sha256').hexdigest()
        if digest != entrada['sha256']:
            raise ValueError('hash incorrecto')
        if 'size' in entrada and archivo.stat().st_size != entrada['size']:
            raise ValueError('tamaño incorrecto')
        if 'key' in entrada:
            if entrada['key'] in claves:
                raise ValueError('clave duplicada')
            claves.add(entrada['key'])
    if m['total_bytes'] != sum(o['size'] for o in m['objects']):
        raise ValueError('total inválido')
    # El hash del manifiesto también está obligado por el índice de checksums.
    checksums = {}
    for linea in (raiz / 'checksums.sha256').read_text().splitlines():
        digest, nombre = linea.split('  ', 1)
        nombre = nombre.removeprefix('./')
        if not re.fullmatch(r'[a-f0-9]{64}', digest) or nombre in checksums:
            raise ValueError('checksums inválidos')
        checksums[nombre] = digest
    if set(checksums) != nombres | {'manifest.json'}:
        raise ValueError('índice de integridad incompleto')
    for nombre, digest in checksums.items():
        with (raiz / nombre).open('rb') as f:
            if hashlib.file_digest(f, 'sha256').hexdigest() != digest:
                raise ValueError('checksum incorrecto')
    return m


if __name__ == '__main__':
    try:
        verificar(sys.argv[1])
        print('backup verificado')
    except (ValueError, KeyError, OSError) as error:
        print(f'backup rechazado: {error}', file=sys.stderr)
        sys.exit(1)
