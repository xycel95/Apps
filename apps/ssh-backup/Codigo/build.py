"""Compila un único EXE Windows .NET Framework y los ensayos de regresión."""
import os,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parent
RUNTIME=pathlib.Path(os.environ.get('SSHBACKUP_MONO_ROOT',str(ROOT.parents[1]/'runtime-root')))
REFS=pathlib.Path(os.environ.get('SSHBACKUP_NET48_REFS',str(RUNTIME/'usr/lib/mono/4.5')))
ENV=dict(os.environ,MONO_PATH=str(RUNTIME/'usr/lib/mono/4.5'),MONO_GAC_PREFIX=str(RUNTIME/'usr'),MONO_CFG_DIR=str(RUNTIME/'etc'),LD_LIBRARY_PATH=str(RUNTIME/'usr/lib')+':'+str(RUNTIME/'usr/lib/x86_64-linux-gnu'),TZ='Europe/Madrid')
for name in ('entrega','build'): (ROOT/name).mkdir(exist_ok=True)
base=[str(RUNTIME/'usr/bin/mono'),'--config',str(RUNTIME/'etc/mono/config'),str(RUNTIME/'usr/lib/mono/4.5/mcs.exe'),'-noconfig','-nostdlib','-langversion:7','-platform:anycpu','-optimize+']
for name in ('mscorlib','System','System.Core','System.Xml','System.Security','System.IO.Compression','System.Windows.Forms','System.Drawing'):
 base.append('-r:'+str(REFS/(name+'.dll')))
base.append('-r:'+str(ROOT/'deps/lib/Renci.SshNet.dll'))
sources=[str(p) for p in sorted(ROOT.glob('*.cs')) if not p.name.startswith('Tests')]
resources=['-resource:'+str(p)+',deps.'+p.name for p in sorted((ROOT/'deps/lib').glob('*.dll'))]
for p in [ROOT/'GUIA.txt',ROOT/'LICENCIAS.txt',ROOT/'assets/app.ico',ROOT/'assets/Plantilla_Servidores_SSH.xlsx',ROOT/'assets/Plantilla_Exportacion_SSH.xlsx']:
 resources.append('-resource:'+str(p)+',assets.'+p.name)
subprocess.run(base+['-target:winexe','-win32manifest:'+str(ROOT/'app.manifest'),'-win32icon:'+str(ROOT/'assets/app.ico'),'-out:'+str(ROOT/'entrega/SSHBackup.exe')]+resources+sources,env=ENV,check=True)
for filename,main,out in [('Tests.cs','SSHBackup.Tests','Pruebas.exe'),('Tests2.cs','SSHBackup.Tests2','Pruebas2.exe')]:
 if (ROOT/filename).exists(): subprocess.run(base+['-target:exe','-main:'+main,'-out:'+str(ROOT/'build'/out)]+resources+sources+[str(ROOT/filename)],env=ENV,check=True)
print('EXE compilado:',ROOT/'entrega/SSHBackup.exe')
