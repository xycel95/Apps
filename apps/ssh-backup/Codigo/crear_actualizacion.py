"""Crea paquetes firmados. La clave privada se guarda aparte y NO se distribuye."""
import argparse,base64,hashlib,pathlib,xml.etree.ElementTree as ET,zipfile
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import padding
p=argparse.ArgumentParser();p.add_argument('--exe',type=pathlib.Path,required=True);p.add_argument('--clave',type=pathlib.Path,required=True);p.add_argument('--version',required=True);p.add_argument('--notas',type=pathlib.Path,required=True);p.add_argument('--salida',type=pathlib.Path,required=True);p.add_argument('--url',default='');a=p.parse_args()
exe=a.exe.read_bytes();notes=a.notas.read_text(encoding='utf-8').replace('\r\n','\n').replace('\r','\n');digest=hashlib.sha256(exe).hexdigest()
key=serialization.load_pem_private_key(a.clave.read_bytes(),None)
canonical='\n'.join([a.version,digest,a.url,notes]).encode('utf-8')
values={'Version':a.version,'Sha256':digest,'DownloadUrl':a.url,'Notes':notes,'Signature':base64.b64encode(key.sign(canonical,padding.PKCS1v15(),hashes.SHA256())).decode()}
root=ET.Element('UpdateRelease')
for k,v in values.items():ET.SubElement(root,k).text=v
xml=ET.tostring(root,encoding='utf-8',xml_declaration=True)
a.salida.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(a.salida,'w',zipfile.ZIP_DEFLATED) as z:z.writestr('release.xml',xml);z.writestr('SSHBackup.exe',exe)
a.salida.with_suffix('.xml').write_bytes(xml)
print('Paquete firmado:',a.salida.name,'SHA-256:',digest)
