"""Portable extraction and lifetime tests, without contacting any camera."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import portable_launcher as launcher


class SilentProgress:
    def update(self, *_):
        pass
    def close(self):
        pass


class PortableTests(unittest.TestCase):
    def package(self, directory, *, bad_hash=False, extra=False, missing=False, unsafe=False):
        required = ('runtime/pythonw.exe', 'runtime/python.exe', 'src/app.py',
                    'src/worker.py', 'bin/ffmpeg.exe', 'bin/ffprobe.exe')
        files = {name: ('fixture ' + name).encode() for name in required if not (missing and name == 'src/worker.py')}
        if unsafe:
            files['../../outside.txt'] = b'outside'
        records = [{'file': name, 'size': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
                   for name, data in files.items()]
        if bad_hash:
            records[0]['sha256'] = '0' * 64
        path = directory / 'payload.zip'
        with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as archive:
            for name, data in files.items():
                archive.writestr('CameraApp/' + name, data)
            archive.writestr('CameraApp/ARCHIVOS_SHA256.json', json.dumps(records))
            if extra:
                archive.writestr('CameraApp/unlisted.txt', 'unlisted')
        return path

    def test_extracts_verified_files_in_a_path_with_spaces_and_unicode(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary); target = base / 'Carpeta vacía áé'
            target.mkdir()
            root = launcher.extract_payload(self.package(base), target)
            self.assertEqual((root / 'src/app.py').read_bytes(), b'fixture src/app.py')

    def test_refuses_modified_payload(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            with self.assertRaises(launcher.PayloadError):
                launcher.extract_payload(self.package(base, bad_hash=True), base / 'app')

    def test_refuses_unlisted_payload_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            with self.assertRaises(launcher.PayloadError):
                launcher.extract_payload(self.package(base, extra=True), base / 'app')
            self.assertFalse((base / 'app').exists())

    def test_refuses_incomplete_application(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            with self.assertRaises(launcher.PayloadError):
                launcher.extract_payload(self.package(base, missing=True), base / 'app')

    def test_refuses_archive_path_traversal(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            with self.assertRaises(launcher.PayloadError):
                launcher.extract_payload(self.package(base, unsafe=True), base / 'app')
            self.assertFalse((base / 'outside.txt').exists())

    def test_waits_for_application_before_cleaning_temporary_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary); self.package(base)
            output = base / 'result.json'; prepared = []
            class Child:
                def __init__(self, command, **kwargs):
                    prepared.append(Path(kwargs['cwd']))
                    self.output = Path(command[-1])
                def wait(self):
                    assert (prepared[0] / 'src/app.py').is_file()
                    self.output.write_text('{"status": "passed"}', encoding='utf-8')
                    return 0
            with patch.object(launcher.sys, '_MEIPASS', str(base), create=True), \
                 patch.object(launcher, 'PreparationWindow', SilentProgress), \
                 patch.object(launcher, 'child_environment', return_value={}), \
                 patch.object(launcher.subprocess, 'Popen', Child):
                self.assertEqual(launcher.main(['--self-test-report', str(output)]), 0)
            self.assertFalse(prepared[0].exists())
            self.assertTrue(json.loads(output.read_text())['temporary_application_removed'])

    def test_child_failure_cleans_files_without_leaking_exception_text(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary); self.package(base); output = base / 'result.json'
            with patch.object(launcher.sys, '_MEIPASS', str(base), create=True), \
                 patch.object(launcher, 'PreparationWindow', SilentProgress), \
                 patch.object(launcher, 'child_environment', return_value={}), \
                 patch.object(launcher.subprocess, 'Popen', side_effect=OSError('private-fixture-must-not-leak')):
                self.assertEqual(launcher.main(['--self-test-report', str(output)]), 1)
            self.assertNotIn('private-fixture-must-not-leak', output.read_text())
            self.assertEqual(list(base.glob('app-*')), [])


if __name__ == '__main__':
    unittest.main()
