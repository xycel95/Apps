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
TAG = 'correo-diario-v0.4.1-beta.1'
PREFIX = 'CorreoDiario_0.4.1_beta'
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
assert matches and matches[-1][0] == matches[-1][1] == '71'
report_path = OUT / 'VALIDACION-WINDOWS.json'
report = json.loads(report_path.read_text(encoding='utf-8-sig'))
assert report['native_startup_verified'] and not report['deletion_checkbox_default_checked']
assert report['existing_browser_ui_verified'] and report['embedded_login_ui_absent'] and report['date_range_controls_verified']
wire = (OUT / 'BROWSER-BRIDGE.txt').read_text(encoding='utf-8-sig')
assert 'PASS Conexión local' in wire
report['core_tests_passed'] = 71
report['browser_wire_test_verified'] = True
auth = (OUT / 'OUTLOOK-AUTH.txt').read_text(encoding='utf-8-sig')
assert 'PASS Edge real y servicio HTTPS sintético' in auth
report['edge_synthetic_attachment_verified'] = True
report['outlook_authentication_synthetic_verified'] = True
report['outlook_authentication_verified'] = False
native_pdf = (OUT / 'PDF-REAL.txt').read_text(encoding='utf-8-sig')
assert 'PASS Flujo Windows con WebView2 real' in native_pdf
report['outlook_search_contract_verified'] = True
report['outlook_search_synthetic_verified'] = True
report['synthetic_mail_export_verified'] = True
report['native_pdf_render_synthetic_verified'] = True
report['pdf_render_verified'] = True
report['real_outlook_export_verified'] = False
report['date_range_synthetic_verified'] = True
report['attachment_contract_verified'] = True
assert 'tamaño de almacén 344, contenido 8 bytes; segunda lectura y SHA-256 verificados' in native_pdf
report['outlook_store_size_semantics_verified'] = True
report['attachment_source_readback_verified'] = True
report['limitation'] = 'Arranque Windows y flujo con Edge, servicio OWA sintético, intervalos de fechas inclusivos, contrato de adjuntos, búsquedas paginadas, PDF reales de WebView2, tamaños de almacén separados de contenido y segunda lectura de adjuntos comprobados. Pendientes la exportación con la cuenta real de Outlook y el perfil clásico del usuario.'
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
manifest['version'] = '0.4.1-beta.1'
manifest['release_date'] = '2026-11-21'
manifest['summary'] = 'Exporta correos y adjuntos desde la pestaña de Outlook ya abierta en Edge o Chrome, con borrado opcional tras verificar.'
manifest['authentication'] = {'flow': 'existing-browser-session', 'embedded_login': False, 'browser_extension_required': False, 'browser_user_permission_required': True, 'default_browser_profile': True, 'observed_request_authorization': True, 'session_credentials_persisted': False}
manifest['development_version'] = None
manifest['requirements'] = ['Microsoft Edge WebView2 Runtime en el equipo', 'Edge/Chrome con permiso de conexión, o Outlook clásico configurado']
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

**CorreoDiario 0.4.1 beta · Windows de 64 bits.**

- [Descargar el ejecutable](%s)
- [Descargar el ZIP con el ejecutable, guía y licencias](%s)
- [Descargar el código fuente](%s)
- [Comprobar SHA-256](%s)
- [Ver la publicación](%s)

Si eliges el ZIP, descomprímelo y abre `CorreoDiario.exe`. Puedes mover ese
EXE a una carpeta compartida. Requiere Microsoft Edge WebView2 Runtime en
el equipo; .NET está incluido y no se instala una extensión en el navegador.

Esta versión usa la pestaña de Outlook que ya tienes abierta en Edge o Chrome.
Pulsa **Cómo conectar** para ver la opción del navegador que debes activar y
acepta su aviso de conexión. Después selecciona tu pestaña. Elige **Desde** y **Hasta**: ambos días se incluyen.
Para un solo día, pon la misma fecha en los dos campos. No hay un formulario
de inicio de sesión dentro de CorreoDiario ni se instala una extensión.
Si Outlook pide renovar la conexión, pulsa **Actualizar** en su pestaña y vuelve
a **Usar esta pestaña**. Las cabeceras de sesión se mantienen solo en memoria.
El modo clásico sigue disponible para perfiles configurados en Outlook clásico.

**Validación:** 71 pruebas automáticas, transporte WebSocket local, arranque
del EXE y flujo con Edge real frente a un servicio HTTPS sintético que exige
autorización y valida el contrato de búsqueda. Incluye búsquedas por intervalo de fechas y
carpeta, paginación, contrato de adjuntos, segunda lectura del contenido y PDF
reales de WebView2. Queda pendiente la
exportación con las cuentas reales del usuario. [Informe](../docs/VALIDACION-WINDOWS.json).

El borrado es opcional y mueve a Eliminados después de verificar toda la
exportación. No vacía esa carpeta; puede seguir ocupando cuota.
''' % (BASE+PREFIX+'.exe', BASE+PREFIX+'.zip', BASE+PREFIX+'_fuentes.zip', BASE+'SHA256SUMS.txt', RELEASE), encoding='utf-8')
print('Prepared release files:', ', '.join(file.name for file in sorted(OUT.iterdir())))
