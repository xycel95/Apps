"""One isolated camera operation. Secrets arrive on stdin, never in arguments."""
from __future__ import annotations
import asyncio
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
import time
import traceback
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
os.environ["PATH"] = str(ROOT / "bin") + os.pathsep + os.environ.get("PATH", "")
from core import VERSION, CameraConfig, Recording, check_recording_file, friendly_error, normalise_recordings


def emit(kind: str, **values):
    print(json.dumps({"type": kind, **values}, ensure_ascii=False), flush=True)


def make_camera(config: CameraConfig):
    from pytapo import Tapo
    config.validate()
    os.environ["NO_PROXY"] = os.environ.get("NO_PROXY", "") + "," + config.host
    if config.tapo_password:
        return Tapo(config.host, "admin", config.tapo_password,
                    cloudPassword=config.tapo_password, printDebugInformation=False,
                    printWarnInformation=False, redactConfidentialInformation=True)
    return Tapo(config.host, config.user, config.camera_password,
                printDebugInformation=False, printWarnInformation=False,
                redactConfidentialInformation=True)


def run_process(command, timeout=60):
    result = subprocess.run(command, capture_output=True, timeout=timeout,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if result.returncode:
        raise RuntimeError("No se ha podido preparar el vídeo descargado.")
    return result


def install_converter():
    """Use argument arrays, hidden FFmpeg windows and checked conversion results."""
    import pytapo.media_stream.downloader as downloader_module
    from pytapo.media_stream.convert import Convert

    class SafeConvert(Convert):
        def calculateLength(self):
            self._last_length_calc_time = time.time()
            temporary = None
            try:
                with tempfile.NamedTemporaryFile(delete=False, suffix=".ts") as file:
                    temporary = Path(file.name)
                    file.write(self.writer.getvalue())
                result = run_process(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                                      "-of", "default=noprint_wrappers=1:nokey=1", str(temporary)], 15)
                duration = float(result.stdout)
                if not math.isfinite(duration) or duration <= 0:
                    return False
                self.known_lengths[self.addedChunks] = duration
                self.lengthLastCalculatedAtChunk = self.addedChunks
                self._cached_bytes_per_second = len(self.writer.getvalue()) / duration
                return duration
            except Exception:
                return False
            finally:
                if temporary:
                    temporary.unlink(missing_ok=True)

        async def save(self, fileLocation, fileLength, method="ffmpeg"):
            destination = Path(fileLocation)
            with tempfile.TemporaryDirectory(prefix="c230-convert-", dir=destination.parent) as temp:
                video = Path(temp) / "video.ts"
                video.write_bytes(self.writer.getvalue())
                args = ["ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-i", str(video)]
                if self.audioWriter.getbuffer().nbytes:
                    audio = Path(temp) / "audio.raw"
                    audio.write_bytes(self.audioWriter.getvalue())
                    args += ["-f", self._get_audio_format(), "-ar", str(self._get_audio_rate()),
                             "-ac", "1", "-i", str(audio), "-map", "0:v:0", "-map", "1:a:0"]
                else:
                    args += ["-map", "0:v:0", "-map", "0:a?"]
                args += ["-t", str(fileLength), "-c:v", "copy", "-c:a", "aac", "-movflags", "+faststart", str(destination)]
                await asyncio.to_thread(run_process, args, 90)

    downloader_module.Convert = SafeConvert
    return downloader_module.Downloader


async def download_recording(camera, recording: Recording, destination: Path):
    Downloader = install_converter()
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="receiving-", dir=destination.parent) as temp:
        partial = Path(temp) / "recording.mp4"
        downloader = Downloader(camera, recording.start, recording.end, recording.correction,
                                outputDirectory=str(Path(temp)) + os.sep, padding=0,
                                overwriteFiles=False, window_size=50, fileName=partial.name,
                                stall_timeout=25, progressInterval=2, output="mp4")
        async for status in downloader.download():
            action = status.get("currentAction", "")
            if action == "Camera busy":
                raise RuntimeError("Camera busy")
            if action == "Recording in progress":
                raise RuntimeError("La cámara todavía está escribiendo este vídeo. Prueba con una grabación anterior.")
            if action == "Giving up":
                raise RuntimeError("La cámara ha interrumpido la descarga. Vuelve a intentarlo.")
            messages = {"Downloading": "Recibiendo vídeo de la microSD…", "Retrying": "Reintentando la descarga…",
                        "Converting": "Preparando la reproducción…", "Converting [shorter]": "Preparando la reproducción…"}
            total, progress = float(status.get("total", 0)), float(status.get("progress", 0))
            percent = max(0, min(95, int(100 * progress / total))) if total > 0 else -1
            emit("progress", message=messages.get(action, "Preparando el vídeo…"), percent=percent)
        check_recording_file(partial, recording.duration)
        os.replace(partial, destination)
    return {"path": str(destination)}


def execute(request: dict):
    if request.get('action') == 'discover':
        from discovery import discover
        return discover()
    config = CameraConfig(**request["config"])
    action = request.get("action")
    if action == "download":
        recording = Recording(**request["recording"])
        if not 0 < recording.duration <= 300:
            raise ValueError("Duración de fragmento no válida.")
        destination = Path(request["destination"])
        if destination.suffix.lower() != ".mp4":
            raise ValueError("Destino no válido")
        if destination.exists():
            try:
                check_recording_file(destination, recording.duration)
                return {"path": str(destination)}
            except (RuntimeError, ValueError):
                # This is the app's local cache, never the camera's SD card.
                destination.unlink()
    camera = make_camera(config)
    if action == "probe":
        info = camera.getBasicInfo()
        # Do not return serial numbers, MAC addresses or credentials.
        return {"connected": bool(info), "message": "Cámara accesible. Credenciales aceptadas."}
    if action == "list":
        day = str(request["day"])
        if len(day) != 8 or not day.isdecimal():
            raise ValueError("Fecha no válida")
        raw = camera.getRecordings(day)
        correction = camera.getTimeCorrection()
        if correction is False or not isinstance(correction, (int, float)):
            raise RuntimeError("No se ha podido leer el reloj de la cámara.")
        recordings = normalise_recordings(raw, int(correction), time.time())
        return {"recordings": [r.to_dict() for r in recordings], "correction": int(correction)}
    if action == "move":
        x, y = int(request["x"]), int(request["y"])
        if not -20 <= x <= 20 or not -20 <= y <= 20:
            raise ValueError("Movimiento no válido")
        camera.moveMotor(x, y)
        return {"moved": True}
    if action == "download":
        # A new worker has a new Tapo instance. Fetch the control session ID
        # before asyncio.run: Downloader._buildRequest calls getUserID inside
        # its media loop, where a synchronous control login cannot run.
        camera.getUserID()
        emit("progress", message="Solicitando el vídeo a la cámara…", percent=-1)
        return asyncio.run(download_recording(camera, recording, destination))
    raise ValueError("Operación no válida")


def error_report(error, action):
    # Only structured types, codes and source locations; no exception text,
    # response body, IP, username, authenticated URL or credential values.
    report = {'app_version': VERSION, 'action': action,
              'error_type': type(error).__name__,
              'frames': [{'file': Path(f.filename).name, 'function': f.name, 'line': f.lineno}
                         for f in traceback.extract_tb(error.__traceback__)]}
    status = getattr(error, 'status_code', None)
    if isinstance(status, int):
        report['http_status'] = status
    known = ("La cámara no ha entregado", "La cámara todavía está escribiendo", "La cámara ha interrumpido",
             "La descarga está incompleta", "El archivo recibido", "No se ha podido preparar el vídeo",
             "No se ha podido leer el reloj")
    if str(error).startswith(known):
        message = str(error)
    elif action == 'download' and status in (401, 403):
        message = ('La cámara ha rechazado el acceso al canal de vídeo de la microSD '
                   f'(HTTP {status}). La consulta del catálogo y la descarga usan conexiones distintas.')
    elif action == 'download' and isinstance(error, FileNotFoundError):
        message = 'Falta un componente para preparar el vídeo. Extrae todo el ZIP y conserva la carpeta bin.'
    elif action == 'download' and 'event loop' in str(error).lower():
        message = 'La app no ha podido preparar la sesión de descarga. Guarda el diagnóstico para revisar este fallo.'
    else:
        message = friendly_error(error)
        if action == 'download' and message.startswith('No se ha podido completar'):
            message = ('No se ha podido descargar o preparar el vídeo. Guarda el diagnóstico '
                       'desde Grabaciones para identificar la causa.')
    return message, report


def main():
    request = {}
    try:
        request = json.loads(sys.stdin.readline(65536))
        result = execute(request)
        emit("result", result=result)
    except Exception as error:
        action = request.get('action', 'unknown') if isinstance(request, dict) else 'unknown'
        message, report = error_report(error, action)
        emit("error", message=message, diagnostic=report)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
