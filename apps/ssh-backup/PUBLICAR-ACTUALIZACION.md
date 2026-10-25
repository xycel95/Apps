# Publicar una actualización de SSH Backup

1. Abrir la rama ssh-backup y leer AGENTS.md y CONTEXTO.md de la aplicación.
2. Preparar el cambio de la aplicación y comprobar el flujo afectado. Conservar las claves públicas y las licencias.
3. Preparar el paquete de entrega sin claves privadas ni datos reales, comprobar su versión, integridad y firma, y colocarlo en descargas/.
4. Añadir las notas de la versión arriba de notas-de-parche/README.md, sin borrar las notas anteriores.
5. Actualizar app.json: versión, fecha, nombre y ruta del paquete, URL y SHA-256 comprobados. Mantener download.available en false mientras no haya una descarga válida.
6. Actualizar la descripción o guía si cambia su uso; conservar los datos históricos y sus límites en CONTEXTO.md.
7. Guardar juntos los archivos relacionados, leer la revisión remota antes de publicar y conservar cambios ajenos.
8. Comprobar que el ZIP publicado se puede descargar y que coincide con el hash previsto antes de retirar un archivo anterior o dar por terminada la publicación.
9. Actualizar el índice de main si cambia la información del catálogo.

La rama de la aplicación, el índice general y la web son piezas diferentes. El asistente que gestione la web debe leer estos metadatos y aplicar la ficha o las novedades en su propio repositorio cuando el propietario autorice publicarlas allí. El actualizador integrado necesita su propio manifiesto firmado y no queda conectado automáticamente por subir archivos a GitHub.
