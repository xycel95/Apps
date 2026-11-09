"""Local settings and recording catalogue for Visor Tapo. No camera writes."""
from __future__ import annotations

import base64
import ctypes
import hashlib
import ipaddress
import json
import math
import os
import re
import tempfile
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

VERSION = "1.2.0"
APP_NAME = "Visor Tapo"
MADRID = ZoneInfo("Europe/Madrid")
LOCAL_NETWORKS = tuple(ipaddress.ip_network(n) for n in
                       ("10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"))


def data_directory() -> Path:
    # Keep the historical directory so existing encrypted settings and copies
    # remain available when upgrading from Visor C230 to Visor Tapo.
    override = os.environ.get('VISOR_TAPO_DATA') or os.environ.get('VISOR_C230_DATA')
    root = Path(override) if override else (
        Path(os.environ.get("LOCALAPPDATA", str(Path.home() / ".local/share"))) / "VisorC230")
    root.mkdir(parents=True, exist_ok=True)
    return root


@dataclass
class CameraConfig:
    host: str = ""
    user: str = ""
    camera_password: str = ""
    tapo_password: str = ""
    quality: str = "Alta"
    remember: bool = False

    def validate(self, live: bool = False) -> None:
        try:
            address = ipaddress.ip_address(self.host.strip())
        except ValueError:
            raise ValueError("Escribe la IP local de la cámara, por ejemplo 192.168.1.50.") from None
        if not any(address in network for network in LOCAL_NETWORKS):
            raise ValueError("Esta versión se conecta a direcciones privadas de la red de casa (10.x, 172.16–31.x o 192.168.x).")
        if live and (not self.user or not self.camera_password):
            raise ValueError("Para el directo, completa el usuario y la contraseña de «Cuenta de cámara» de Tapo.")
        if not live and not self.tapo_password and (not self.user or not self.camera_password):
            raise ValueError("Completa la contraseña de Tapo para consultar la microSD. También se puede probar con la cuenta de cámara.")

    def public(self) -> dict:
        return {"host": self.host, "user": self.user, "quality": self.quality, "remember": self.remember}

    def cache_key(self) -> str:
        return hashlib.sha256(self.host.strip().encode()).hexdigest()[:16]


def _dpapi(data: bytes, decrypt: bool = False) -> bytes:
    if os.name != "nt":
        raise OSError("El guardado protegido de contraseñas está disponible en Windows.")

    class Blob(ctypes.Structure):
        _fields_ = [("cbData", ctypes.c_ulong), ("pbData", ctypes.POINTER(ctypes.c_ubyte))]

    buffer = ctypes.create_string_buffer(data)
    source = Blob(len(data), ctypes.cast(buffer, ctypes.POINTER(ctypes.c_ubyte)))
    destination = Blob()
    crypt32 = ctypes.WinDLL("crypt32", use_last_error=True)
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    if decrypt:
        function = crypt32.CryptUnprotectData
        function.argtypes = [ctypes.POINTER(Blob), ctypes.c_void_p, ctypes.c_void_p,
                             ctypes.c_void_p, ctypes.c_void_p, ctypes.c_ulong, ctypes.POINTER(Blob)]
        ok = function(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(destination))
    else:
        function = crypt32.CryptProtectData
        function.argtypes = [ctypes.POINTER(Blob), ctypes.c_wchar_p, ctypes.c_void_p,
                             ctypes.c_void_p, ctypes.c_void_p, ctypes.c_ulong, ctypes.POINTER(Blob)]
        ok = function(ctypes.byref(source), APP_NAME, None, None, None, 1, ctypes.byref(destination))
    if not ok:
        raise OSError("Windows no ha podido proteger o recuperar las contraseñas.")
    try:
        return ctypes.string_at(destination.pbData, destination.cbData)
    finally:
        kernel32.LocalFree.argtypes = [ctypes.c_void_p]
        kernel32.LocalFree(ctypes.cast(destination.pbData, ctypes.c_void_p))


def save_settings(config: CameraConfig, directory: Path | None = None) -> None:
    directory = directory or data_directory()
    directory.mkdir(parents=True, exist_ok=True)
    result = config.public()
    if config.remember:
        secret = json.dumps({"camera_password": config.camera_password,
                             "tapo_password": config.tapo_password}).encode()
        result["protected_credentials"] = base64.b64encode(_dpapi(secret)).decode("ascii")
    handle, temporary = tempfile.mkstemp(prefix="settings-", dir=directory)
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as file:
            json.dump(result, file, ensure_ascii=False, indent=2)
        os.replace(temporary, directory / "settings.json")
    finally:
        Path(temporary).unlink(missing_ok=True)


def load_settings(directory: Path | None = None) -> tuple[CameraConfig, str]:
    directory = directory or data_directory()
    path = directory / "settings.json"
    if not path.exists():
        return CameraConfig(), ""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        config = CameraConfig(**{key: data.get(key, default) for key, default in
            (("host", ""), ("user", ""), ("quality", "Alta"), ("remember", False))})
        if data.get("protected_credentials"):
            try:
                secret = json.loads(_dpapi(base64.b64decode(data["protected_credentials"]), decrypt=True))
                config.camera_password = str(secret.get("camera_password", ""))
                config.tapo_password = str(secret.get("tapo_password", ""))
            except Exception:
                config.remember = False
                return config, "Vuelve a introducir las contraseñas: Windows no ha podido recuperar las guardadas."
        return config, ""
    except (OSError, ValueError, TypeError):
        return CameraConfig(), "No se han podido leer los ajustes. Puedes introducirlos de nuevo."


@dataclass(frozen=True)
class Recording:
    start: int
    end: int
    correction: int = 0

    @property
    def duration(self) -> int:
        return self.end - self.start

    @property
    def local_start(self) -> datetime:
        return datetime.fromtimestamp(self.start + self.correction, MADRID)

    @property
    def local_end(self) -> datetime:
        return datetime.fromtimestamp(self.end + self.correction, MADRID)

    @property
    def filename(self) -> str:
        return f"Tapo_{self.local_start:%Y-%m-%d_%H-%M-%S}_{self.start}_{self.end}.mp4"

    @property
    def legacy_filename(self) -> str:
        return 'C230_' + self.filename.removeprefix('Tapo_')

    def to_dict(self) -> dict:
        return asdict(self)


def normalise_recordings(raw, correction: int = 0, now: float | None = None,
                         chunk_seconds: int = 300) -> list[Recording]:
    """Flatten firmware variants, deduplicate and bound each download's memory use.

    API timestamps stay unchanged for requests. Correction is used for display and
    to omit the most recent 65 seconds, while the camera finishes writing them.
    """
    if not isinstance(raw, (list, dict)):
        raise ValueError("La cámara ha devuelto un catálogo de grabaciones no reconocido.")
    if chunk_seconds < 1:
        raise ValueError("Duración de fragmento no válida.")
    intervals = set()
    malformed = False

    def walk(node):
        nonlocal malformed
        if isinstance(node, dict):
            start = node.get("startTime", node.get("start_time"))
            end = node.get("endTime", node.get("end_time"))
            if start is not None or end is not None:
                try:
                    first, last = int(start), int(end)
                    if not 946684800 <= first < last <= 7258118400:
                        raise ValueError()
                    intervals.add((first, last))
                except (ValueError, TypeError, OverflowError):
                    malformed = True
            else:
                for child in node.values():
                    walk(child)
        elif isinstance(node, list):
            for child in node:
                walk(child)

    walk(raw)
    if malformed or (raw and not intervals):
        raise ValueError("No se ha podido interpretar el horario de las grabaciones de este firmware.")
    ready_until = int(now) - int(correction) - 65 if now is not None else None
    result = set()
    for first, last in sorted(intervals):
        if ready_until is not None:
            last = min(last, ready_until)
        while first < last:
            stop = min(first + chunk_seconds, last)
            result.add(Recording(first, stop, int(correction)))
            first = stop
    return sorted(result, key=lambda r: (r.start, r.end))


def format_duration(seconds: int | float) -> str:
    value = max(0, int(seconds))
    minutes, seconds = divmod(value, 60)
    hours, minutes = divmod(minutes, 60)
    return f"{hours:02}:{minutes:02}:{seconds:02}" if hours else f"{minutes:02}:{seconds:02}"


def friendly_error(error: Exception) -> str:
    message = str(error).lower()
    if any(word in message for word in ("authentication", "password", "login", "-40211", "-40411", "-40401")):
        return ("La cámara no ha aceptado las credenciales. Revisa la contraseña de Tapo para la microSD "
                "y la cuenta de cámara para el directo. Si acabas de cambiar la contraseña, abre primero Tapo en el móvil.")
    if "busy" in message or "-524" in message:
        return "La cámara está ocupada. Cierra la reproducción en el móvil y vuelve a intentarlo."
    if any(word in message for word in ("timeout", "timed out", "connection", "unreachable", "refused")):
        return "No se ha podido conectar con la cámara. Comprueba la IP y que el PC y la cámara estén en la misma red."
    if "not supported" in message or "firmware" in message:
        return "Esta función no ha sido aceptada por el firmware de la cámara. El directo puede seguir funcionando."
    if isinstance(error, ValueError):
        # Only our own validation messages are public; caller controls these values.
        if message.startswith(("escribe la ip", "esta versión", "para el directo", "completa la contraseña",
                               "la cámara ha devuelto", "no se ha podido interpretar", "duración de fragmento")):
            return str(error)
    return "No se ha podido completar la operación. Revisa la conexión y las credenciales de la cámara."


def media_info(path: Path, probe: str = "ffprobe") -> dict:
    import subprocess
    command = [probe, "-v", "error", "-show_entries", "format=duration:stream=codec_type",
               "-of", "json", str(path)]
    result = subprocess.run(command, capture_output=True, timeout=30,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if result.returncode:
        raise RuntimeError("La cámara no ha entregado un vídeo válido.")
    return json.loads(result.stdout)


def check_recording_file(path: Path, expected_seconds: int, probe: str = "ffprobe") -> None:
    if not path.is_file() or path.stat().st_size < 188:
        raise RuntimeError("La cámara no ha entregado el vídeo. Prueba con una grabación anterior.")
    info = media_info(path, probe)
    if not any(stream.get("codec_type") == "video" for stream in info.get("streams", [])):
        raise RuntimeError("El archivo recibido no contiene vídeo.")
    duration = float(info.get("format", {}).get("duration", 0))
    if not math.isfinite(duration) or duration < max(0.1, expected_seconds - 6):
        raise RuntimeError("La descarga está incompleta. Vuelve a intentarlo.")
