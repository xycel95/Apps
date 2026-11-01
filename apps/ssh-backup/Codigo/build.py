"""Compila un único EXE Windows .NET Framework y los ensayos de regresión."""
import os,pathlib,subprocess
from winres import verify_manifest
ROOT=pathlib.Path(__file__).resolve().parent
RUNTIME=pathlib.Path(os.environ.get('SSHBACKUP_MONO_ROOT',str(ROOT.parents[1]/'runtime-root')))
REFS=pathlib.Path(os.environ.get('SSHBACKUP_NET48_REFS',str(RUNTIME/'usr/lib/mono/4.5')))
if not (REFS/'RedistList/FrameworkList.xml').is_file():
 raise RuntimeError('SSHBACKUP_NET48_REFS debe apuntar a las referencias oficiales de .NET Framework 4.8; no uses las DLL de ejecución de Mono para compilar la entrega Windows.')
ENV=dict(os.environ,MONO_PATH=str(RUNTIME/'usr/lib/mono/4.5'),MONO_GAC_PREFIX=str(RUNTIME/'usr'),MONO_CFG_DIR=str(RUNTIME/'etc'),LD_LIBRARY_PATH=str(RUNTIME/'usr/lib')+':'+str(RUNTIME/'usr/lib/x86_64-linux-gnu'),TZ='Europe/Madrid')
for name in ('entrega','build'): (ROOT/name).mkdir(exist_ok=True)
DOTNET=pathlib.Path(os.environ['SSHBACKUP_DOTNET_ROOT'])
CSC=sorted((DOTNET/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
ENV['DOTNET_ROOT']=str(DOTNET)
base=[str(DOTNET/'dotnet'),str(CSC),'-nologo','-noconfig','-nostdlib+','-langversion:7.3','-platform:anycpu','-optimize+','-deterministic+','-debug-','-pathmap:'+str(ROOT)+'=/_/SSHBackup']
for name in ('mscorlib','System','System.Core','System.Xml','System.Security','System.IO.Compression','System.Windows.Forms','System.Drawing'):
 base.append('-r:'+str(REFS/(name+'.dll')))
base.append('-r:'+str(ROOT/'deps/lib/Renci.SshNet.dll'))
sources=[str(p) for p in sorted(ROOT.glob('*.cs')) if not p.name.startswith('Tests')]
resources=['-resource:'+str(p)+',deps.'+p.name for p in sorted((ROOT/'deps/lib').glob('*.dll'))]
for p in [ROOT/'GUIA.txt',ROOT/'LICENCIAS.txt',ROOT/'assets/app.ico',ROOT/'assets/Plantilla_Servidores_SSH.xlsx',ROOT/'assets/Plantilla_Exportacion_SSH.xlsx']:
 resources.append('-resource:'+str(p)+',assets.'+p.name)
subprocess.run(base+['-target:winexe','-win32manifest:'+str(ROOT/'app.manifest'),'-win32icon:'+str(ROOT/'assets/app.ico'),'-out:'+str(ROOT/'entrega/SSHBackup.exe')]+resources+sources,env=ENV,check=True)
verify_manifest(ROOT/'entrega/SSHBackup.exe',ROOT/'app.manifest')
for filename,main,out in [('TestsUpdates.cs','SSHBackup.TestsUpdates','PruebasUpdates.exe'),('Tests.cs','SSHBackup.Tests','Pruebas.exe'),('Tests2.cs','SSHBackup.Tests2','Pruebas2.exe')]:
 if (ROOT/filename).exists(): subprocess.run(base+['-target:exe','-main:'+main,'-out:'+str(ROOT/'build'/out)]+resources+sources+[str(ROOT/filename)],env=ENV,check=True)
print('EXE compilado:',ROOT/'entrega/SSHBackup.exe')
