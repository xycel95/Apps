"""Verificación local de protocolos e interfaz; no usa servidores del usuario."""
import errno
import logging
import os
import pathlib
import socket
import struct
import subprocess
import sys
import threading
import time
import tempfile
import shutil
import zipfile
import hashlib

ROOT = pathlib.Path(__file__).resolve().parent
CHECK = ROOT / "verificacion"
CHECK.mkdir(exist_ok=True)
MONO = pathlib.Path(os.environ.get("SSHBACKUP_MONO_ROOT", "/workspace/scratch/e5c16abbe89f/deps/root"))
ENV = dict(os.environ)
ENV.update(MONO_GAC_PREFIX=str(MONO/"usr"), MONO_CFG_DIR=str(MONO/"etc"),
           MONO_PATH=str(MONO/"usr/lib/mono/4.5"),
           LD_LIBRARY_PATH=str(MONO/"usr/lib")+":"+str(MONO/"usr/lib/x86_64-linux-gnu"), TZ="Europe/Madrid")
COMMAND = [str(MONO/"usr/bin/mono"), "--config", str(MONO/"etc/mono/config"), str(ROOT/"build/Pruebas.exe")]

def run_check(arguments, name, env=ENV):
    with (CHECK/(name+".log")).open("w") as log:
        result = subprocess.run(COMMAND + arguments, env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=600)
    text = (CHECK/(name+".log")).read_text(errors="replace")
    print(text, flush=True)
    if result.returncode:
        raise RuntimeError(name + ": salida " + str(result.returncode))

def gui():
    display = 87
    auth = CHECK/"Xauthority"
    cookie = os.urandom(16)
    def item(value): return struct.pack(">H",len(value))+value
    auth.write_bytes(struct.pack(">H",65535)+item(b"")+item(str(display).encode())+item(b"MIT-MAGIC-COOKIE-1")+item(cookie))
    auth.chmod(0o600)
    env = dict(ENV, DISPLAY="127.0.0.1:"+str(display), XAUTHORITY=str(auth))
    tool_dir=None
    for number in range(100):
        candidate=pathlib.Path("/tmp/q"+str(number).zfill(1))
        if len(str(candidate))==7:
            try:
                candidate.mkdir(mode=0o700);tool_dir=candidate;break
            except FileExistsError: pass
    if tool_dir is None: raise RuntimeError("No se pudo preparar el directorio privado de verificación gráfica.")
    (tool_dir/"xkbcomp").symlink_to(MONO/"usr/bin/xkbcomp")
    binary=(MONO/"usr/bin/Xvfb-relocated").read_bytes().replace(b"/tmp/e5",str(tool_dir).encode())
    server=CHECK/"Xvfb-verificacion"
    server.write_bytes(binary);server.chmod(0o700)
    with (CHECK/"xvfb.log").open("w") as log:
        process = subprocess.Popen([str(server), ":"+str(display), "-screen", "0", "1600x1100x24",
                                    "-listen", "tcp", "-nolisten", "unix", "-auth", str(auth)], env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            time.sleep(1)
            if process.poll() is not None:
                raise RuntimeError((CHECK/"xvfb.log").read_text())
            run_check(["gui",str(CHECK/"gui")],"interfaz",env)
            app_env=dict(env,SSHBACKUP_DATA_DIR=str(CHECK/"arranque-limpio"))
            app=subprocess.Popen(COMMAND[:-1]+[str(ROOT/"entrega/SSHBackup.exe")],env=app_env,cwd=CHECK,
                                 stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
            try:
                time.sleep(2)
                if app.poll() is not None: raise RuntimeError("El EXE no mantuvo abierta su ventana.")
                from PIL import ImageGrab
                previous_auth=os.environ.get("XAUTHORITY")
                os.environ["XAUTHORITY"]=str(auth)
                try: ImageGrab.grab(xdisplay=env["DISPLAY"]).save(CHECK/"arranque-exe.png")
                finally:
                    if previous_auth is None: os.environ.pop("XAUTHORITY",None)
                    else: os.environ["XAUTHORITY"]=previous_auth
                print("EXE autónomo: ventana abierta sin DLL externas en su carpeta.",flush=True)
            finally:
                app.terminate()
                try: app.wait(timeout=5)
                except subprocess.TimeoutExpired: app.kill()
        finally:
            process.terminate()
            try: process.wait(timeout=5)
            except subprocess.TimeoutExpired: process.kill()
            (tool_dir/"xkbcomp").unlink(missing_ok=True)
            tool_dir.rmdir()

def ssh(contents=False):
    sys.path.insert(0,"/tmp/sshbackup-test-deps-e842")
    import paramiko
    paramiko.util.log_to_file(str(CHECK/"ssh-servidor-debug.log"),level=logging.DEBUG)
    fixture = CHECK/("origen-contenido-windows" if contents else "origen-ssh")
    destination = CHECK/("copias-contenido-windows" if contents else "copias-ssh")
    remote_root = "/D:/Copias de prueba"
    for owned in (fixture,destination):
        if owned.exists(): shutil.rmtree(owned)
    fixture.mkdir(exist_ok=True)
    if contents:
        for name in ["Copia 01.zip","Copia_Ñ_02.ZIP","archivo con espacios.zip","respaldo.2026-10-17.zip","copia-final.zip"]:
            with zipfile.ZipFile(fixture/name,"w",compression=zipfile.ZIP_DEFLATED) as z:
                z.writestr("documentos/informe.txt","Original: "+name)
                z.writestr("datos.bin",os.urandom(2048))
        sub=fixture/"Datos y documentos";sub.mkdir()
        (sub/"subcarpeta vacía").mkdir()
        (sub/"leeme.txt").write_text("Córdoba: carpeta incluida íntegramente.",encoding="utf-8")
        (sub/"datos.bin").write_bytes(os.urandom(4096))
        with zipfile.ZipFile(sub/"anidado.zip","w") as z: z.writestr("interior.txt","ZIP anidado original")
    else:
        (fixture/"carpeta vacía").mkdir(exist_ok=True)
        (fixture/"documentos").mkdir(exist_ok=True)
        (fixture/"niño.txt").write_text("Córdoba y Madrid. Copia verificada.\n",encoding="utf-8")
        (fixture/"documentos/datos.bin").write_bytes(os.urandom(131072 if os.environ.get("SSHBACKUP_TEST_SMALL") else 2*1024*1024))
        (fixture/"vacio.txt").write_bytes(b"")
        (fixture/"omitir.tmp").write_text("excluir")
    mutations=[]
    directory_reads=[]
    def snapshot():
        result={}
        for path in [fixture]+sorted(fixture.rglob("*")):
            stat=path.stat()
            result[str(path.relative_to(fixture))]=(stat.st_mode,stat.st_size if path.is_file() else 0,stat.st_mtime_ns,
                hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None)
        return result
    before=snapshot()
    host_key = paramiko.RSAKey.generate(2048)
    user_key = paramiko.RSAKey.generate(2048)
    key_file = CHECK/("clave-usuario-windows.pem" if contents else "clave-usuario.pem")
    user_key.write_private_key_file(str(key_file))
    key_file.chmod(0o600)
    secret = "Solo-para-verificacion-SSH-e842!"
    class Auth(paramiko.ServerInterface):
        def check_auth_password(self,username,password):
            return paramiko.AUTH_SUCCESSFUL if username=="backup" and password==secret else paramiko.AUTH_FAILED
        def check_auth_publickey(self,username,key):
            matches=key.get_base64()==user_key.get_base64()
            print("Comprobación de clave pública:",username,key.get_name(),matches,flush=True)
            return paramiko.AUTH_SUCCESSFUL if username=="backup" and matches else paramiko.AUTH_FAILED
        def get_allowed_auths(self,username): return "password,publickey"
        def check_channel_request(self,kind,chanid):
            return paramiko.OPEN_SUCCEEDED if kind=="session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED
    class Handle(paramiko.SFTPHandle):
        def read(self,offset,length):
            time.sleep(.001)
            return super().read(offset,length)
        def stat(self):
            try: return paramiko.SFTPAttributes.from_stat(os.fstat(self.readfile.fileno()))
            except OSError as error: return paramiko.SFTPServer.convert_errno(error.errno)
    class ReadOnlySFTP(paramiko.SFTPServerInterface):
        def local(self,path,follow=True):
            if contents:
                remote=path.replace("\\","/")
                if remote in (".",""): remote=remote_root
                if not remote.startswith("/"): remote="/"+remote
                if remote!=remote_root and not remote.startswith(remote_root+"/"):
                    raise FileNotFoundError(errno.ENOENT,"Ruta Windows inexistente: "+path)
                suffix=remote[len(remote_root):].lstrip("/")
                path=str(fixture/suffix)
            resolved=os.path.abspath(path if path.startswith("/") else str(fixture/path))
            if os.path.commonpath([str(fixture),resolved])!=str(fixture):
                raise PermissionError(errno.EACCES,"Fuera de la carpeta de verificación")
            if follow and os.path.commonpath([str(fixture),os.path.realpath(resolved)])!=str(fixture):
                raise PermissionError(errno.EACCES,"Enlace fuera de la carpeta de verificación")
            return resolved
        def canonicalize(self,path):
            if contents:
                # Some Windows SFTP services return drive paths without the leading slash.
                if path in (".",""): return remote_root.lstrip("/")
                return path.replace("\\","/").lstrip("/")
            try: return self.local(path)
            except OSError: return str(fixture)
        def stat(self,path):
            try: return paramiko.SFTPAttributes.from_stat(os.stat(self.local(path)))
            except OSError as error: return paramiko.SFTPServer.convert_errno(error.errno)
        def lstat(self,path):
            try: return paramiko.SFTPAttributes.from_stat(os.lstat(self.local(path,False)))
            except OSError as error: return paramiko.SFTPServer.convert_errno(error.errno)
        def list_folder(self,path):
            try:
                result=[]
                for file in os.scandir(self.local(path)):
                    attrs=paramiko.SFTPAttributes.from_stat(file.stat(follow_symlinks=False))
                    attrs.filename=file.name
                    result.append(attrs)
                directory_reads.append((path, sorted(a.filename for a in result)))
                return result
            except OSError as error: return paramiko.SFTPServer.convert_errno(error.errno)
        def open(self,path,flags,attr):
            if flags & (os.O_WRONLY|os.O_RDWR|os.O_CREAT|os.O_TRUNC):
                mutations.append(("open-write",path))
                return paramiko.SFTP_PERMISSION_DENIED
            try:
                handle=Handle(flags)
                handle.readfile=open(self.local(path),"rb")
                return handle
            except OSError as error: return paramiko.SFTPServer.convert_errno(error.errno)
        def remove(self,path):
            mutations.append(("remove",path));return paramiko.SFTP_PERMISSION_DENIED
        def rename(self,oldpath,newpath):
            mutations.append(("rename",oldpath,newpath));return paramiko.SFTP_PERMISSION_DENIED
        def mkdir(self,path,attr):
            mutations.append(("mkdir",path));return paramiko.SFTP_PERMISSION_DENIED
        def rmdir(self,path):
            mutations.append(("rmdir",path));return paramiko.SFTP_PERMISSION_DENIED
        def chattr(self,path,attr):
            mutations.append(("chattr",path));return paramiko.SFTP_PERMISSION_DENIED
        def symlink(self,target_path,path):
            mutations.append(("symlink",target_path,path));return paramiko.SFTP_PERMISSION_DENIED
    sock=socket.socket()
    sock.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1)
    sock.bind(("127.0.0.1",0));sock.listen(30);sock.settimeout(.5)
    port=sock.getsockname()[1]
    stopped=threading.Event()
    transports=[]
    def serve():
        while not stopped.is_set():
            try: channel,_=sock.accept()
            except socket.timeout: continue
            except OSError: break
            def connection(channel=channel):
                try:
                    channel.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1)
                    transport=paramiko.Transport(channel);transports.append(transport)
                    transport.add_server_key(host_key)
                    transport.set_subsystem_handler("sftp",paramiko.SFTPServer,ReadOnlySFTP)
                    transport.start_server(server=Auth())
                    while transport.is_active() and not stopped.wait(.1): pass
                except Exception: pass
            threading.Thread(target=connection,daemon=True).start()
    thread=threading.Thread(target=serve,daemon=True);thread.start()
    print("Servidor SSH local preparado en 127.0.0.1:"+str(port),flush=True)
    try:
        run_check(["contents" if contents else "ssh","127.0.0.1",str(port),"backup",secret,
                   r"D:\Copias de prueba" if contents else str(fixture),str(destination),str(key_file),str(fixture)],
                  "contenido-windows" if contents else "ssh")
        if contents:
            assert snapshot()==before,"El origen cambió durante la copia."
            assert not mutations,"Solicitudes de modificación remota: "+repr(mutations)
            print("SOLO LECTURA: mismos nombres, contenido, tamaños, fechas y permisos; cero solicitudes de escritura, borrado o renombrado.",flush=True)
    finally:
        import json
        (CHECK/"listados-sftp-prueba.json").write_text(json.dumps(directory_reads,ensure_ascii=False,indent=2))
        stopped.set();sock.close()
        for transport in transports:
            try: transport.close()
            except Exception: pass
        key_file.unlink(missing_ok=True)

if __name__=="__main__":
    if sys.argv[1]=="gui": gui()
    elif sys.argv[1]=="ssh": ssh()
    elif sys.argv[1]=="contents": ssh(True)
    elif sys.argv[1]=="export":
        target=CHECK/"exportacion"
        if target.exists(): shutil.rmtree(target)
        run_check(["export",str(target)],"exportacion")
    elif sys.argv[1]=="unit":
        target=CHECK/"unit-final"
        if target.exists(): shutil.rmtree(target)
        run_check(["unit",str(target)],"unit-final")
    else: raise SystemExit("Usa unit, ssh o gui.")
