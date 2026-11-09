"""Native acceptance test: move just the EXE, run it, then close normally."""
import ctypes
from ctypes import wintypes
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import time


def stop(process):
    if process.poll() is None:
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        process.wait(timeout=15)


def visible_app_windows(title):
    user = ctypes.WinDLL('user32', use_last_error=True)
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    user.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user.IsWindowVisible.argtypes = [wintypes.HWND]
    matches = []
    @callback_type
    def inspect(hwnd, _):
        text = ctypes.create_unicode_buffer(300)
        user.GetWindowTextW(hwnd, text, len(text))
        if text.value == title and user.IsWindowVisible(hwnd):
            matches.append(hwnd)
        return True
    user.EnumWindows(inspect, 0)
    return matches


def validate(executable, output):
    assert os.name == 'nt'
    original = executable.resolve()
    version = json.loads((Path(__file__).resolve().parent.parent / 'build_inputs.json').read_text(encoding='utf-8'))['app_version']
    with original.open('rb') as source:
        checksum = hashlib.file_digest(source, 'sha256').hexdigest()
    with tempfile.TemporaryDirectory(prefix='VisorTapo-native-') as temporary:
        base = Path(temporary)
        runtime_temp = base / 'temporary'; runtime_temp.mkdir()
        user_data = base / 'user-data'; user_data.mkdir()
        env = {**os.environ, 'VISOR_TAPO_DATA': str(user_data), 'QT_QPA_PLATFORM': 'windows',
               'TEMP': str(runtime_temp), 'TMP': str(runtime_temp), 'PYTHONUTF8': '1'}
        env.pop('PYTHONPATH', None); env.pop('PYTHONHOME', None)
        cases = []; current = original
        try:
            for number, name in enumerate(('Carpeta vacía áé', 'Otra ubicación ñ')):
                folder = base / name; folder.mkdir()
                moved = folder / ('Mi cámara.exe' if number else original.name)
                shutil.move(str(current), moved); current = moved
                report_path = base / f'case-{number}.json'
                process = subprocess.Popen([str(current), '--self-test-report', str(report_path)], cwd=folder, env=env)
                try:
                    code = process.wait(timeout=150)
                finally:
                    stop(process)
                report = json.loads(report_path.read_text(encoding='utf-8')) if report_path.exists() else {'status': 'missing'}
                print(json.dumps({'case': number + 1, 'exit_code': code, 'report': report}), flush=True)
                assert code == 0 and report['status'] == 'passed', report
                assert report['version'] == version and report['isolated_worker_verified']
                assert report['temporary_application_removed'] and report['dpapi_roundtrip_verified']
                if number:
                    assert report['settings_restored_after_move']
                assert sorted(p.name for p in folder.iterdir()) == [current.name]
                assert not list(runtime_temp.glob('_MEI*'))
                cases.append({'name': 'empty-folder' if not number else 'moved-and-renamed', **report,
                              'no_adjacent_files_created': True, 'bootloader_temporary_removed': True})
            process = subprocess.Popen([str(current)], cwd=current.parent, env=env)
            try:
                deadline = time.monotonic() + 100; windows = []
                while time.monotonic() < deadline and process.poll() is None:
                    windows = visible_app_windows(f'Visor Tapo · {version}')
                    if windows:
                        break
                    time.sleep(0.2)
                assert windows, 'The normal entry did not show the main window'
                user = ctypes.WinDLL('user32')
                user.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
                user.PostMessageW(windows[0], 0x0010, 0, 0)
                assert process.wait(timeout=30) == 0
            finally:
                stop(process)
            assert not list(runtime_temp.glob('_MEI*'))
            assert sorted(p.name for p in current.parent.iterdir()) == [current.name]
            cases.append({'name': 'normal-double-click-entry', 'native_window_visible': True,
                          'closed_normally': True, 'no_adjacent_files_created': True,
                          'bootloader_temporary_removed': True})
        finally:
            if current != original and current.exists():
                shutil.move(str(current), original)
        report = {'application': 'Visor Tapo', 'version': version, 'status': 'passed',
                  'platform': platform.platform(), 'python': platform.python_version(),
                  'sha256': checksum, 'single_executable_verified': True, 'cases': cases,
                  'physical_camera_tested': False}
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(report, ensure_ascii=False, indent=2), flush=True)


if __name__ == '__main__':
    validate(Path(sys.argv[1]), Path(sys.argv[2]))
