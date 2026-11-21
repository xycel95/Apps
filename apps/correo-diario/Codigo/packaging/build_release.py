from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[2]
WORKSPACE = Path(os.environ.get('GITHUB_WORKSPACE', Path.cwd()))
OUT = WORKSPACE / '.correo-release'
OUT.mkdir(exist_ok=True)
TAG = 'correo-diario-v0.1.0-beta.1'
PREFIX = 'CorreoDiario_0.1_beta'
BASE = 'https://github.com/xycel95/Apps/releases/download/' + TAG + '/'
RELEASE = 'https://github.com/xycel95/Apps/releases/tag/' + TAG
CODE = ROOT / 'Codigo'
EXE = WORKSPACE / '.correo-publish' / 'CorreoDiario.exe'
data = EXE.read_bytes()
assert data[:2] == b'MZ'
offset = struct.unpack_from('<I', data, 0x3c)[0]
assert data[offset:offset+4] == b'PE\0\0'
assert struct.unpack_from('<H', data, offset+4)[0] == 0x8664
assert struct.unpack_from('<H', data, offset+24+68)[0] == 2
assert b'WebView2Loader.dll' in data
shutil.copyfile(EXE, OUT / (PREFIX + '.exe'))

log = (OUT / 'PRUEBAS.txt').read_text(encoding='utf-8-sig')
matches = re.findall(r'(\d+)/(\d+) comprobaciones superadas\.', log)
assert matches and matches[-1][0] == matches[-1][1] == '23'
report_path = OUT / 'VALIDACION-WINDOWS.json'
report = json.loads(report_path.read_text(encoding='utf-8-sig'))
assert report['native_startup_verified'] and not report['deletion_checkbox_default_checked']
report['core_tests_passed'] = 23
report['prerelease'] = True
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

with zipfile.ZipFile(OUT / (PREFIX + '.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=8) as archive:
    archive.write(EXE, 'CorreoDiario.exe')
    archive.write(CODE / 'LEEME.txt', 'LEEME.txt')
    archive.write(CODE / 'LICENSE.txt', 'LICENSE.txt')
    archive.write(report_path, report_path.name)
    for file in sorted((CODE / 'licencias').iterdir()): archive.write(file, 'licencias/' + file.name)

with zipfile.ZipFile(OUT / (PREFIX + '_fuentes.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=8) as archive:
    for file in sorted(CODE.rglob('*')):
        relative = file.relative_to(CODE)
        if file.is_file() and not any(part in ['bin', 'obj', '.git'] for part in relative.parts):
            archive.write(file, 'CorreoDiario/' + relative.as_posix())

def info(name):
    file = OUT / name
    return {'available': True, 'filename': name, 'url': BASE + name,
            'sha256': hashlib.sha256(file.read_bytes()).hexdigest(), 'size_bytes': file.stat().st_size,
            'hosting': 'github-release', 'release_tag': TAG, 'release_url': RELEASE,
            'integrity_url': BASE + 'SHA256SUMS.txt'}

manifest_path = ROOT / 'app.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
manifest['download'] = manifest['executable'] = info(PREFIX + '.exe')
manifest['zip_download'] = info(PREFIX + '.zip')
manifest['source_download'] = info(PREFIX + '_fuentes.zip')
manifest['validation'] = report
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
hashes = []
for file in sorted(OUT.iterdir()):
    if file.name != 'SHA256SUMS.txt' and file.is_file():
        hashes.append(hashlib.sha256(file.read_bytes()).hexdigest() + '  ' + file.name)
(OUT / 'SHA256SUMS.txt').write_text('\n'.join(hashes) + '\n', encoding='utf-8')
shutil.copyfile(OUT / 'SHA256SUMS.txt', ROOT / 'descargas' / 'SHA256SUMS.txt')
shutil.copyfile(report_path, ROOT / 'docs' / report_path.name)
(ROOT / 'descargas' / 'README.md').write_text('''# Descargar

**CorreoDiario 0.1 beta · Windows de 64 bits.**

- [Descargar el ejecutable](%s)
- [Descargar el ZIP con el ejecutable, guía y licencias](%s)
- [Descargar el código fuente](%s)
- [Comprobar SHA-256](%s)
- [Ver la publicación](%s)

Si eliges el ZIP, descomprímelo y abre `CorreoDiario.exe`. Puedes mover ese
EXE a una carpeta compartida. Requiere Microsoft Edge WebView2 Runtime en
el equipo; .NET está incluido y no se instala una extensión en el navegador.

El modo clásico requiere Outlook clásico instalado y con una cuenta
configurada. El modo web admite indicar la dirección de Outlook.

**Validación:** 23 pruebas automáticas superadas en Windows, arranque del EXE
y casilla de borrado desmarcada comprobados. Siguen pendientes la impresión
real, la autenticación y las cuentas personales/laborales. No se garantiza
cualquier servidor de Outlook. [Informe](../docs/VALIDACION-WINDOWS.json).

El borrado es opcional y mueve a Eliminados después de verificar toda la
exportación. No vacía esa carpeta; puede seguir ocupando cuota.
''' % (BASE+PREFIX+'.exe', BASE+PREFIX+'.zip', BASE+PREFIX+'_fuentes.zip', BASE+'SHA256SUMS.txt', RELEASE), encoding='utf-8')
print('Prepared release files:', ', '.join(file.name for file in sorted(OUT.iterdir())))
