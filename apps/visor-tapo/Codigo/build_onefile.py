"""Build the self-contained Windows executable on native Windows x64."""
from __future__ import annotations
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

BASE = Path(__file__).resolve().parent


def build():
    if os.name != 'nt':
        raise SystemExit('El ejecutable único se compila en Windows x64.')
    import build_windows
    manifest = json.loads((BASE / 'build_inputs.json').read_text(encoding='utf-8'))
    version = manifest['app_version']
    if importlib.metadata.version('pyinstaller') != manifest['onefile']['version']:
        raise RuntimeError('PyInstaller version does not match build_inputs.json')
    build_windows.build()
    internal_zip = BASE / 'dist' / f'Visor_Tapo_{version}_Windows.zip'
    with zipfile.ZipFile(internal_zip) as payload:
        prefix = f'Visor_Tapo_{version}_Windows/'
        files = json.loads(payload.read(prefix + 'ARCHIVOS_SHA256.json'))
        if set(payload.namelist()) != {prefix + item['file'] for item in files} | {prefix + 'ARCHIVOS_SHA256.json'}:
            raise RuntimeError('Internal payload contains unlisted or missing files')
        for item in files:
            data = payload.read(prefix + item['file'])
            if len(data) != item['size'] or hashlib.sha256(data).hexdigest() != item['sha256']:
                raise RuntimeError('Internal payload checksum mismatch')
    work = BASE / 'build' / 'onefile'
    work.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(internal_zip, work / 'payload.zip')
    version_tuple = tuple(int(n) for n in version.split('.')) + (0,)
    version_file = work / 'version.txt'
    version_file.write_text(f'''VSVersionInfo(
 ffi=FixedFileInfo(filevers={version_tuple!r}, prodvers={version_tuple!r}, mask=0x3f, flags=0, OS=0x40004, fileType=1, subtype=0, date=(0, 0)),
 kids=[StringFileInfo([StringTable('040904B0', [
 StringStruct('CompanyName', 'XyceL'), StringStruct('FileDescription', 'Visor Tapo'),
 StringStruct('FileVersion', '{version}'), StringStruct('ProductVersion', '{version}'),
 StringStruct('ProductName', 'Visor Tapo'), StringStruct('InternalName', 'VisorTapo'),
 StringStruct('OriginalFilename', 'Visor_Tapo_{version}_Windows.exe')])]),
 VarFileInfo([VarStruct('Translation', [1033, 1200])])])
''', encoding='utf-8')
    pyinstaller = importlib.metadata.distribution('pyinstaller')
    licenses = [pyinstaller.locate_file(item) for item in pyinstaller.files
                if Path(item).name.upper() == 'COPYING.TXT']
    if not licenses:
        raise RuntimeError('Missing PyInstaller distribution license')
    args = [sys.executable, '-m', 'PyInstaller', '--noconfirm', '--clean', '--onefile',
            '--windowed', '--noupx', '--disable-windowed-traceback',
            '--name', f'Visor_Tapo_{version}_Windows', '--distpath', str(BASE / 'dist'),
            '--workpath', str(work / 'work'), '--specpath', str(work),
            '--icon', str(BASE / 'src/app.ico'), '--manifest', str(BASE / 'src/app.manifest'),
            '--version-file', str(version_file), '--add-data', str(work / 'payload.zip') + ':.',
            '--add-data', str(licenses[0]) + ':licencias/PyInstaller',
            str(BASE / 'portable_launcher.py')]
    subprocess.run(args, cwd=BASE, check=True)
    executable = BASE / 'dist' / f'Visor_Tapo_{version}_Windows.exe'
    import pefile
    pe = pefile.PE(str(executable))
    if pe.FILE_HEADER.Machine != 0x8664 or pe.OPTIONAL_HEADER.Subsystem != 2:
        raise RuntimeError('Expected a native Windows x64 GUI executable')
    pe.close()
    print(json.dumps({'onefile_exe': str(executable), 'size_bytes': executable.stat().st_size,
                      'sha256': build_windows.sha256(executable), 'pyinstaller': manifest['onefile']['version']}))


if __name__ == '__main__':
    build()
