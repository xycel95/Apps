"""Exercise the actual GUI and worker from inside the one-file executable."""
from __future__ import annotations
import ctypes
from dataclasses import asdict
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import traceback

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'src'))


def exercise():
    if os.environ.get('VISOR_TAPO_SELFTEST_ACTIVE') != '1':
        raise RuntimeError('This check requires the isolated portable test launcher')
    os.environ.setdefault('QT_QPA_PLATFORM', 'windows')
    from PySide6.QtCore import QEventLoop, QTimer, QUrl
    from PySide6.QtWidgets import QApplication
    from app import MainWindow, Operation, STYLE
    from core import VERSION, CameraConfig, Recording, data_directory, load_settings, save_settings
    app = QApplication([])
    app.setStyle('Fusion'); app.setStyleSheet(STYLE)
    config, warning = load_settings()
    settings_restored = bool(config.user == 'portable-fixture')
    fixture = CameraConfig(host='192.168.1.50', user='portable-fixture',
                           camera_password='fixture-camera-only', tapo_password='fixture-tapo-only', remember=True)
    save_settings(fixture)
    restored, warning = load_settings()
    assert not warning and restored.camera_password == fixture.camera_password
    assert restored.tapo_password == fixture.tapo_password
    raw = (data_directory() / 'settings.json').read_text(encoding='utf-8')
    assert fixture.camera_password not in raw and fixture.tapo_password not in raw
    window = MainWindow(); window.show(); app.processEvents()
    assert window.isVisible() and window.windowTitle().endswith(VERSION)
    assert window.host.text() == fixture.host
    with tempfile.TemporaryDirectory(prefix='synthetic-', dir=data_directory()) as temporary:
        clip = Path(temporary) / 'vídeo de prueba.mp4'
        subprocess.run([str(ROOT / 'bin/ffmpeg.exe'), '-nostdin', '-v', 'error', '-f', 'lavfi',
                        '-i', 'testsrc2=size=320x180:rate=15', '-t', '2', '-c:v', 'libx264',
                        '-pix_fmt', 'yuv420p', str(clip)], check=True, timeout=30,
                       stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                       creationflags=subprocess.CREATE_NO_WINDOW)
        frames = []
        window.tabs.setCurrentIndex(1)
        window.record_player.video.videoSink().videoFrameChanged.connect(
            lambda frame: frames.append(frame.size()) if frame.isValid() else None)
        window.record_player.open_source(QUrl.fromLocalFile(str(clip)), clip)
        loop = QEventLoop(); QTimer.singleShot(3500, loop.quit); loop.exec()
        assert frames and window.record_player.player.duration() >= 1900
        # The worker returns this valid local cache entry before any camera login.
        operation = Operation({'action': 'download', 'config': asdict(fixture),
            'recording': asdict(Recording(1700000000, 1700000002)), 'destination': str(clip)})
        results = []; errors = []
        worker_loop = QEventLoop()
        operation.result.connect(results.append); operation.error.connect(errors.append)
        operation.finished.connect(worker_loop.quit)
        operation.start(); QTimer.singleShot(20000, worker_loop.quit); worker_loop.exec()
        operation.thread.join(timeout=3)
        if not results or errors:
            operation.cancel()
            raise AssertionError('Isolated local worker did not return a valid cached recording')
        assert Path(results[0]['path']) == clip
        window.record_player.stop()
    window.close(); app.processEvents()
    return {'status': 'passed', 'version': VERSION, 'native_gui_opened': True,
            'synthetic_video_frames': len(frames), 'isolated_worker_verified': True,
            'settings_restored_after_move': settings_restored, 'dpapi_roundtrip_verified': True,
            'no_plaintext_credentials': True, 'camera_contacted': False}


if __name__ == '__main__':
    report = Path(sys.argv[1])
    try:
        result = exercise()
    except BaseException as error:
        result = {'status': 'failed', 'child_exception_type': type(error).__name__,
                  'frames': [f'{Path(frame.filename).name}:{frame.lineno}' for frame in traceback.extract_tb(error.__traceback__)]}
    report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    raise SystemExit(0 if result['status'] == 'passed' else 1)
