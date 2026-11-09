"""Single-file entry point. Prepare the embedded app, wait for it, then clean up.

Only this small standard-library launcher is frozen. The verified camera app
keeps its own embedded interpreter, Qt plugins and isolated worker processes.
"""
from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tempfile
import time
import zipfile


class PayloadError(Exception):
    pass


class PreparationCancelled(Exception):
    pass


def safe_relative(name: str) -> PurePosixPath:
    if not isinstance(name, str) or not name or '\\' in name or ':' in name:
        raise PayloadError('Invalid payload path')
    parts = name.split('/')
    path = PurePosixPath(name)
    if path.is_absolute() or any(p in ('', '.', '..') for p in parts):
        raise PayloadError('Invalid payload path')
    return path


def extract_payload(archive: Path, destination: Path, progress=None) -> Path:
    """Extract exactly the files in the internal manifest, checking every hash."""
    with zipfile.ZipFile(archive) as payload:
        entries = [info for info in payload.infolist() if not info.is_dir()]
        names = [info.filename for info in entries]
        if len(names) != len(set(names)):
            raise PayloadError('Duplicate payload entries')
        manifests = [n for n in names if n.endswith('/ARCHIVOS_SHA256.json')]
        if len(manifests) != 1:
            raise PayloadError('Missing payload manifest')
        manifest_name = manifests[0]
        prefix = manifest_name.removesuffix('ARCHIVOS_SHA256.json')
        root_name = safe_relative(prefix.rstrip('/'))
        if len(root_name.parts) != 1:
            raise PayloadError('Invalid payload root')
        records = json.loads(payload.read(manifest_name))
        expected = {}
        for record in records:
            path = safe_relative(record['file'])
            name = prefix + path.as_posix()
            digest = record['sha256']
            if name in expected or not isinstance(record['size'], int) or record['size'] < 0:
                raise PayloadError('Invalid payload manifest')
            if not isinstance(digest, str) or len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest):
                raise PayloadError('Invalid payload hash')
            expected[name] = record
        if set(names) != set(expected) | {manifest_name}:
            raise PayloadError('Payload does not match its manifest')
        total = sum(info.file_size for info in entries)
        completed = 0
        for info in entries:
            path = safe_relative(info.filename)
            if (info.external_attr >> 16) & 0o170000 == 0o120000:
                raise PayloadError('Payload links are not supported')
            record = expected.get(info.filename)
            if record and info.file_size != record['size']:
                raise PayloadError('Payload file size mismatch')
            target = destination.joinpath(*path.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            digest = hashlib.sha256()
            with payload.open(info) as source, target.open('xb') as output:
                while chunk := source.read(1024 * 1024):
                    output.write(chunk)
                    digest.update(chunk)
                    completed += len(chunk)
                    if progress:
                        progress(completed, total)
            if record and digest.hexdigest() != record['sha256']:
                raise PayloadError('Payload file hash mismatch')
        root = destination.joinpath(*root_name.parts)
        required = ('runtime/pythonw.exe', 'runtime/python.exe', 'src/app.py',
                    'src/worker.py', 'bin/ffmpeg.exe', 'bin/ffprobe.exe')
        if not all((root / name).is_file() for name in required):
            raise PayloadError('Incomplete camera application')
        return root


class PreparationWindow:
    """Small native progress window; no extra GUI toolkit in the launcher."""
    def __init__(self):
        self.hwnd = None
        self.last_update = 0.0
        if os.name != 'nt':
            return
        from ctypes import wintypes
        self.wintypes = wintypes
        self.user = ctypes.WinDLL('user32', use_last_error=True)
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.GetModuleHandleW.argtypes = [wintypes.LPCWSTR]
        kernel.GetModuleHandleW.restype = wintypes.HINSTANCE
        self.user.CreateWindowExW.argtypes = [wintypes.DWORD, wintypes.LPCWSTR, wintypes.LPCWSTR,
            wintypes.DWORD, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
            wintypes.HWND, wintypes.HMENU, wintypes.HINSTANCE, ctypes.c_void_p]
        self.user.CreateWindowExW.restype = wintypes.HWND
        self.user.SendMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
        self.user.SendMessageW.restype = wintypes.LPARAM
        self.user.IsWindow.argtypes = [wintypes.HWND]
        self.user.DestroyWindow.argtypes = [wintypes.HWND]
        self.user.PeekMessageW.argtypes = [ctypes.POINTER(wintypes.MSG), wintypes.HWND,
                                         wintypes.UINT, wintypes.UINT, wintypes.UINT]
        self.user.TranslateMessage.argtypes = [ctypes.POINTER(wintypes.MSG)]
        self.user.DispatchMessageW.argtypes = [ctypes.POINTER(wintypes.MSG)]
        self.user.DispatchMessageW.restype = wintypes.LPARAM
        class Controls(ctypes.Structure):
            _fields_ = [('size', wintypes.DWORD), ('classes', wintypes.DWORD)]
        controls = Controls(ctypes.sizeof(Controls), 0x20)
        ctypes.WinDLL('comctl32').InitCommonControlsEx(ctypes.byref(controls))
        instance = kernel.GetModuleHandleW(None)
        x = max(0, (self.user.GetSystemMetrics(0) - 460) // 2)
        y = max(0, (self.user.GetSystemMetrics(1) - 150) // 2)
        self.hwnd = self.user.CreateWindowExW(0x00040001, '#32770', 'Visor Tapo',
            0x90C80000, x, y, 460, 150, None, None, instance, None)
        if not self.hwnd:
            return
        text = self.user.CreateWindowExW(0, 'STATIC', 'Preparando Visor Tapo…',
            0x50000000, 22, 20, 410, 24, self.hwnd, None, instance, None)
        self.bar = self.user.CreateWindowExW(0, 'msctls_progress32', '',
            0x50000001, 22, 55, 405, 20, self.hwnd, None, instance, None)
        gdi = ctypes.WinDLL('gdi32')
        gdi.GetStockObject.restype = wintypes.HANDLE
        font = gdi.GetStockObject(17)
        self.user.SendMessageW(text, 0x0030, font, 1)
        self.user.SendMessageW(self.bar, 0x0406, 0, 100)
        self.update(0, 1)

    def update(self, completed, total):
        if not self.hwnd:
            return
        now = time.monotonic()
        if now - self.last_update < 0.04 and completed < total:
            return
        self.last_update = now
        message = self.wintypes.MSG()
        while self.user.PeekMessageW(ctypes.byref(message), None, 0, 0, 1):
            self.user.TranslateMessage(ctypes.byref(message))
            self.user.DispatchMessageW(ctypes.byref(message))
        if not self.user.IsWindow(self.hwnd):
            self.hwnd = None
            raise PreparationCancelled()
        self.user.SendMessageW(self.bar, 0x0402, min(100, completed * 100 // max(1, total)), 0)

    def close(self):
        if self.hwnd:
            self.user.DestroyWindow(self.hwnd)
            self.hwnd = None


def child_environment(resource_directory: Path) -> dict:
    env = dict(os.environ)
    for key in tuple(env):
        if key.startswith('_PYI_') or key == '_MEIPASS2':
            env.pop(key, None)
    # Do not inherit the frozen launcher's DLL paths into the embedded Python.
    if os.name == 'nt':
        ctypes.WinDLL('kernel32').SetDllDirectoryW(None)
    resource = str(resource_directory).casefold()
    env['PATH'] = os.pathsep.join(part for part in env.get('PATH', '').split(os.pathsep)
                                if not part.casefold().startswith(resource + os.sep) and part.casefold() != resource)
    env['PYTHONIOENCODING'] = 'utf-8'
    return env


def failure(stage, exception):
    try:
        directory = Path(os.environ.get('VISOR_TAPO_DATA') or os.environ.get('VISOR_C230_DATA') or
                         str(Path(os.environ.get('LOCALAPPDATA', Path.home())) / 'VisorC230'))
        directory.mkdir(parents=True, exist_ok=True)
        (directory / 'portable-error.json').write_text(json.dumps(
            {'stage': stage, 'exception_type': type(exception).__name__}), encoding='utf-8')
    except OSError:
        pass
    if os.name == 'nt':
        ctypes.windll.user32.MessageBoxW(None,
            'No se ha podido abrir Visor Tapo. Cierra la aplicación y vuelve a abrirla. '
            'Si continúa el problema, descarga otra vez el ejecutable completo. '
            'Comprueba también que Windows tenga espacio temporal disponible.', 'Visor Tapo', 0x10)


def main(argv=None):
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('--self-test-report', type=Path)
    args = parser.parse_args(argv)
    resource = Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent))
    preparation = PreparationWindow()
    stage = 'prepare'
    workspace = None
    report = None
    try:
        # Keep all prepared files in the bootloader's own temporary area.
        with tempfile.TemporaryDirectory(prefix='app-', dir=resource) as temporary:
            workspace = Path(temporary)
            root = extract_payload(resource / 'payload.zip', workspace, preparation.update)
            preparation.close()
            env = child_environment(resource)
            stage = 'run'
            script = root / ('tests/native_portable_smoke.py' if args.self_test_report else 'src/app.py')
            command = [str(root / 'runtime/pythonw.exe'), str(script)]
            if args.self_test_report:
                # A developer self-test must never replace normal user settings.
                test_data = env.pop('VISOR_TAPO_SELFTEST_DATA', str(workspace / 'self-test-data'))
                env['VISOR_TAPO_DATA'] = str(Path(test_data).resolve())
                env['VISOR_TAPO_SELFTEST_ACTIVE'] = '1'
                child_report = workspace / 'smoke-report.json'
                command.append(str(child_report))
            process = subprocess.Popen(command, cwd=root, env=env, stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            result = process.wait()
            if args.self_test_report and child_report.is_file():
                report = json.loads(child_report.read_text(encoding='utf-8'))
            if result:
                raise PayloadError('Camera application exited with an error')
            if args.self_test_report:
                if not report or report.get('status') != 'passed':
                    raise PayloadError('Native smoke test failed')
        if report is not None:
            report.update(single_file=True, temporary_application_removed=not workspace.exists())
            args.self_test_report.resolve().write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        return 0
    except PreparationCancelled:
        return 0
    except Exception as error:
        if args.self_test_report:
            args.self_test_report.resolve().write_text(json.dumps(
                {**(report or {}), 'status': 'failed', 'stage': stage,
                 'launcher_exception_type': type(error).__name__}), encoding='utf-8')
        else:
            failure(stage, error)
        return 1
    finally:
        preparation.close()


if __name__ == '__main__':
    raise SystemExit(main())
