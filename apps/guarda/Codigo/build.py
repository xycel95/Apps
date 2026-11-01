"""Compila con Roslyn del SDK .NET. Mono solo se usa para ejecutar pruebas en Linux."""
import os
from pathlib import Path
import subprocess
from winres import verify_manifest

ROOT = Path(__file__).resolve().parent
TOOLCHAIN = Path(os.environ.get('GUARDA_MONO_ROOT', str(ROOT.parents[1] / '.toolchain/root')))
REFS = Path(os.environ.get('GUARDA_FRAMEWORK_REFS', str(TOOLCHAIN / 'usr/lib/mono/4.5')))
DOTNET_ROOT = Path(os.environ.get('GUARDA_DOTNET_ROOT', str(ROOT.parents[1] / '.toolchain-dotnet/root/usr/lib/dotnet')))
_compilers = sorted((DOTNET_ROOT / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
_csc_path = os.environ.get('GUARDA_CSC_DLL') or (str(_compilers[-1]) if _compilers else None)
if not _csc_path:
    raise RuntimeError('Configura GUARDA_DOTNET_ROOT o GUARDA_CSC_DLL con el SDK y el compilador Roslyn.')
CSC = Path(_csc_path)
MONO = str(TOOLCHAIN / 'usr/bin/mono-sgen')
ENV = dict(os.environ, DOTNET_ROOT=str(DOTNET_ROOT), MONO_PATH=str(REFS), MONO_GAC_PREFIX=str(TOOLCHAIN / 'usr'), MONO_CFG_DIR=str(TOOLCHAIN / 'etc'), LD_LIBRARY_PATH=str(TOOLCHAIN / 'usr/lib') + ':' + str(TOOLCHAIN / 'usr/lib/x86_64-linux-gnu'))
BASE = [str(DOTNET_ROOT / 'dotnet'), str(CSC), '/nologo', '/noconfig', '/nostdlib+', '/langversion:5', '/platform:anycpu', '/optimize+', '/deterministic+', '/debug-', '/pathmap:' + str(ROOT) + '=/_/Guarda']
BASE += ['/r:' + str(REFS / (name + '.dll')) for name in ('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Xml')]


def compile_app():
    out = ROOT.parent / 'entrega'; out.mkdir(exist_ok=True)
    sources = [str(p) for p in sorted(ROOT.glob('*.cs'))]
    public_key = '/resource:' + str(ROOT / 'update-public-key.xml') + ',Guarda.UpdatePublicKey'
    subprocess.run(BASE + ['/target:winexe', public_key, '/win32manifest:' + str(ROOT / 'app.manifest'), '/win32icon:' + str(ROOT / 'app.ico'), '/out:' + str(out / 'Guarda.exe')] + sources, env=ENV, check=True)
    verify_manifest(out / 'Guarda.exe', ROOT / 'app.manifest')
    tests = ROOT.parent / 'pruebas'
    for name, files, main in [('Tests', ['BackupEngine.cs'], 'Guarda.Tests'), ('GuiSmoke', [p.name for p in ROOT.glob('*.cs')], 'Guarda.GuiSmoke'), ('UpdateTests', ['Updates.cs'], 'Guarda.UpdateTests')]:
        if (tests / (name + '.cs')).exists():
            subprocess.run(BASE + [public_key, '/target:exe', '/main:' + main, '/out:' + str(tests / (name + '.exe'))] + [str(ROOT / p) for p in files] + [str(tests / (name + '.cs'))], env=ENV, check=True)
    print('Compilado con Roslyn:', out / 'Guarda.exe')


if __name__ == '__main__': compile_app()
