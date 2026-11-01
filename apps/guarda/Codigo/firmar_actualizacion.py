"""Firma un canal de Guarda. La clave privada debe estar fuera del repositorio."""
import argparse
import base64
import hashlib
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--key', type=Path, required=True)
    parser.add_argument('--exe', type=Path, required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--notes', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+', args.version):
        raise SystemExit('La versión debe tener tres componentes numéricos.')
    repo_code = Path(__file__).resolve().parent
    repository = next((parent for parent in repo_code.parents if (parent / '.git').exists()), repo_code.parent)
    if args.key.resolve().is_relative_to(repository):
        raise SystemExit('La clave privada debe estar fuera del código publicado.')
    key = serialization.load_pem_private_key(args.key.read_bytes(), password=None)
    trusted = ET.fromstring((repo_code / 'update-public-key.xml').read_bytes())
    modulus = int.from_bytes(base64.b64decode(trusted.findtext('Modulus')), 'big')
    if key.public_key().public_numbers().n != modulus:
        raise SystemExit('La clave no corresponde a la clave pública integrada. No se rota automáticamente.')
    data = args.exe.read_bytes()
    if not data.startswith(b'MZ') or len(data) > 64 * 1024 * 1024:
        raise SystemExit('Ejecutable de Windows no válido o demasiado grande.')
    root = ET.Element('GuardaUpdate', {'schema': '1', 'app': 'guarda'})
    fields = {
        'Version': args.version,
        'Url': 'https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/versiones/' + args.version + '/Guarda.exe',
        'Sha256': hashlib.sha256(data).hexdigest(),
        'Size': str(len(data)),
        'Notes': args.notes.read_text(encoding='utf-8').replace('\r\n', '\n')
    }
    for name, value in fields.items(): ET.SubElement(root, name).text = value
    ET.indent(root, space='  ')
    manifest = ET.tostring(root, encoding='utf-8', xml_declaration=True) + b'\n'
    signature = key.sign(manifest, padding.PKCS1v15(), hashes.SHA256())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(manifest)
    Path(str(args.output) + '.sig').write_text(base64.b64encode(signature).decode() + '\n', encoding='ascii')
    key.public_key().verify(signature, manifest, padding.PKCS1v15(), hashes.SHA256())
    print('Canal firmado y verificado:', args.version, fields['Sha256'])


if __name__ == '__main__': main()
