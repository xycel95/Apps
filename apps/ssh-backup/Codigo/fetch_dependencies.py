"""Descarga dependencias oficiales de NuGet para .NET Framework 4.8."""
import concurrent.futures
import hashlib
import json
import pathlib
import re
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent
PACKAGES = ROOT / "deps"
LIB = PACKAGES / "lib"
LICENSES = ROOT / "Licencias"
for folder in (PACKAGES, LIB, LICENSES):
    folder.mkdir(parents=True, exist_ok=True)

def framework(value):
    return value.lower().replace(".netframework", "net").replace(".netstandard", "netstandard").replace(".", "")

def rank(value):
    value = framework(value)
    if value.startswith("netstandard"):
        return {"netstandard20": 200, "netstandard10": 100}.get(value, -1)
    if value.startswith("net4"):
        number = value[3:]
        number = (number + "00")[:3]
        if int(number) <= 480:
            return 1000 + int(number)
    return -1

def fetch(item):
    name, version = item
    file = PACKAGES / (name.lower() + "." + version + ".nupkg")
    url = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg".format(name.lower(), version)
    if not file.exists():
        with urllib.request.urlopen(url, timeout=45) as response:
            file.write_bytes(response.read())
    dependencies = []
    with zipfile.ZipFile(file) as archive:
        spec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith(".nuspec"))))
        ns = {"n": spec.tag.split("}")[0].strip("{")}
        groups = spec.findall(".//n:dependencies/n:group", ns)
        group = max(groups, key=lambda g: rank(g.get("targetFramework", "")), default=None)
        if group is not None and rank(group.get("targetFramework", "")) < 0:
            raise RuntimeError("No hay grupo compatible: " + name)
        elements = group.findall("n:dependency", ns) if group is not None else spec.findall(".//n:dependencies/n:dependency", ns)
        for dependency in elements:
            v = re.search(r"\d+(?:\.\d+){1,3}(?:-[\w.]+)?", dependency.get("version", ""))
            if v:
                dependencies.append((dependency.attrib["id"], v.group()))
        dlls = [n for n in archive.namelist() if re.match(r"lib/[^/]+/[^/]+\.dll$", n)]
        targets = sorted({n.split("/")[1] for n in dlls}, key=rank, reverse=True)
        if targets:
            target = targets[0]
            if rank(target) < 0:
                raise RuntimeError("Sin DLL compatible: " + name + " " + repr(targets))
            for n in dlls:
                if n.split("/")[1] == target:
                    (LIB / pathlib.PurePosixPath(n).name).write_bytes(archive.read(n))
            for n in archive.namelist():
                if n.startswith("lib/" + target + "/") and n.endswith(".xml"):
                    (PACKAGES / pathlib.PurePosixPath(n).name).write_bytes(archive.read(n))
        license_files = [n for n in archive.namelist() if re.search(r"(^|/)(license|licence|notice|copyright)[^/]*$", n, re.I)]
        for i, n in enumerate(license_files):
            if not n.endswith("/"):
                (LICENSES / (name + "-" + str(i) + "-" + pathlib.PurePosixPath(n).name)).write_bytes(archive.read(n))
        if not license_files:
            license_element = spec.find(".//n:license", ns)
            (LICENSES / (name + ".txt")).write_text(
                name + " " + version + "\nLicencia: " + (license_element.text if license_element is not None else "Ver paquete oficial") +
                "\n" + url + "\n", encoding="utf-8")
    return {"id": name, "version": version, "sha256": hashlib.sha256(file.read_bytes()).hexdigest(), "url": url}, dependencies

pending = [("SSH.NET", "2026.0.0")]
resolved = {}
while pending:
    wave = [item for item in pending if item[0].lower() not in resolved]
    pending = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
        for record, dependencies in pool.map(fetch, wave):
            resolved[record["id"].lower()] = record
            pending.extend(d for d in dependencies if d[0].lower() not in resolved)
            print(record["id"] + " " + record["version"], flush=True)
    unique = {}
    for name, version in pending:
        key = name.lower()
        if key not in unique or tuple(map(int, version.split("."))) > tuple(map(int, unique[key][1].split("."))):
            unique[key] = (name, version)
    pending = list(unique.values())
(PACKAGES / "dependencies.lock.json").write_text(json.dumps(list(resolved.values()), indent=2), encoding="utf-8")
print("Dependencias listas: " + str(len(resolved)), flush=True)
