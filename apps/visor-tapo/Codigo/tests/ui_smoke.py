"""UI playback smoke test, with a synthetic local clip rather than a camera."""
import os
os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
import subprocess
import tempfile
from pathlib import Path
from PySide6.QtCore import QEventLoop, QTimer, QUrl
from PySide6.QtWidgets import QApplication
from app import MainWindow, STYLE


def main():
    application = QApplication([])
    application.setStyle("Fusion")
    application.setStyleSheet(STYLE)
    window = MainWindow()
    window.show()
    application.processEvents()
    with tempfile.TemporaryDirectory() as temp:
        clip = Path(temp) / "demo.mp4"
        subprocess.run(["ffmpeg", "-nostdin", "-v", "error", "-f", "lavfi", "-i",
                        "testsrc2=size=320x180:rate=15", "-t", "2", "-c:v", "libx264",
                        "-pix_fmt", "yuv420p", str(clip)], check=True, timeout=30)
        window.tabs.setCurrentIndex(1)
        received_frames = []
        window.record_player.video.videoSink().videoFrameChanged.connect(
            lambda frame: received_frames.append(frame.size()) if frame.isValid() else None)
        window.record_player.open_source(QUrl.fromLocalFile(str(clip)), clip)
        loop = QEventLoop()
        QTimer.singleShot(3000, loop.quit)
        loop.exec()
        assert received_frames, "No video frame reached the visible player"
        assert window.record_player.player.duration() >= 1900
        assert window.record_player.stack.currentWidget() is window.record_player.video
        print(f"UI smoke passed: {len(received_frames)} video frames, two tabs, local MP4 playback")
    window.close()
    application.processEvents()


if __name__ == "__main__":
    main()
