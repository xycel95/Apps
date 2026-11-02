"""One requested release publication, using the runner's short-lived token."""
import hashlib
import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

BASE = Path(__file__).resolve().parent
VERSION = json.loads((BASE / 'build_inputs.json').read_text())['app_version']
REPOSITORY = os.environ['GITHUB_REPOSITORY']
COMMIT = os.environ['GITHUB_SHA']
TOKEN = os.environ['GITHUB_TOKEN']
if REPOSITORY != 'xycel95/Apps' or os.environ.get('GITHUB_REF') != 'refs/heads/visor-tapo':
    raise SystemExit('Publicación limitada a xycel95/Apps, rama visor-tapo.')
if VERSION != '1.1.0' or not re.fullmatch(r'[0-9a-f]{40}', COMMIT):
    raise SystemExit('Versión o revisión no válida para esta publicación inicial.')


def request(url, method='GET', data=None, content_type='application/json'):
    if isinstance(data, dict):
        data = json.dumps(data).encode()
    req = urllib.request.Request(url, method=method, data=data, headers={
        'Authorization': 'Bearer ' + TOKEN, 'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28', 'Content-Type': content_type,
        'User-Agent': 'Visor-Tapo-release',
    })
    try:
        with urllib.request.urlopen(req, timeout=180) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(f'GitHub ha rechazado la operación de publicación: HTTP {error.code}') from None


def digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def main():
    dist = BASE / 'dist'
    files = [dist / f'Visor_Tapo_{VERSION}_Windows.zip', dist / f'Visor_Tapo_Codigo_{VERSION}.zip']
    for path in files:
        with zipfile.ZipFile(path) as archive:
            if archive.testzip():
                raise RuntimeError('ZIP con un fallo de integridad: ' + path.name)
    checksums = dist / 'SHA256SUMS.txt'
    checksums.write_text(''.join(f'{digest(path)}  {path.name}\n' for path in files), encoding='ascii')
    files.append(checksums)
    release = request(f'https://api.github.com/repos/{REPOSITORY}/releases', 'POST', {
        'tag_name': f'visor-tapo-v{VERSION}', 'target_commitish': COMMIT,
        'name': f'Visor Tapo {VERSION}', 'draft': True, 'prerelease': False,
        'make_latest': 'false',
        'body': 'Visor Tapo para Windows 10/11 x64. Extrae todo el ZIP y abre Visor Tapo.exe.\n\n'
                'Directo local y consulta de grabaciones microSD. Incluye lista técnica de 133 modelos, kits y variantes, '
                'con límites por función. El acceso a microSD mediante pytapo depende del hardware y firmware.\n\n'
                'Proyecto independiente, no oficial de TP-Link/Tapo.\n\n'
                'Guía y requisitos: https://github.com/xycel95/Apps/blob/visor-tapo/apps/visor-tapo/docs/README.md\n'
                'Compatibilidad: https://github.com/xycel95/Apps/blob/visor-tapo/apps/visor-tapo/docs/COMPATIBILIDAD.md\n'
                'Notas: https://github.com/xycel95/Apps/blob/visor-tapo/apps/visor-tapo/notas-de-parche/README.md\n\n'
                'Compilación cruzada para Windows y comprobaciones en Linux; no se presenta como ensayo físico de todos los modelos.',
    })
    uploaded = []
    endpoint = release['upload_url'].split('{', 1)[0]
    for path in files:
        expected = digest(path)
        asset = request(endpoint + '?name=' + urllib.parse.quote(path.name), 'POST', path.read_bytes(),
                        'application/zip' if path.suffix == '.zip' else 'text/plain')
        if asset['state'] != 'uploaded' or asset['size'] != path.stat().st_size:
            raise RuntimeError('No se ha confirmado el archivo: ' + path.name)
        if asset.get('digest') and asset['digest'] != 'sha256:' + expected:
            raise RuntimeError('La huella publicada no coincide: ' + path.name)
        uploaded.append({'filename': path.name, 'size_bytes': path.stat().st_size, 'sha256': expected,
                         'url': asset['browser_download_url']})
    published = request(release['url'], 'PATCH', {'draft': False, 'make_latest': 'false'})
    result = {'version': VERSION, 'commit': COMMIT, 'release_url': published['html_url'], 'assets': uploaded}
    (dist / 'PUBLICACION.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
