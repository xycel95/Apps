"""Ensayos reproducibles; solo usa un servidor SFTP local de solo lectura."""
import os,sys,pathlib,subprocess,zipfile,hashlib,base64,xml.etree.ElementTree as ET,importlib.util,time,tempfile,shutil
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import padding
ROOT=pathlib.Path(__file__).resolve().parent
RUNTIME=ROOT.parents[1]/'runtime-root'
os.environ['SSHBACKUP_MONO_ROOT']=str(RUNTIME)
sys.path.append(str(RUNTIME/'usr/lib/python3/dist-packages'))
spec=importlib.util.spec_from_file_location('verify_old',ROOT/'verify.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)

def packages():
 folder=ROOT/'verificacion/paquetes-prueba';folder.mkdir(parents=True,exist_ok=True)
 key=serialization.load_pem_private_key((ROOT.parent/'update-signing-key.pem').read_bytes(),None)
 payload=(ROOT/'entrega/SSHBackup.exe').read_bytes()
 shutil.copy2(ROOT.parent/'SSHBackup.exe',folder/'old.exe')
 data=dict(Version='2.1.0',Sha256=hashlib.sha256(payload).hexdigest(),DownloadUrl='',Notes='Ensayo firmado. No es una versión publicada.')
 canonical='\n'.join([data['Version'],data['Sha256'],data['DownloadUrl'],data['Notes']]).encode()
 data['Signature']=base64.b64encode(key.sign(canonical,padding.PKCS1v15(),hashes.SHA256())).decode()
 for name in ['valid','tampered','bad-signature','traversal']:
  doc=ET.Element('UpdateRelease')
  for k,value in data.items(): ET.SubElement(doc,k).text=value if name!='bad-signature' or k!='Signature' else 'AA=='
  with zipfile.ZipFile(folder/(name+'.sshupdate'),'w') as z:
   z.writestr('release.xml',ET.tostring(doc,encoding='utf-8',xml_declaration=True))
   z.writestr('SSHBackup.exe',payload if name!='tampered' else payload+b'corrupto')
   if name=='traversal':z.writestr('../escape.exe',payload)
 return folder

def gui(new=True):
 env=dict(v.ENV,DISPLAY='127.0.0.1:95')
 folder=ROOT/'verificacion/gui2';folder.mkdir(parents=True,exist_ok=True)
 target=folder/(('new-' if new else 'regresion-')+str(time.monotonic_ns()))
 if target.exists():shutil.rmtree(target)
 for num in range(10,99):
  bindir=pathlib.Path('/tmp/x'+str(num))
  try:bindir.mkdir(mode=0o700);break
  except FileExistsError:pass
 (bindir/'xkbcomp').symlink_to(RUNTIME/'usr/bin/xkbcomp')
 binary=folder/'Xvfb';binary.write_bytes((RUNTIME/'usr/bin/Xvfb').read_bytes().replace(b'/usr/bin\x00',str(bindir).encode()+b'\x00'));binary.chmod(0o700)
 with (folder/'xvfb.log').open('w') as out:
  p=subprocess.Popen([str(binary),':95','-screen','0','1600x1100x24','-listen','tcp','-nolisten','unix','-ac','-nolock'],env=env,stdout=out,stderr=subprocess.STDOUT)
  try:
   time.sleep(1)
   if p.poll() is not None:raise RuntimeError((folder/'xvfb.log').read_text())
   v.run_check(['gui2' if new else 'gui',str(target)],'interfaz2' if new else 'interfaz-anterior',env)
   if new:
    app_env=dict(env,SSHBACKUP_DATA_DIR=str(target/'arranque-exe'))
    app=subprocess.Popen(v.COMMAND[:-1]+[str(ROOT/'entrega/SSHBackup.exe')],env=app_env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    try:
     time.sleep(2)
     if app.poll() is not None:raise RuntimeError('El EXE no mantuvo abierta la ventana: '+app.communicate()[0].decode(errors='replace'))
     print('EXE autónomo: arranque de la aplicación real correcto, sin DLL externas.',flush=True)
    finally:
     app.terminate();app.wait(timeout=10)
  finally:
   p.terminate();p.wait(timeout=10);(bindir/'xkbcomp').unlink();bindir.rmdir()

if __name__=='__main__':
 mode=sys.argv[1]
 if mode=='schedule':
  v.COMMAND[-1]=str(ROOT/'build/Pruebas2.exe');v.run_check(['schedule',str(ROOT/('verificacion/programacion-dias-'+str(time.monotonic_ns())))],'programacion-dias')
 elif mode=='bandwidth':
  v.COMMAND[-1]=str(ROOT/'build/Pruebas2.exe');v.run_check(['bandwidth'],'limite-red')
 elif mode=='unit':
  folder=packages();v.COMMAND[-1]=str(ROOT/'build/Pruebas2.exe');v.run_check(['unit2',str(ROOT/'verificacion/unit2'),str(folder)],'unit2')
 elif mode in ('gui','gui-old'):
  if mode=='gui':v.COMMAND[-1]=str(ROOT/'build/Pruebas2.exe')
  gui(mode=='gui')
 elif mode in ('ssh','contents','ssh2'):
  v.CHECK=ROOT/('verificacion/regresion-'+mode);v.CHECK.mkdir(exist_ok=True)
  if mode=='ssh2':
   os.environ['SSHBACKUP_TEST_SMALL']='1';v.COMMAND[-1]=str(ROOT/'build/Pruebas2.exe');v.CHECK=ROOT/('verificacion/ssh2-'+str(time.monotonic_ns()));v.CHECK.mkdir(exist_ok=True)
   oldrun=v.run_check
   def newrun(args,name,env=v.ENV):args[0]='ssh2';return oldrun(args,'ssh2',env)
   v.run_check=newrun
  v.ssh(mode=='contents')
 else:raise SystemExit('unit, ssh, contents, ssh2, gui, gui-old')
