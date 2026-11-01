# Publicar una actualización de Guarda

El canal integrado comienza en **1.1.0**. Las versiones anteriores requieren una primera descarga manual.

1. Leer las instrucciones y el contexto vigentes; confirmar el último commit de `guarda`.
2. Actualizar las versiones de ensamblado, manifiesto y documentación; compilar. Con Mono, `build.py` incorpora y verifica el manifiesto mediante `winres.py`.
3. Ejecutar las pruebas del flujo afectado. Comprobar el ejecutable y su versión antes de sustituir una descarga.
4. Recuperar la clave privada del archivo privado **Guarda_Clave_de_Firma_PRIVADA.zip**, conservado fuera de GitHub. Extraerla fuera del repositorio. No publicarla ni regenerarla. La clave pública compatible está en `Codigo/update-public-key.xml` y `actualizaciones/public-key.xml`.
5. Con Python 3.9+ y `cryptography` en el entorno de desarrollo, ejecutar:

```text
python Codigo/firmar_actualizacion.py --key RUTA_PRIVADA_FUERA_DEL_REPOSITORIO/private.pem --exe descargas/Guarda.exe --version X.Y.Z --notes notas-version.txt --output actualizaciones/stable.xml
```

6. Guardar el mismo ejecutable en `descargas/versiones/X.Y.Z/Guarda.exe`. Esa ruta concreta queda vinculada por la firma y no debe reutilizarse para otros bytes.
7. Preparar el ZIP completo, `descargas/SHA256SUMS.txt`, `app.json`, guía, notas de parche y contexto. Conservar las notas y los paquetes históricos.
8. Publicar los binarios y el canal firmado en el mismo commit, usando revisión esperada para conservar cambios ajenos.
9. Recuperar desde GitHub el canal, la firma y ambos binarios. Comprobar firma, longitud, SHA-256, versión y ZIP. El mismo canal debe indicar una actualización desde una versión anterior y «al día» para la publicada.

La firma se calcula sobre los bytes UTF-8 exactos de `stable.xml`, con RSA-3072, SHA-256 y PKCS#1 v1.5. `stable.xml.sig` contiene la firma en base64. No reformatear el XML después de firmarlo.

La app solo admite descargas HTTPS de la ruta oficial `raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/versiones/<versión>/Guarda.exe`. No se instala una versión anterior o igual. Una nueva entrega debe conservar el protocolo de instalación y la clave pública de los clientes existentes.

La clave de Guarda es independiente de SSH Backup. No modificar ni reutilizar claves privadas de otras aplicaciones.
