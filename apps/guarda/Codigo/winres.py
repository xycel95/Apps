"""Conserva los recursos nativos de mcs y agrega el manifiesto de Windows.

mcs 6.8 acepta /win32manifest pero no lo incorpora. Se extraen sus iconos y
versiones, se añade RT_MANIFEST y se pasa el conjunto mediante /win32res.
"""
from pathlib import Path
import struct


def resources(executable):
    data = Path(executable).read_bytes()
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    if data[:2] != b'MZ' or data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('El archivo no es un ejecutable PE.')
    optional = pe + 24
    if struct.unpack_from('<H', data, optional)[0] != 0x10B:
        raise ValueError('Se esperaba PE32 administrado AnyCPU.')
    count = struct.unpack_from('<H', data, pe + 6)[0]
    section_start = optional + struct.unpack_from('<H', data, pe + 20)[0]
    section_list = [struct.unpack_from('<IIII', data, section_start + i * 40 + 8)
                    for i in range(count)]
    if any(raw + raw_size > len(data) for virtual_size, virtual, raw_size, raw in section_list):
        raise ValueError('Ejecutable PE incompleto: hay secciones truncadas.')

    def offset(rva):
        for virtual_size, virtual, raw_size, raw in section_list:
            if virtual <= rva < virtual + max(virtual_size, raw_size):
                return raw + rva - virtual
        raise ValueError('RVA de recurso fuera de las secciones.')

    root_rva, size = struct.unpack_from('<II', data, optional + 96 + 2 * 8)
    if not size:
        return []
    root = offset(root_rva)
    output = []

    def walk(relative, names):
        start = root + relative
        named, ids = struct.unpack_from('<HH', data, start + 12)
        for i in range(named + ids):
            name, entry = struct.unpack_from('<II', data, start + 16 + i * 8)
            if name & 0x80000000:
                at = root + (name & 0x7FFFFFFF)
                length = struct.unpack_from('<H', data, at)[0]
                name = data[at + 2:at + 2 + length * 2].decode('utf-16le')
            if entry & 0x80000000:
                walk(entry & 0x7FFFFFFF, names + [name])
            else:
                rva, length, codepage, reserved = struct.unpack_from('<IIII', data, root + entry)
                route = names + [name]
                if len(route) != 3:
                    raise ValueError('Jerarquía de recursos no compatible.')
                output.append((route[0], route[1], route[2], data[offset(rva):offset(rva) + length]))

    walk(0, [])
    return output


def _identifier(value):
    if isinstance(value, int):
        return struct.pack('<HH', 0xFFFF, value)
    return str(value).encode('utf-16le') + b'\0\0'


def _align(data):
    return data + b'\0' * (-len(data) % 4)


def write_with_manifest(executable, manifest, output):
    records = resources(executable)
    records = [record for record in records if record[0] != 24]
    records.append((24, 1, 0, Path(manifest).read_bytes()))
    # The resource stream starts with the standard empty ordinal record.
    result = struct.pack('<IIHHHHIHHII', 0, 32, 0xFFFF, 0, 0xFFFF, 0, 0, 0, 0, 0, 0)
    for resource_type, name, language, payload in records:
        identity = _align(_identifier(resource_type) + _identifier(name))
        metadata = struct.pack('<IHHII', 0, 0x1030, language, 0, 0)
        header = struct.pack('<II', len(payload), 8 + len(identity) + len(metadata)) + identity + metadata
        result += header + _align(payload)
    Path(output).write_bytes(result)


def verify_manifest(executable, expected):
    found = [data for kind, name, language, data in resources(executable) if kind == 24 and name == 1]
    if found != [Path(expected).read_bytes()]:
        raise ValueError('El manifiesto nativo de Windows no se ha incorporado correctamente.')
