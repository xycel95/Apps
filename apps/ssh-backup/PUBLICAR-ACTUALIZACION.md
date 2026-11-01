# Publicar una actualización de SSH Backup

1. Leer `AGENTS.md`, `CONTEXTO.md` y el estado remoto de `ssh-backup`; conservar la clave pública original.
2. Implementar y comprobar el cambio. Coordinar versión del código, guía, notas y metadatos. Compilar con el manifiesto nativo incorporado y verificado.
3. Recuperar en privado `SSHBackup_clave_firma_actualizaciones.pem`. Nunca publicarla, incluirla en un ZIP ni sustituirla por otra.
4. Crear el paquete con `Codigo/crear_actualizacion.py`, indicando `--exe`, `--clave`, `--version`, `--notas`, `--salida` y una `--url` HTTPS fija: `descargas/versiones/<versión>/SSHBackup_<versión>.sshupdate` bajo la rama permanente.
5. Conservar ese paquete y EXE en la carpeta de versión. Copiar el XML firmado que genera el script a `actualizaciones/stable.xml`. La firma está dentro del XML, como en los clientes anteriores; no es el formato de GUARDA.
6. Preparar el ZIP completo sin datos privados ni resultados de prueba; comprobar CRC, firma y hashes. Actualizar `app.json`, descargas, notas y contexto en un commit coherente.
7. Validar en la rama de comprobación y promover a `ssh-backup` con revisión esperada. No reemplazar un archivo de versión ya entregado con otros bytes.
8. Comprobar la descarga pública del ZIP, EXE, paquete y [canal oficial](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/actualizaciones/stable.xml) y sus hashes. `main` enlaza el apartado de descargas sin duplicar la versión.
9. Si el propietario lo solicita, actualizar por separado la ficha y la nota de versión en Web-XyceL y verificar el recibo de WordPress. Enviar comunicaciones solo cuando las haya pedido.

Desde 2.2 el canal oficial se utiliza si el ajuste está vacío. Se conservan los canales personalizados. Desde 2.0/2.1 se puede instalar el paquete o introducir una vez el canal oficial para dar el salto inicial.
