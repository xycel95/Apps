"""Reproducible Windows x64 portable package. Run from Linux, macOS or Windows.

Build dependencies: pip install ziglang==0.17.0 pefile==2024.8.26
All payload downloads are pinned in build_inputs.json and SHA-256 checked.
"""
from __future__ import annotations
import hashlib
import json
import os
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

BASE = Path(__file__).resolve().parent
BUILD = BASE / "build"
MANIFEST = json.loads((BASE / "build_inputs.json").read_text())
DOCUMENTS = ('README.md', 'README.txt', 'COMPATIBILIDAD.md', 'CHANGELOG.md',
             'CONTRIBUTING.md', 'TERCEROS.md', 'VALIDACION.txt', 'build_inputs.json',
             'requirements-windows.txt', 'requirements-dev.txt', 'requirements-build-windows.txt',
             'build_windows.py', 'build_onefile.py', 'portable_launcher.py', '.gitignore')


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as file:
        while chunk := file.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def obtain(entry, path):
    if path.exists() and sha256(path) == entry["sha256"]:
        return path
    path.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(entry["url"], timeout=90) as response, path.open("wb") as file:
        shutil.copyfileobj(response, file)
    if sha256(path) != entry["sha256"]:
        path.unlink()
        raise RuntimeError("Checksum mismatch: " + path.name)
    return path


def extract_wheel(wheel, runtime):
    site = runtime / "Lib" / "site-packages"
    with zipfile.ZipFile(wheel) as archive:
        for info in archive.infolist():
            parts = Path(info.filename).parts
            if not parts or info.is_dir():
                continue
            if parts[0].endswith(".data"):
                if parts[1] in ("purelib", "platlib"):
                    target = site.joinpath(*parts[2:])
                elif parts[1] == "data" and len(parts) == 3:
                    target = runtime / parts[-1]
                else:
                    continue
            else:
                target = site.joinpath(*parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info) as source, target.open("wb") as output:
                shutil.copyfileobj(source, output)


def compact_qt(runtime):
    """Keep the imported modules, Windows plugins and their DLL import closure."""
    import pefile
    qt = runtime / "Lib" / "site-packages" / "PySide6"
    modules = {"QtCore", "QtGui", "QtWidgets", "QtNetwork", "QtMultimedia", "QtMultimediaWidgets"}
    for module in qt.glob("*.pyd"):
        if module.name.split(".")[0] not in modules:
            module.unlink()
    for name in ("qml", "include", "typesystems", "glue", "doc", "metatypes", "resources"):
        shutil.rmtree(qt / name, ignore_errors=True)
        shutil.rmtree(qt / "Qt" / name, ignore_errors=True)
    plugins = qt / "plugins"
    keep_plugins = {"qwindows.dll", "ffmpegmediaplugin.dll", "qjpeg.dll", "qico.dll", "qschannelbackend.dll"}
    for plugin in plugins.rglob("*.dll"):
        if plugin.name.lower() not in keep_plugins:
            plugin.unlink()
    for translations in (qt / "translations", qt / "Qt" / "translations"):
        for file in translations.glob("*.qm"):
            if file.name not in ("qtbase_es.qm", "qtmultimedia_es.qm", "qt_es.qm"):
                file.unlink()
    dlls = {file.name.lower(): file for file in runtime.rglob("*.dll")}
    seeds = list(runtime.rglob("*.pyd")) + list(plugins.rglob("*.dll"))
    seeds += [runtime / "python.exe", runtime / "pythonw.exe"]
    # These graphics support DLLs can be loaded by filename rather than PE imports.
    seeds += [file for file in qt.rglob("*.dll") if file.name.lower() in
              {"d3dcompiler_47.dll", "dxcompiler.dll", "dxil.dll", "opengl32sw.dll"}]
    keep = set()
    pending = list(seeds)
    while pending:
        path = pending.pop()
        if path in keep:
            continue
        keep.add(path)
        try:
            pe = pefile.PE(str(path), fast_load=True)
            pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
                                                  pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT"]])
            dependencies = list(getattr(pe, "DIRECTORY_ENTRY_IMPORT", [])) + list(getattr(pe, "DIRECTORY_ENTRY_DELAY_IMPORT", []))
            for entry in dependencies:
                name = entry.dll.decode("ascii").lower()
                if name in dlls:
                    pending.append(dlls[name])
            pe.close()
        except pefile.PEFormatError:
            pass
    for file in qt.rglob("*.dll"):
        if file not in keep and file.name.lower() not in {"msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll"}:
            file.unlink()
    return sorted(str(file.relative_to(runtime)) for file in keep if file.is_relative_to(runtime))


def compile_launcher(destination):
    import ziglang
    zig = Path(ziglang.__file__).resolve().parent / ("zig.exe" if os.name == "nt" else "zig")
    subprocess.run([str(zig), "rc", "/fo", str(BUILD / "resources.res"), "resources.rc"],
                   cwd=BASE / "src", stdin=subprocess.DEVNULL, check=True)
    subprocess.run([str(zig), "cc", "-target", "x86_64-windows-gnu", "-municode", "-O2",
                    "-Wl,--subsystem,windows", "launcher.c", str(BUILD / "resources.res"), "-o", str(destination), "-luser32"],
                   cwd=BASE / "src", stdin=subprocess.DEVNULL, check=True)
    import pefile
    pe = pefile.PE(str(destination))
    if pe.FILE_HEADER.Machine != 0x8664 or pe.OPTIONAL_HEADER.Subsystem != 2:
        raise RuntimeError("Expected a Windows x64 GUI executable")
    if not hasattr(pe, "DIRECTORY_ENTRY_RESOURCE"):
        raise RuntimeError("Missing application icon and manifest")
    pe.close()


def build():
    version = MANIFEST["app_version"]
    output = BASE / "dist" / f"Visor_Tapo_{version}_Windows"
    if output.exists():
        shutil.rmtree(output)
    runtime = output / "runtime"
    runtime.mkdir(parents=True)
    python_zip = obtain(MANIFEST["python"], BUILD / "python-embed.zip")
    with zipfile.ZipFile(python_zip) as archive:
        archive.extractall(runtime)
    for entry in MANIFEST["wheels"]:
        wheel = obtain(entry, BUILD / "wheels-windows" / entry["filename"])
        extract_wheel(wheel, runtime)
    for pth in runtime.glob("python*._pth"):
        pth.write_text("python313.zip\n.\nLib/site-packages\n../src\nimport site\n", encoding="utf-8")
    kept = compact_qt(runtime)
    (BUILD / "qt-runtime-files.json").write_text(json.dumps(kept, indent=2))
    ffmpeg_zip = obtain(MANIFEST["ffmpeg"], BUILD / "ffmpeg-essentials.zip")
    with zipfile.ZipFile(ffmpeg_zip) as archive:
        for member in archive.namelist():
            if member.endswith(("/bin/ffmpeg.exe", "/bin/ffprobe.exe")):
                target = output / "bin" / Path(member).name
            elif member.endswith(("/LICENSE", "/README.txt")) or "/doc/" in member:
                if member.endswith("/"):
                    continue
                target = output / "licencias" / "FFmpeg" / Path(member).name
            else:
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(member))
    shutil.copytree(BASE / "src", output / "src", ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    shutil.copytree(BASE / "tests", output / "tests", ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    for name in DOCUMENTS:
        shutil.copy2(BASE / name, output / name)
    shutil.copytree(BASE / '.github', output / '.github')
    shutil.copy2(BASE / "LICENSE", output / "LICENSE")
    compile_launcher(output / "Visor Tapo.exe")
    package_manifest = [{"file": str(path.relative_to(output)).replace(os.sep, "/"),
                         "size": path.stat().st_size, "sha256": sha256(path)}
                        for path in sorted(output.rglob("*")) if path.is_file()]
    (output / "ARCHIVOS_SHA256.json").write_text(json.dumps(package_manifest, indent=2))
    archive_path = BASE / "dist" / f"Visor_Tapo_{version}_Windows.zip"
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(output.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(output.parent))
    source_path = BASE / "dist" / f"Visor_Tapo_Codigo_{version}.zip"
    with zipfile.ZipFile(source_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for name in ('src', 'tests', '.github', 'LICENSE', *DOCUMENTS):
            path = BASE / name
            paths = path.rglob("*") if path.is_dir() else [path]
            for item in paths:
                if item.is_file() and "__pycache__" not in item.parts and item.suffix != ".pyc":
                    archive.write(item, Path("Visor_Tapo_Codigo") / item.relative_to(BASE))
    print(json.dumps({"windows_zip": str(archive_path), "size": archive_path.stat().st_size,
                      "source_zip": str(source_path), "source_size": source_path.stat().st_size,
                      "payload_files": len(package_manifest)}, indent=2))


if __name__ == "__main__":
    build()
