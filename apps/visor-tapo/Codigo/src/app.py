from __future__ import annotations
import json
import os
import shutil
import subprocess
import sys
import threading
import time
from dataclasses import asdict
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
os.environ.setdefault("QT_MEDIA_BACKEND", "ffmpeg")
os.environ.setdefault("QT_FFMPEG_PROTOCOL_WHITELIST", "file,rtsp,rtp,tcp,udp")
os.environ.setdefault("QT_FFMPEG_RTSP_TRANSPORT", "tcp")
os.environ["PATH"] = str(ROOT / "bin") + os.pathsep + os.environ.get("PATH", "")

from PySide6.QtCore import (QDate, QEvent, QObject, QSignalBlocker, QSize, Qt, QTimer,
                            QUrl, Signal, qInstallMessageHandler)
from PySide6.QtGui import QColor, QDesktopServices, QIcon, QImage, QPainter, QPen, QPixmap
from PySide6.QtWidgets import (QAbstractItemView, QApplication, QCheckBox, QComboBox,
    QDateEdit, QDialog, QFileDialog, QFrame, QGridLayout, QHBoxLayout, QHeaderView, QLabel, QInputDialog,
    QLineEdit, QMainWindow, QMessageBox, QProgressBar, QPushButton, QScrollArea,
    QSizePolicy, QSlider, QSplitter, QStackedWidget, QTabWidget, QTableWidget,
    QTableWidgetItem, QVBoxLayout, QWidget)
from PySide6.QtMultimedia import QAudioOutput, QMediaPlayer, QPlaybackOptions
from PySide6.QtMultimediaWidgets import QVideoWidget

from core import (APP_NAME, VERSION, CameraConfig, Recording, data_directory,
                  format_duration, load_settings, save_settings)

STYLE = """
QWidget { background: #10171f; color: #e9eff4; font-family: 'Segoe UI'; font-size: 13px; }
QMainWindow { background: #10171f; }
QFrame#sidebar { background: #16212c; border-right: 1px solid #283644; }
QFrame#sidebar QLabel, QFrame#sidebar QCheckBox, QFrame#sidebar QScrollArea, QWidget#sidebarContent { background: transparent; }
QLabel#brand { font-size: 21px; font-weight: 700; letter-spacing: 0.3px; }
QLabel#title { font-size: 25px; font-weight: 650; }
QLabel#subtitle, QLabel#muted { color: #91a3b3; }
QLabel#section { font-weight: 650; font-size: 14px; }
QLabel#badge { color: #62dfbf; background: #183b37; border-radius: 8px; padding: 5px 9px; font-size: 11px; }
QLineEdit, QComboBox, QDateEdit { background: #0e1822; border: 1px solid #344656; border-radius: 7px; padding: 7px 10px; min-height: 22px; selection-background-color: #256e61; }
QLineEdit:focus, QDateEdit:focus, QComboBox:focus { border: 1px solid #53d6b7; }
QLineEdit:disabled { color: #7c8c99; }
QComboBox QAbstractItemView { background: #1b2a38; selection-background-color: #28695d; }
QComboBox::drop-down, QDateEdit::drop-down { border: 0px; width: 22px; }
QPushButton { background: #233340; border: 1px solid #344858; padding: 8px 13px; border-radius: 7px; min-height: 20px; }
QPushButton:hover { background: #2b4252; border-color: #587587; }
QPushButton:pressed { background: #172833; }
QPushButton:disabled { color: #627585; border-color: #273744; background: #18252f; }
QPushButton#primary { color: #08271f; background: #64dfbd; border: 1px solid #64dfbd; font-weight: 650; }
QPushButton#primary:hover { background: #83e8cb; }
QPushButton#primary:disabled { background: #315b50; color: #87a29b; border-color: #315b50; }
QPushButton#flat { background: transparent; }
QTabWidget::pane { border: 0; }
QTabBar::tab { color: #8ea2b3; background: transparent; padding: 12px 21px; border-bottom: 2px solid #293744; }
QTabBar::tab:selected { color: #67e1c0; border-bottom: 2px solid #67e1c0; }
QFrame#card { background: #16222d; border: 1px solid #2b3c4a; border-radius: 10px; }
QFrame#card QLabel { background: transparent; }
QWidget#placeholder { background: #0a1118; border: 1px solid #22313d; border-radius: 9px; }
QLabel#placeholderTitle { background: transparent; color: #c4d3df; font-size: 20px; font-weight: 600; }
QLabel#placeholderHint { background: transparent; color: #758b9d; }
QTableWidget { background: #131f2a; alternate-background-color: #192733; border: 1px solid #2c4050; border-radius: 7px; gridline-color: #233644; selection-background-color: #245b51; selection-color: #e6fff6; }
QTableWidget::item { padding: 9px; border-bottom: 1px solid #243542; }
QHeaderView::section { background: #1d2e3c; color: #a8bdcb; border: 0; padding: 9px; font-weight: 600; }
QScrollArea { border: 0; }
QScrollBar:vertical { background: #14202a; width: 9px; }
QScrollBar::handle:vertical { background: #3a4f60; border-radius: 4px; min-height: 25px; }
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height: 0; }
QSlider::groove:horizontal { height: 4px; background: #304353; border-radius: 2px; }
QSlider::sub-page:horizontal { background: #62dcbc; border-radius: 2px; }
QSlider::handle:horizontal { background: #b4f6e3; width: 12px; margin: -4px 0; border-radius: 6px; }
QProgressBar { background: #1b2c39; border: 0; border-radius: 3px; max-height: 6px; }
QProgressBar::chunk { background: #62dfbd; border-radius: 3px; }
QCheckBox { spacing: 7px; color: #a9bccb; }
QCheckBox::indicator { width: 15px; height: 15px; background: #0e1822; border: 1px solid #486171; border-radius: 3px; }
QCheckBox::indicator:checked { background: #64dfbd; border-color: #64dfbd; }
QToolTip { background: #253d4e; color: #edf7ff; border: 1px solid #48677c; }
QSplitter::handle { background: #10171f; width: 10px; }
QStatusBar { color: #91a5b6; border-top: 1px solid #253644; }
"""


def label(text, name=None, wrap=False):
    widget = QLabel(text)
    widget.setTextFormat(Qt.TextFormat.PlainText)
    if name:
        widget.setObjectName(name)
    widget.setWordWrap(wrap)
    return widget


def button(text, callback, primary=False):
    widget = QPushButton(text)
    if primary:
        widget.setObjectName("primary")
    widget.clicked.connect(callback)
    widget.setCursor(Qt.CursorShape.PointingHandCursor)
    return widget


def logo():
    pixmap = QPixmap(80, 80)
    pixmap.fill(Qt.GlobalColor.transparent)
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    painter.setBrush(QColor("#64dfbd"))
    painter.setPen(Qt.PenStyle.NoPen)
    painter.drawRoundedRect(2, 2, 76, 76, 21, 21)
    painter.setPen(QPen(QColor("#0a3028"), 5))
    painter.setBrush(Qt.BrushStyle.NoBrush)
    painter.drawEllipse(19, 19, 42, 42)
    painter.setBrush(QColor("#0a3028"))
    painter.drawEllipse(32, 32, 16, 16)
    painter.end()
    return QIcon(pixmap)


class Operation(QObject):
    progress = Signal(str, int)
    result = Signal(dict)
    error = Signal(str)
    finished = Signal()

    def __init__(self, request, parent=None):
        super().__init__(parent)
        self.request = request
        self.cancelled = threading.Event()
        self.process = None
        self.thread = None

    def start(self):
        self.thread = threading.Thread(target=self.run, daemon=True)
        self.thread.start()

    def cancel(self):
        self.cancelled.set()
        process = self.process
        if process and process.poll() is None:
            try:
                process.kill()
            except OSError:
                pass

    def run(self):
        timer = None
        timed_out = threading.Event()
        try:
            python = ROOT / "runtime" / "python.exe" if os.name == "nt" else Path(sys.executable)
            self.process = subprocess.Popen([str(python), "-u", str(ROOT / "src" / "worker.py")],
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                text=True, encoding="utf-8", env={**os.environ, "PYTHONIOENCODING": "utf-8"},
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            if self.cancelled.is_set():
                self.process.kill()
                return
            timeout = 600 if self.request["action"] == "download" else 55

            def expire():
                timed_out.set()
                if self.process.poll() is None:
                    try:
                        self.process.kill()
                    except OSError:
                        pass

            timer = threading.Timer(timeout, expire)
            timer.daemon = True
            timer.start()
            self.process.stdin.write(json.dumps(self.request, ensure_ascii=False) + "\n")
            self.process.stdin.close()
            complete = False
            for line in self.process.stdout:
                if self.cancelled.is_set():
                    break
                try:
                    response = json.loads(line)
                except (ValueError, TypeError):
                    continue
                kind = response.get("type")
                if kind == "progress":
                    self.progress.emit(str(response.get("message", "Preparando…")), int(response.get("percent", -1)))
                elif kind == "result":
                    complete = True
                    self.result.emit(response["result"])
                elif kind == "error":
                    complete = True
                    diagnostic = response.get('diagnostic')
                    if isinstance(diagnostic, dict):
                        try:
                            (data_directory() / 'last-diagnostic.json').write_text(
                                json.dumps(diagnostic, ensure_ascii=False, indent=2), encoding='utf-8')
                        except OSError:
                            pass
                    self.error.emit(str(response.get("message", "No se ha completado la operación.")))
            self.process.wait(timeout=3)
            if not self.cancelled.is_set() and not complete:
                self.error.emit("La cámara tarda demasiado en responder. Vuelve a intentarlo." if timed_out.is_set()
                                else "No se ha podido iniciar la conexión. Cierra Visor Tapo y vuelve a abrirlo.")
        except Exception:
            if not self.cancelled.is_set():
                self.error.emit("No se ha podido iniciar la operación. Cierra Visor Tapo y vuelve a abrirlo.")
        finally:
            if timer:
                timer.cancel()
            if self.process and self.process.poll() is None:
                self.process.kill()
                self.process.wait(timeout=3)
            self.finished.emit()


class VideoPanel(QWidget):
    connected = Signal()
    failed = Signal(str)
    ended = Signal()
    request_live = Signal()

    def __init__(self, live=False, parent=None):
        super().__init__(parent)
        self.live = live
        self.media_path = None
        self.setMinimumWidth(400)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(13)
        self.stack = QStackedWidget()
        self.stack.setMinimumHeight(250)
        self.stack.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)
        self.placeholder = QWidget()
        self.placeholder.setObjectName("placeholder")
        blank = QVBoxLayout(self.placeholder)
        blank.setContentsMargins(24, 24, 24, 24)
        blank.addStretch()
        mark = label("◉", "placeholderTitle")
        mark.setStyleSheet("font-size: 52px; color: #426e6a; background: transparent;")
        mark.setAlignment(Qt.AlignmentFlag.AlignCenter)
        blank.addWidget(mark)
        self.placeholder_title = label("Tu cámara, en el ordenador" if live else "Elige una grabación", "placeholderTitle", True)
        self.placeholder_hint = label("Introduce los datos de conexión y pulsa «Abrir directo»." if live else
                                      "Consulta una fecha y abre un vídeo de la microSD.", "placeholderHint", True)
        for item in (self.placeholder_title, self.placeholder_hint):
            item.setAlignment(Qt.AlignmentFlag.AlignCenter)
            blank.addWidget(item)
        blank.addStretch()
        self.video = QVideoWidget()
        self.video.setAspectRatioMode(Qt.AspectRatioMode.KeepAspectRatio)
        self.video.installEventFilter(self)
        self.stack.addWidget(self.placeholder)
        self.stack.addWidget(self.video)
        layout.addWidget(self.stack, 1)
        self.player = QMediaPlayer(self)
        self.audio = QAudioOutput(self)
        self.audio.setVolume(0.6)
        self.player.setAudioOutput(self.audio)
        self.player.setVideoOutput(self.video)
        if live:
            options = QPlaybackOptions()
            options.setPlaybackIntent(QPlaybackOptions.PlaybackIntent.LowLatencyStreaming)
            self.player.setPlaybackOptions(options)
        self.player.errorOccurred.connect(self.on_error)
        self.player.mediaStatusChanged.connect(self.on_status)
        self.player.playbackStateChanged.connect(self.update_play_button)
        self.video.videoSink().videoFrameChanged.connect(self.on_frame)
        self.timeout = QTimer(self)
        self.timeout.setSingleShot(True)
        self.timeout.timeout.connect(self.connection_timeout)
        self.time_row = QWidget()
        timeline = QHBoxLayout(self.time_row)
        timeline.setContentsMargins(0, 0, 0, 0)
        self.position_label = label("00:00", "muted")
        self.seek = QSlider(Qt.Orientation.Horizontal)
        self.seek.setRange(0, 0)
        self.seek.sliderReleased.connect(lambda: self.player.setPosition(self.seek.value()))
        self.duration_label = label("00:00", "muted")
        timeline.addWidget(self.position_label)
        timeline.addWidget(self.seek, 1)
        timeline.addWidget(self.duration_label)
        self.player.positionChanged.connect(self.on_position)
        self.player.durationChanged.connect(self.on_duration)
        if not live:
            layout.addWidget(self.time_row)
        controls = QHBoxLayout()
        self.play_button = button("Reproducir", self.toggle_play)
        controls.addWidget(self.play_button)
        controls.addWidget(button("Detener", self.stop))
        controls.addStretch()
        controls.addWidget(label("Volumen", "muted"))
        volume = QSlider(Qt.Orientation.Horizontal)
        volume.setRange(0, 100)
        volume.setValue(60)
        volume.setFixedWidth(85)
        volume.valueChanged.connect(lambda value: self.audio.setVolume(value / 100))
        controls.addWidget(volume)
        controls.addWidget(button("Captura", self.capture))
        controls.addWidget(button("Pantalla completa", self.fullscreen))
        layout.addLayout(controls)

    def eventFilter(self, watched, event):
        if watched is self.video:
            if event.type() == QEvent.Type.MouseButtonDblClick:
                self.fullscreen()
                return True
            if event.type() == QEvent.Type.KeyPress and event.key() == Qt.Key.Key_Escape:
                self.video.setFullScreen(False)
                return True
        return super().eventFilter(watched, event)

    def set_placeholder(self, title, hint=""):
        self.placeholder_title.setText(title)
        self.placeholder_hint.setText(hint)
        self.stack.setCurrentWidget(self.placeholder)

    def open_source(self, source: QUrl, local_path=None):
        self.stop(reset=False)
        self.media_path = local_path
        self.set_placeholder("Conectando con la cámara…" if self.live else "Abriendo grabación…")
        self.player.setSource(source)
        self.timeout.start(20000)
        self.player.play()

    def stop(self, checked=False, reset=True):
        self.timeout.stop()
        self.video.setFullScreen(False)
        self.player.stop()
        self.player.setSource(QUrl())
        if reset:
            self.set_placeholder("Directo detenido" if self.live else "Reproducción detenida",
                                 "Pulsa «Abrir directo» para conectar." if self.live else "Selecciona otro vídeo para continuar.")

    def on_frame(self, frame):
        if frame.isValid():
            first = self.stack.currentWidget() is not self.video
            self.timeout.stop()
            self.stack.setCurrentWidget(self.video)
            if first:
                self.connected.emit()

    def connection_timeout(self):
        self.stop(reset=False)
        self.set_placeholder("No se ha recibido imagen", "Comprueba la IP y las credenciales; cierra otras vistas de la cámara si está ocupada.")
        self.failed.emit("No se ha recibido imagen de la cámara." if self.live else "No se ha podido abrir el vídeo descargado.")

    def on_error(self, error, text):
        if error == QMediaPlayer.Error.NoError:
            return
        self.timeout.stop()
        # Do not display the backend's text: it can contain an authenticated URL.
        message = ("No se ha podido abrir el directo. Revisa la IP y «Cuenta de cámara» en Tapo."
                   if self.live else "No se ha podido reproducir el vídeo. Prueba a descargarlo de nuevo.")
        self.player.stop()
        self.set_placeholder("No se ha podido reproducir", message)
        self.failed.emit(message)

    def on_status(self, status):
        if status == QMediaPlayer.MediaStatus.EndOfMedia:
            self.ended.emit()

    def update_play_button(self, state):
        self.play_button.setText("Pausar" if state == QMediaPlayer.PlaybackState.PlayingState else "Reproducir")

    def toggle_play(self):
        if self.player.source().isEmpty():
            if self.live:
                self.request_live.emit()
            return
        if self.player.playbackState() == QMediaPlayer.PlaybackState.PlayingState:
            self.player.pause()
        else:
            self.player.play()

    def on_position(self, position):
        if not self.seek.isSliderDown():
            self.seek.setValue(position)
        self.position_label.setText(format_duration(position / 1000))

    def on_duration(self, duration):
        self.seek.setRange(0, duration)
        self.duration_label.setText(format_duration(duration / 1000))

    def fullscreen(self):
        if self.stack.currentWidget() is self.video:
            self.video.setFullScreen(not self.video.isFullScreen())

    def capture(self):
        image = self.video.videoSink().videoFrame().toImage()
        if image.isNull():
            QMessageBox.information(self, "Captura", "Primero abre el directo o una grabación.")
            return
        default = Path.home() / "Pictures" / f"Tapo_{datetime.now():%Y-%m-%d_%H-%M-%S}.png"
        path, _ = QFileDialog.getSaveFileName(self, "Guardar captura", str(default), "Imagen PNG (*.png)")
        if path and not image.save(path, "PNG"):
            QMessageBox.warning(self, "Captura", "No se ha podido guardar la imagen en esa carpeta.")


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(f"{APP_NAME} · {VERSION}")
        self.setWindowIcon(logo())
        self.resize(1320, 870)
        self.setMinimumSize(1090, 750)
        self.operation = None
        self.recordings = []
        self.catalogue_profile = None
        self.playing_index = -1
        self.progress_box = None
        config, notice = load_settings()
        container = QWidget()
        self.setCentralWidget(container)
        root = QHBoxLayout(container)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(0)
        self.build_sidebar(root, config)
        content = QVBoxLayout()
        content.setContentsMargins(28, 25, 28, 20)
        content.setSpacing(17)
        root.addLayout(content, 1)
        header = QHBoxLayout()
        titles = QVBoxLayout()
        titles.addWidget(label("Tu cámara, más cerca", "title"))
        titles.addWidget(label("Directo y grabaciones de tu cámara Tapo, desde casa.", "subtitle"))
        header.addLayout(titles, 1)
        self.state = label("Sin conectar", "badge")
        header.addWidget(self.state)
        content.addLayout(header)
        self.tabs = QTabWidget()
        self.tabs.addTab(self.build_live_page(), "Directo")
        self.tabs.addTab(self.build_recordings_page(), "Grabaciones")
        self.tabs.currentChanged.connect(self.tab_changed)
        content.addWidget(self.tabs, 1)
        self.operation_bar = QWidget()
        progress_layout = QVBoxLayout(self.operation_bar)
        progress_layout.setContentsMargins(0, 0, 0, 0)
        progress_row = QHBoxLayout()
        self.operation_label = label("", "muted", True)
        progress_row.addWidget(self.operation_label, 1)
        self.cancel_button = button("Cancelar", self.cancel_operation)
        progress_row.addWidget(self.cancel_button)
        progress_layout.addLayout(progress_row)
        self.progress = QProgressBar()
        self.progress.setTextVisible(False)
        progress_layout.addWidget(self.progress)
        self.operation_bar.hide()
        content.addWidget(self.operation_bar)
        self.statusBar().showMessage(notice or "Introduce la IP y las credenciales de la cámara para empezar.")

    def build_sidebar(self, root, config):
        sidebar = QFrame()
        sidebar.setObjectName("sidebar")
        sidebar.setFixedWidth(280)
        sidebar_layout = QVBoxLayout(sidebar)
        sidebar_layout.setContentsMargins(19, 23, 19, 18)
        sidebar_layout.setSpacing(12)
        brand = QHBoxLayout()
        icon = QLabel()
        icon.setPixmap(logo().pixmap(39, 39))
        brand.addWidget(icon)
        brand.addWidget(label(APP_NAME, "brand"), 1)
        sidebar_layout.addLayout(brand)
        sidebar_layout.addWidget(label("Tapo · Conexión local", "muted"))
        sidebar_layout.addSpacing(10)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        scroll_content = QWidget()
        scroll_content.setObjectName("sidebarContent")
        form = QVBoxLayout(scroll_content)
        form.setContentsMargins(0, 0, 8, 0)
        form.setSpacing(8)
        form.addWidget(label("CONEXIÓN", "section"))
        self.host = self.field(form, "IP de la cámara", config.host, "192.168.1.50")
        self.search_button = button('Buscar cámaras en casa', self.search_cameras, True)
        form.addWidget(self.search_button)
        form.addWidget(button('¿Dónde saco los datos?', self.connection_help))
        form.addWidget(button('Modelos y compatibilidad', self.show_compatibility))
        form.addSpacing(7)
        form.addWidget(label("Para el directo", "section"))
        self.user = self.field(form, "Usuario de «Cuenta de cámara»", config.user)
        self.camera_password = self.field(form, "Contraseña de cámara", config.camera_password, password=True)
        form.addSpacing(7)
        form.addWidget(label("Para la microSD", "section"))
        self.tapo_password = self.field(form, "Contraseña de tu cuenta Tapo", config.tapo_password, password=True)
        form.addWidget(label("Se usa para acceder a la cámara por tu red local.", "muted", True))
        self.show_passwords = QCheckBox("Mostrar contraseñas")
        self.show_passwords.toggled.connect(self.toggle_passwords)
        form.addWidget(self.show_passwords)
        self.remember = QCheckBox("Recordar claves en este PC")
        self.remember.setChecked(config.remember)
        self.remember.setEnabled(os.name == "nt")
        self.remember.setToolTip("Las claves se cifran para tu usuario de Windows. Si no lo marcas, solo se guardan la IP, el usuario y la calidad.")
        form.addWidget(self.remember)
        form.addSpacing(7)
        form.addWidget(label("Calidad del directo", "muted"))
        self.quality = QComboBox()
        self.quality.addItems(["Alta", "Ligera"])
        self.quality.setCurrentText(config.quality)
        form.addWidget(self.quality)
        self.connect_button = button("Abrir directo", self.connect_live, True)
        self.probe_button = button("Probar acceso a microSD", self.probe)
        self.save_button = button("Guardar ajustes", self.save_config)
        form.addWidget(self.connect_button)
        form.addWidget(self.probe_button)
        form.addWidget(self.save_button)
        form.addSpacing(10)
        help_text = label("En Tapo: Ajustes → Información del dispositivo para ver la IP.\n\nCrea el usuario del directo en Ajustes → Configuración avanzada → Cuenta de cámara.", "muted", True)
        form.addWidget(help_text)
        form.addStretch()
        scroll.setWidget(scroll_content)
        sidebar_layout.addWidget(scroll, 1)
        sidebar_layout.addWidget(label("Solo red de casa · Horas de Madrid", "muted"))
        root.addWidget(sidebar)
        self.config_fields = [self.host, self.search_button, self.user, self.camera_password, self.tapo_password,
                              self.remember, self.quality, self.connect_button, self.probe_button, self.save_button]

    @staticmethod
    def field(layout, title, value, placeholder="", password=False):
        layout.addWidget(label(title, "muted", True))
        field = QLineEdit(value)
        field.setPlaceholderText(placeholder)
        if password:
            field.setEchoMode(QLineEdit.EchoMode.Password)
        layout.addWidget(field)
        return field

    def build_live_page(self):
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(0, 19, 0, 0)
        layout.setSpacing(14)
        self.live = VideoPanel(live=True)
        self.live.request_live.connect(self.connect_live)
        self.live.connected.connect(lambda: self.set_status("En directo", "Conexión con la cámara establecida."))
        self.live.failed.connect(lambda message: self.set_status("Sin imagen", message))
        layout.addWidget(self.live, 1)
        movement = QFrame()
        movement.setObjectName("card")
        row = QHBoxLayout(movement)
        row.setContentsMargins(17, 13, 17, 13)
        text = QVBoxLayout()
        text.addWidget(label("Mueve la cámara", "section"))
        text.addWidget(label("Giros cortos. Usa las credenciales de la microSD.", "muted"))
        row.addLayout(text, 1)
        self.move_buttons = []
        for title, x, y in [("←", -10, 0), ("↑", 0, 10), ("↓", 0, -10), ("→", 10, 0)]:
            control = button(title, lambda checked=False, x=x, y=y: self.move_camera(x, y))
            control.setFixedSize(43, 38)
            self.move_buttons.append(control)
            row.addWidget(control)
        layout.addWidget(movement)
        return page

    def build_recordings_page(self):
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(0, 19, 0, 0)
        layout.setSpacing(12)
        filters = QHBoxLayout()
        filters.addWidget(label("Fecha", "section"))
        self.date = QDateEdit(QDate.currentDate())
        self.date.setCalendarPopup(True)
        self.date.setDisplayFormat("dd/MM/yyyy")
        self.date.setMaximumDate(QDate.currentDate())
        filters.addWidget(self.date)
        self.today_button = button("Hoy", lambda: self.date.setDate(QDate.currentDate()))
        filters.addWidget(self.today_button)
        self.list_button = button("Consultar microSD", self.list_recordings, True)
        filters.addWidget(self.list_button)
        filters.addStretch()
        filters.addWidget(button('Guardar diagnóstico', self.export_diagnostic))
        filters.addWidget(button("Copias en este PC", self.open_cache_folder))
        layout.addLayout(filters)
        self.catalogue_label = label("Consulta una fecha para ver qué hay guardado.", "muted", True)
        layout.addWidget(self.catalogue_label)
        splitter = QSplitter(Qt.Orientation.Horizontal)
        browser = QWidget()
        browser_layout = QVBoxLayout(browser)
        browser_layout.setContentsMargins(0, 0, 0, 0)
        browser_layout.setSpacing(10)
        self.table = QTableWidget(0, 3)
        self.table.setHorizontalHeaderLabels(["Inicio", "Fin", "Duración"])
        self.table.verticalHeader().hide()
        self.table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.table.setSelectionBehavior(QAbstractItemView.SelectionBehavior.SelectRows)
        self.table.setSelectionMode(QAbstractItemView.SelectionMode.SingleSelection)
        self.table.setEditTriggers(QAbstractItemView.EditTrigger.NoEditTriggers)
        self.table.setAlternatingRowColors(True)
        self.table.setShowGrid(False)
        self.table.cellDoubleClicked.connect(lambda *_: self.open_recording())
        browser_layout.addWidget(self.table, 1)
        self.open_button = button("Ver selección", self.open_recording, True)
        self.export_button = button("Guardar copia", self.export_recording)
        self.open_button.setEnabled(False)
        self.export_button.setEnabled(False)
        browser_layout.addWidget(self.open_button)
        browser_layout.addWidget(self.export_button)
        self.continue_next = QCheckBox("Continuar con el siguiente")
        self.continue_next.setChecked(True)
        browser_layout.addWidget(self.continue_next)
        browser_layout.addWidget(label("Los vídeos largos se muestran en tramos de hasta 5 minutos. El último minuto puede tardar en aparecer.", "muted", True))
        splitter.addWidget(browser)
        self.record_player = VideoPanel()
        self.record_player.ended.connect(self.next_recording)
        self.record_player.connected.connect(lambda: self.set_status("Reproduciendo", "Reproduciendo una copia local de la grabación."))
        self.record_player.failed.connect(lambda message: self.set_status("Sin reproducción", message))
        splitter.addWidget(self.record_player)
        splitter.setStretchFactor(0, 0)
        splitter.setStretchFactor(1, 1)
        splitter.setSizes([300, 630])
        layout.addWidget(splitter, 1)
        return page

    def toggle_passwords(self, visible):
        mode = QLineEdit.EchoMode.Normal if visible else QLineEdit.EchoMode.Password
        self.camera_password.setEchoMode(mode)
        self.tapo_password.setEchoMode(mode)

    def search_cameras(self, checked=False):
        if self.operation:
            return
        def received(value):
            devices = value.get('devices', [])
            if not devices:
                QMessageBox.information(self, 'Buscar cámaras',
                    'No se ha detectado ninguna cámara. Esto no significa que esté desconectada.\n\n'
                    'Comprueba que el PC y la cámara estén en la misma red, evita la red de invitados y revisa el permiso de red privada de Windows.\n\n'
                    'También puedes consultar la IP en Tapo → tu cámara → Ajustes → Información del dispositivo e introducirla aquí.')
                self.set_status('Búsqueda terminada', 'No se han recibido respuestas de cámaras ONVIF.')
                return
            choices = [f"{item['name']} · {item['host']}" for item in devices]
            selected, ok = QInputDialog.getItem(self, 'Cámaras encontradas',
                'Selecciona tu cámara (pueden aparecer otras cámaras ONVIF):', choices, 0, False)
            if ok:
                self.host.setText(devices[choices.index(selected)]['host'])
                self.set_status('Cámara seleccionada', 'IP rellenada. Completa las credenciales para conectar.')
        self.start_operation('discover', self.config(), received)
        self.operation_label.setText('Buscando cámaras en la red de casa…')

    def connection_help(self, checked=False):
        QMessageBox.information(self, 'Cómo conectar tu cámara Tapo',
            '1. ENCUENTRA LA CÁMARA\nPulsa «Buscar cámaras en casa» y selecciona la tuya. No necesitas saber la IP ni introducir contraseñas para buscar. '
            'Si no aparece: Tapo → tu cámara → Ajustes → Información del dispositivo → Dirección IP.\n\n'
            '2. USUARIO Y CONTRASEÑA DEL DIRECTO\nEn la app Tapo del móvil: tu cámara → Ajustes → Configuración avanzada → Cuenta de cámara. '
            'Ahí creas o cambias ese usuario y su contraseña. No son tu correo ni la clave de tu cuenta Tapo.\n\n'
            '3. CONTRASEÑA PARA LA MICROSD\nEs la contraseña con la que inicias sesión en tu cuenta Tapo/TP-Link ID. '
            'Si no la recuerdas, usa «¿Olvidaste la contraseña?» en la pantalla de inicio de sesión de Tapo. '
            'Después abre Tapo en el móvil y escribe la nueva clave aquí.\n\n'
            'La aplicación no puede mostrar ni recuperar automáticamente tus contraseñas. '
            'Las introduces aquí y, si marcas «Recordar claves en este PC», Windows las guarda protegidas.\n\n'
            'Guía oficial: https://www.tp-link.com/es/support/faq/2680/')

    def show_compatibility(self, checked=False):
        try:
            catalogue = json.loads((ROOT / 'src' / 'compatibility.json').read_text(encoding='utf-8'))
        except (OSError, ValueError):
            QMessageBox.information(self, 'Compatibilidad', 'Consulta COMPATIBILIDAD.md, incluido junto al ejecutable.')
            return
        dialog = QDialog(self)
        dialog.setWindowTitle('Visor Tapo · Modelos y compatibilidad')
        dialog.resize(1120, 700)
        layout = QVBoxLayout(dialog)
        layout.addWidget(label('Compatibilidad por función', 'title'))
        layout.addWidget(label('Compatibilidad técnica según las fichas de TP-Link. «Previsto con pytapo» indica acceso a microSD sujeto al hardware y firmware; no es una garantía del fabricante.', 'muted', True))
        search = QLineEdit()
        search.setPlaceholderText('Buscar modelo (por ejemplo, C230)')
        search.setClearButtonEnabled(True)
        search.setObjectName('compatibilitySearch')
        layout.addWidget(search)
        table = QTableWidget(len(catalogue['models']), 4)
        table.setObjectName('compatibilityTable')
        table.setHorizontalHeaderLabels(['Modelo', 'Directo', 'Grabaciones microSD', 'Condiciones'])
        for column in range(3):
            table.horizontalHeader().setSectionResizeMode(column, QHeaderView.ResizeMode.ResizeToContents)
        table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeMode.Stretch)
        table.verticalHeader().hide()
        table.setEditTriggers(QAbstractItemView.EditTrigger.NoEditTriggers)
        table.setAlternatingRowColors(True)
        table.setSelectionBehavior(QAbstractItemView.SelectionBehavior.SelectRows)
        models = catalogue['models']
        for row, item in enumerate(models):
            for column, text in enumerate([item['model'], item['live'], item['recordings'], item['note']]):
                cell = QTableWidgetItem(text)
                cell.setToolTip(item['note'] + '\nFicha: ' + item['source'])
                table.setItem(row, column, cell)
        def filter_models(text):
            query = text.casefold().replace('tapo', '').strip()
            for row, item in enumerate(models):
                table.setRowHidden(row, query not in item['model'].casefold())
        search.textChanged.connect(filter_models)
        layout.addWidget(table, 1)
        layout.addWidget(label('Requisitos: misma red local, RTSP para el directo y credenciales propias. «No documentado» indica que la información técnica es insuficiente. Consulta COMPATIBILIDAD.md para las fichas y límites de cada modelo.', 'muted', True))
        layout.addWidget(label(f"{len(models)} modelos, kits y variantes · Fichas y soporte oficial de TP-Link · Revisión: 02/11/2026.", 'muted'))
        layout.addWidget(button('Cerrar', dialog.accept))
        dialog.exec()

    def config(self):
        return CameraConfig(self.host.text().strip(), self.user.text().strip(), self.camera_password.text(),
                            self.tapo_password.text(), self.quality.currentText(), self.remember.isChecked())

    def save_config(self, checked=False):
        try:
            save_settings(self.config())
            self.statusBar().showMessage("Ajustes guardados. Las contraseñas solo se guardan si has marcado la protección de Windows.")
        except Exception:
            QMessageBox.warning(self, "Ajustes", "No se han podido guardar los ajustes. Las contraseñas no se han guardado sin cifrar.")

    def validated_config(self, live=False):
        config = self.config()
        try:
            config.validate(live)
        except ValueError as error:
            QMessageBox.information(self, "Datos de conexión", str(error))
            return None
        self.save_config()
        return config

    def set_status(self, state, message):
        self.state.setText(state)
        self.statusBar().showMessage(message)

    def connect_live(self, checked=False):
        if self.operation:
            return
        config = self.validated_config(live=True)
        if config is None:
            return
        self.tabs.setCurrentIndex(0)
        self.record_player.stop()
        source = QUrl()
        source.setScheme("rtsp")
        source.setHost(config.host)
        source.setPort(554)
        source.setUserName(config.user)
        source.setPassword(config.camera_password)
        source.setPath("/stream1" if config.quality == "Alta" else "/stream2")
        self.set_status("Conectando", "Abriendo el vídeo en directo…")
        self.live.open_source(source)

    def tab_changed(self, index):
        if index == 0:
            self.record_player.stop()
        else:
            self.live.stop()
            self.set_status("Grabaciones", "Consulta una fecha para acceder a la tarjeta microSD.")

    def start_operation(self, action, config, on_result, **extra):
        if self.operation:
            return
        operation = Operation({"action": action, "config": asdict(config), **extra}, self)
        self.operation = operation
        self.operation_bar.show()
        self.operation_label.setText("Conectando con la cámara…")
        self.progress.setRange(0, 0)
        self.set_busy(True)
        operation.progress.connect(self.operation_progress)

        def result_received(value):
            if not operation.cancelled.is_set():
                on_result(value)

        operation.result.connect(result_received)
        operation.error.connect(self.operation_error)
        operation.finished.connect(lambda: self.finish_operation(operation))
        operation.start()

    def set_busy(self, busy):
        for control in self.config_fields + self.move_buttons + [self.date, self.today_button, self.list_button, self.table]:
            control.setEnabled(not busy)
        if os.name != "nt":
            self.remember.setEnabled(False)
        self.open_button.setEnabled(not busy and bool(self.recordings))
        self.export_button.setEnabled(not busy and bool(self.recordings))

    def operation_progress(self, message, percent):
        self.operation_label.setText(message)
        if percent < 0:
            self.progress.setRange(0, 0)
        else:
            self.progress.setRange(0, 100)
            self.progress.setValue(percent)

    def operation_error(self, message):
        if self.operation and self.operation.request["action"] == "list":
            self.catalogue_label.setText("No se ha podido consultar la microSD. Revisa la conexión y vuelve a intentarlo.")
        self.set_status("Revisar conexión", message)
        QMessageBox.warning(self, "Conexión con la cámara", message)

    def finish_operation(self, operation):
        if self.operation is operation:
            self.operation = None
            self.set_busy(False)
            self.operation_bar.hide()
        operation.deleteLater()

    def cancel_operation(self):
        if self.operation:
            self.operation.cancel()
            self.operation_label.setText("Cancelando…")
            self.statusBar().showMessage("Operación cancelada. Puedes volver a intentarlo.")

    def probe(self, checked=False):
        config = self.validated_config()
        if config:
            self.start_operation("probe", config, lambda value: self.set_status("Accesible", value["message"]))

    def move_camera(self, x, y):
        config = self.validated_config()
        if config:
            self.start_operation("move", config, lambda value: self.statusBar().showMessage("Movimiento enviado a la cámara."), x=x, y=y)

    def list_recordings(self, checked=False):
        config = self.validated_config()
        if not config:
            return
        self.live.stop()
        self.record_player.stop()
        self.recordings = []
        self.table.setRowCount(0)
        self.catalogue_profile = None
        self.playing_index = -1
        day = self.date.date().toString("yyyyMMdd")
        self.catalogue_label.setText("Consultando la microSD…")

        def received(value):
            self.recordings = [Recording(**item) for item in value["recordings"]]
            self.catalogue_profile = asdict(config)
            self.table.setRowCount(len(self.recordings))
            for row, recording in enumerate(self.recordings):
                values = (recording.local_start.strftime("%H:%M:%S"), recording.local_end.strftime("%H:%M:%S"),
                          format_duration(recording.duration))
                for column, text in enumerate(values):
                    item = QTableWidgetItem(text)
                    item.setTextAlignment(Qt.AlignmentFlag.AlignCenter)
                    item.setToolTip(recording.local_start.strftime("%d/%m/%Y %H:%M:%S %Z"))
                    self.table.setItem(row, column, item)
            if self.recordings:
                self.table.selectRow(0)
            date_text = f"{day[6:8]}/{day[4:6]}/{day[:4]}"
            self.catalogue_label.setText(f"{len(self.recordings)} tramos disponibles · {date_text} · Horas de Madrid" if self.recordings
                                         else f"Sin grabaciones disponibles el {date_text}.")
            self.set_status("Catálogo listo", "Elige un vídeo para reproducirlo o guardar una copia.")

        self.start_operation("list", config, received, day=day)

    def selected_recording(self):
        row = self.table.currentRow()
        if not 0 <= row < len(self.recordings):
            self.statusBar().showMessage("Selecciona una grabación de la lista.")
            return None
        if self.catalogue_profile != asdict(self.config()):
            QMessageBox.information(self, "Grabaciones", "Has cambiado los datos de conexión. Consulta de nuevo la microSD para cargar su catálogo.")
            return None
        return row, self.recordings[row]

    def fetch_recording(self, after):
        if self.operation:
            return
        selected = self.selected_recording()
        if not selected:
            return
        row, recording = selected
        config = self.config()
        cache = data_directory() / "videos" / config.cache_key() / recording.filename
        legacy = cache.with_name(recording.legacy_filename)
        if not cache.exists() and legacy.exists():
            cache = legacy
        self.live.stop()
        self.record_player.stop()
        self.start_operation("download", config, lambda value: after(Path(value["path"]), row),
                             recording=recording.to_dict(), destination=str(cache))

    def open_recording(self, checked=False):
        def ready(path, row):
            self.playing_index = row
            self.record_player.open_source(QUrl.fromLocalFile(str(path)), path)
            self.statusBar().showMessage("Abriendo la grabación. La copia descargada se conserva en este PC.")
        self.fetch_recording(ready)

    def export_recording(self, checked=False):
        selected = self.selected_recording()
        if not selected:
            return
        _, recording = selected
        target, _ = QFileDialog.getSaveFileName(self, "Guardar copia de la grabación",
            str(Path.home() / "Videos" / recording.filename), "Vídeo MP4 (*.mp4)")
        if not target:
            return

        def ready(path, row):
            destination = Path(target)
            if destination.resolve() == path.resolve():
                self.statusBar().showMessage("La copia ya está guardada en esa ubicación.")
                return
            temporary = destination.with_name(destination.name + ".partial")
            try:
                shutil.copyfile(path, temporary)
                os.replace(temporary, destination)
                self.statusBar().showMessage("Copia guardada correctamente.")
            except OSError:
                temporary.unlink(missing_ok=True)
                QMessageBox.warning(self, "Guardar copia", "No se ha podido guardar la copia en esa carpeta.")
        self.fetch_recording(ready)

    def next_recording(self):
        if (self.continue_next.isChecked() and not self.operation and self.tabs.currentIndex() == 1
                and 0 <= self.playing_index < len(self.recordings) - 1):
            self.table.selectRow(self.playing_index + 1)
            QTimer.singleShot(0, self.open_recording)

    def open_cache_folder(self, checked=False):
        directory = data_directory() / "videos"
        directory.mkdir(parents=True, exist_ok=True)
        QDesktopServices.openUrl(QUrl.fromLocalFile(str(directory)))

    def export_diagnostic(self, checked=False):
        source = data_directory() / 'last-diagnostic.json'
        if not source.is_file():
            QMessageBox.information(self, 'Diagnóstico', 'Todavía no hay ningún error registrado. Si falla una operación, podrás guardar aquí su diagnóstico.')
            return
        target, _ = QFileDialog.getSaveFileName(self, 'Guardar diagnóstico',
            str(Path.home() / 'Documents' / 'Visor_Tapo_Diagnostico.json'), 'Diagnóstico JSON (*.json)')
        if target:
            try:
                if Path(target).resolve() != source.resolve():
                    shutil.copyfile(source, target)
                self.statusBar().showMessage('Diagnóstico guardado. No contiene contraseñas ni imágenes de la cámara.')
            except OSError:
                QMessageBox.warning(self, 'Diagnóstico', 'No se ha podido guardar el diagnóstico en esa carpeta.')

    def closeEvent(self, event):
        if self.operation:
            self.operation.cancel()
            if self.operation.thread:
                self.operation.thread.join(timeout=3)
        self.live.stop()
        self.record_player.stop()
        event.accept()


def main():
    # Native decoder errors may contain the RTSP URL. Never save or echo them.
    qInstallMessageHandler(lambda *_: None)
    application = QApplication(sys.argv)
    application.setApplicationName(APP_NAME)
    application.setOrganizationName("XyceL")
    application.setStyle("Fusion")
    application.setStyleSheet(STYLE)
    application.setWindowIcon(logo())
    window = MainWindow()
    window.show()
    return application.exec()


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        # Record only exception type and frame locations, without messages or locals.
        import traceback
        summary = type(error).__name__ + "\n" + "\n".join(
            f"{Path(frame.filename).name}:{frame.lineno}" for frame in traceback.extract_tb(error.__traceback__))
        try:
            (data_directory() / "startup-error.txt").write_text(summary, encoding="utf-8")
        except OSError:
            pass
        raise SystemExit(1)
