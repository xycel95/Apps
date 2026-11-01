"""Compila en Linux con un entorno Mono extraído. No se distribuye Mono."""
import os
import pathlib
import subprocess
from winres import write_with_manifest, verify_manifest

ROOT = pathlib.Path(__file__).resolve().parent
TOOLCHAIN = pathlib.Path(os.environ.get('GUARDA_MONO_ROOT', str(ROOT.parents[1] / '.toolchain/root')))
REFS = TOOLCHAIN / 'usr/lib/mono/4.5'
ENV = dict(os.environ, MONO_PATH=str(REFS), MONO_GAC_PREFIX=str(TOOLCHAIN / 'usr'),
           MONO_CFG_DIR=str(TOOLCHAIN / 'etc'),
           LD_LIBRARY_PATH=str(TOOLCHAIN / 'usr/lib') + ':' + str(TOOLCHAIN / 'usr/lib/x86_64-linux-gnu'))
MONO = str(TOOLCHAIN / 'usr/bin/mono-sgen')
BASE = [MONO, '--config', str(TOOLCHAIN / 'etc/mono/config'), str(REFS / 'mcs.exe'),
        '-noconfig', '-nostdlib', '-langversion:5', '-platform:anycpu', '-optimize+']
BASE += ['-r:' + str(REFS / (name + '.dll')) for name in
         ('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Xml')]


def compile_app():
    out = ROOT.parent / 'entrega'
    out.mkdir(exist_ok=True)
    sources = [str(p) for p in sorted(ROOT.glob('*.cs'))]
    public_key = '-resource:' + str(ROOT / 'update-public-key.xml') + ',Guarda.UpdatePublicKey'
    subprocess.run(BASE + ['-target:winexe', public_key,
                          '-win32icon:' + str(ROOT / 'app.ico'), '-out:' + str(out / 'Guarda.exe'),
                          ] + sources, env=ENV, check=True)
    native = ROOT.parent / '.build'
    native.mkdir(exist_ok=True)
    resource_file = native / 'Guarda.win32.res'
    write_with_manifest(out / 'Guarda.exe', ROOT / 'app.manifest', resource_file)
    subprocess.run(BASE + ['-target:winexe', public_key, '-win32res:' + str(resource_file),
                          '-out:' + str(out / 'Guarda.exe'),
                          ] + sources, env=ENV, check=True)
    verify_manifest(out / 'Guarda.exe', ROOT / 'app.manifest')
    tests = ROOT.parent / 'pruebas'
    for name, sources, main in [
        ('Tests', ['BackupEngine.cs'], 'Guarda.Tests'),
        ('GuiSmoke', [p.name for p in sorted(ROOT.glob('*.cs'))], 'Guarda.GuiSmoke'),
        ('UpdateTests', ['Updates.cs'], 'Guarda.UpdateTests')
    ]:
        if (tests / (name + '.cs')).exists():
            subprocess.run(BASE + [public_key, '-target:exe', '-main:' + main, '-out:' + str(tests / (name + '.exe'))]
                           + [str(ROOT / s) for s in sources] + [str(tests / (name + '.cs'))], env=ENV, check=True)
    print('Compilado:', out / 'Guarda.exe')


if __name__ == '__main__':
    compile_app()
