import asyncio
import json
import os
import subprocess
import tempfile
import unittest
from dataclasses import asdict
from pathlib import Path
from unittest import mock

from core import (CameraConfig, Recording, check_recording_file, load_settings,
                  normalise_recordings, save_settings)
import worker


class CatalogueTests(unittest.TestCase):
    def test_continuous_recordings_are_bounded_deduplicated_and_request_times_preserved(self):
        start = 1793606400
        raw = [{"video_0": {"startTime": str(start), "endTime": str(start + 780)}},
               {"video_1": {"startTime": start, "endTime": start + 780}}]
        result = normalise_recordings(raw, correction=3600)
        self.assertEqual([(r.start, r.end) for r in result],
                         [(start, start + 300), (start + 300, start + 600), (start + 600, start + 780)])
        self.assertEqual(sum(r.duration for r in result), 780)
        self.assertEqual(int(result[0].local_start.timestamp()), start + 3600)

    def test_open_recording_is_cut_before_camera_write_boundary(self):
        start = 1793606400
        result = normalise_recordings([{"start_time": start, "end_time": start + 600}],
                                      correction=3600, now=start + 3600 + 240)
        self.assertEqual(result[-1].end, start + 175)

    def test_corrupt_catalogue_does_not_look_like_an_empty_card(self):
        for value in (None, {"unrecognised": "format"}, [{"startTime": "broken", "endTime": "1"}]):
            with self.subTest(value=value), self.assertRaises(ValueError):
                normalise_recordings(value)
        self.assertEqual(normalise_recordings([]), [])

    def test_daylight_saving_repeat_hour_does_not_overwrite_a_different_clip(self):
        # 25 October 2026: two distinct 02:15 occurrences in Madrid.
        first = Recording(1792887300, 1792887330)
        second = Recording(first.start + 3600, first.end + 3600)
        self.assertEqual(first.local_start.strftime("%H:%M"), second.local_start.strftime("%H:%M"))
        self.assertNotEqual(first.filename, second.filename)

    def test_public_and_remote_addresses_are_refused(self):
        for host in ("8.8.8.8", "example.com", "127.0.0.1", "0.0.0.0", "192.168.1.1@evil.test"):
            with self.subTest(host=host), self.assertRaises(ValueError):
                CameraConfig(host=host, user="x", camera_password="y").validate(live=True)


class CredentialTests(unittest.TestCase):
    def test_diagnostic_never_exports_raw_exception_with_credentials(self):
        secret = 'example-test-secret-that-must-not-be-exported'
        try:
            raise RuntimeError('some server details ' + secret)
        except RuntimeError as error:
            message, diagnostic = worker.error_report(error, 'download')
        self.assertNotIn(secret, message + json.dumps(diagnostic))
        self.assertEqual(diagnostic['error_type'], 'RuntimeError')
        self.assertTrue(diagnostic['frames'])
        self.assertNotIn('credenciales', message)

    def test_media_auth_failure_is_distinguished_from_catalogue_access(self):
        from pytapo.media_stream.error import HttpStatusCodeException
        message, diagnostic = worker.error_report(HttpStatusCodeException(401), 'download')
        self.assertIn('canal de vídeo', message)
        self.assertEqual(diagnostic['http_status'], 401)

    def test_passwords_are_absent_unless_encrypted_storage_is_selected(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            config = CameraConfig("192.168.1.2", "camera", "camera-secret", "tapo-secret")
            save_settings(config, directory)
            content = (directory / "settings.json").read_text()
            self.assertNotIn("camera-secret", content)
            self.assertNotIn("tapo-secret", content)
            loaded, notice = load_settings(directory)
            self.assertEqual(loaded.host, config.host)
            self.assertEqual(loaded.camera_password, "")
            self.assertEqual(notice, "")

    def test_encryption_failure_does_not_fall_back_to_plaintext(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            old = CameraConfig("192.168.1.2", "camera")
            save_settings(old, directory)
            original = (directory / "settings.json").read_bytes()
            config = CameraConfig("192.168.1.3", "camera", "camera-secret", "tapo-secret", remember=True)
            with mock.patch("core._dpapi", side_effect=OSError("Unavailable")), self.assertRaises(OSError):
                save_settings(config, directory)
            self.assertEqual((directory / "settings.json").read_bytes(), original)


class MediaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix="C230 test & ñ ")
        cls.directory = Path(cls.temporary.name)
        cls.mp4 = cls.directory / "fixture.mp4"
        cls.ts = cls.directory / "fixture.ts"
        cls.audio = cls.directory / "fixture.alaw"
        def command(*args):
            subprocess.run(["ffmpeg", "-v", "error", "-nostdin", "-y", *args], check=True,
                           capture_output=True, timeout=30)
        command("-f", "lavfi", "-i", "testsrc2=size=320x180:rate=15", "-t", "2",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", str(cls.mp4))
        command("-i", str(cls.mp4), "-c:v", "copy", "-f", "mpegts", str(cls.ts))
        command("-f", "lavfi", "-i", "sine=frequency=440:sample_rate=8000", "-t", "2",
                "-c:a", "pcm_alaw", "-f", "alaw", str(cls.audio))

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def test_cached_video_can_be_opened_without_contacting_the_camera(self):
        config = CameraConfig("192.168.1.2", "camera", "secret")
        request = {"action": "download", "config": asdict(config),
                   "recording": Recording(1793606400, 1793606402).to_dict(), "destination": str(self.mp4)}
        with mock.patch("worker.make_camera", side_effect=AssertionError("Network access is forbidden")):
            self.assertEqual(worker.execute(request), {"path": str(self.mp4)})

    def test_truncated_video_is_not_accepted_as_a_successful_download(self):
        bad = self.directory / "truncated.mp4"
        bad.write_bytes(self.mp4.read_bytes()[:900])
        with self.assertRaises(RuntimeError):
            check_recording_file(bad, 2)
        with self.assertRaises(RuntimeError):
            check_recording_file(self.mp4, 60)

    def test_failed_download_never_enters_the_finished_cache(self):
        target = self.directory / "download.mp4"

        class BrokenDownloader:
            def __init__(self, *args, **kwargs):
                self.output = Path(kwargs["outputDirectory"]) / kwargs["fileName"]

            async def download(self):
                self.output.write_bytes(b"partial video data")
                yield {"currentAction": "Downloading", "progress": 1, "total": 2}
                yield {"currentAction": "Giving up"}

        with mock.patch("worker.install_converter", return_value=BrokenDownloader):
            with self.assertRaises(RuntimeError):
                asyncio.run(worker.download_recording(object(), Recording(1793606400, 1793606402), target))
        self.assertFalse(target.exists())
        self.assertFalse(list(self.directory.glob("receiving-*")))

    def test_converter_keeps_video_and_audio_with_spaces_and_shell_characters_in_path(self):
        downloader = worker.install_converter()
        import pytapo.media_stream.downloader as module
        convert = module.Convert()
        convert.writer.write(self.ts.read_bytes())
        convert.audioWriter.write(self.audio.read_bytes())
        destination = self.directory / "grabación & 'test'.mp4"
        asyncio.run(convert.save(str(destination), 2))
        check_recording_file(destination, 2)
        info = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-show_streams", "-of", "json", str(destination)]))
        self.assertEqual({stream["codec_type"] for stream in info["streams"]}, {"audio", "video"})

    def test_new_worker_can_download_without_a_previous_catalogue_in_its_session(self):
        # Exercise the real pytapo Downloader and session-ID implementation.
        # Its synchronous control transport must be called before the media loop.
        from pytapo import Tapo
        from pytapo.asyncHandler import AsyncHandler
        from rtp import PayloadType
        from types import SimpleNamespace
        camera = Tapo.__new__(Tapo)
        camera.userID = False
        camera.playerID = 'test-player'
        camera.asyncHandler = AsyncHandler(None)
        async def control_response():
            return {'user_id': 123}
        camera.executeFunction = lambda *args: camera.asyncHandler.executeAsyncExecutorJob(control_response)
        camera.getAudioConfig = lambda: {}
        camera.logger = mock.Mock()
        fixture = self.ts.read_bytes()

        class MediaSession:
            def set_window_size(self, value):
                pass
            async def __aenter__(self):
                return self
            async def __aexit__(self, *args):
                pass
            async def transceive(self, payload):
                self.request = json.loads(payload)
                yield SimpleNamespace(mimetype='video/mp2t', plaintext=fixture,
                                      audioPayload=b'', audioPayloadType=PayloadType.PCMA)
                yield SimpleNamespace(mimetype='application/json', plaintext=json.dumps({
                    'type': 'notification', 'params': {'event_type': 'stream_status', 'status': 'finished'}
                }).encode())

        camera.getMediaSession = lambda *args: MediaSession()
        target = self.directory / 'fresh-session.mp4'
        config = CameraConfig('192.168.1.2', 'camera', 'test-camera', 'test-tapo')
        try:
            with mock.patch('worker.make_camera', return_value=camera), mock.patch('worker.emit'):
                result = worker.execute({'action': 'download', 'config': asdict(config),
                    'recording': Recording(1793606400, 1793606402).to_dict(), 'destination': str(target)})
            self.assertEqual(result['path'], str(target))
            check_recording_file(target, 2)
        finally:
            if camera.asyncHandler._loop:
                camera.asyncHandler._loop.close()


if __name__ == "__main__":
    unittest.main()
