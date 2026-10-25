# Trabajar en SSH Backup

- Responder en español, con explicaciones breves. Leer este archivo, `README.md` y `CONTEXTO.md` de la rama actual antes de modificar la aplicación.
- Mantener el alcance en `apps/ssh-backup/` en la rama `ssh-backup`. Las instrucciones generales del repositorio siguen vigentes. No editar otras aplicaciones al trabajar en esta.
- La rama permanente de esta aplicación es `ssh-backup`. La migración incluye la entrega existente, sin modificarla. `main` es el índice general de todas las aplicaciones; no fusionar automáticamente ramas de aplicaciones en ella.
- Antes de editar, leer la revisión remota actual y conservar cambios ajenos. Usar commits coherentes y actualización con revisión esperada.
- Para cambios funcionales, compilar y comprobar el flujo afectado. Diferenciar pruebas Linux/Mono de pruebas Windows nativas; no afirmar que el Programador de tareas o el arranque nativo se han probado si no hay evidencia.
- Prohibido añadir credenciales, configuración real de servidores, historial del usuario, registros reales, copias de datos, claves SSH o la clave privada de firma. Las contraseñas de ejemplo de los ensayos son datos ficticios. No mostrar secretos en los mensajes ni en salidas de herramientas.
- Conservar la clave pública del actualizador. La clave privada permanece fuera de GitHub; cualquier rotación necesita plan explícito de compatibilidad con los ejecutables ya distribuidos.
- El origen remoto es de solo lectura. No añadir escrituras, borrados, descompresión remota ni comandos remotos para realizar copias.
- La reanudación conserva temporales y solo publica un ZIP tras validar su contenido. Cada copia final, incluida la de novedades, debe seguir siendo completa e independiente.
- Las opciones de novedades, programación y límite de red son opcionales. Respetar la migración de ajustes existentes y las casillas independientes de lunes a domingo.
- No enviar correos, invitar colaboradores ni cambiar la visibilidad del repositorio sin petición del usuario. Subir el proyecto público no configura un canal web del actualizador.
- Antes de sustituir una descarga, comprobar versión, hash y firma del nuevo paquete. Mantener licencias y documentación. No incluir artefactos de pruebas, cachés ni claves en la entrega.
- Actualizar `CONTEXTO.md` al terminar con el estado verificado y lo pendiente, sin copiar conversaciones completas.

## Organización de archivos y actualizaciones

- Mantener DESCRIPCION.md para explicar la aplicación; docs/README.md para el uso; descargas/ para la entrega; notas-de-parche/README.md para el historial y app.json para los metadatos públicos.
- Añadir las versiones nuevas arriba del historial y conservar las notas anteriores. Editar una descripción no crea por sí solo un parche ni una versión.
- Al publicar una actualización real, coordinar su paquete, versión, hash, notas y metadatos en un commit coherente. No anunciar una descarga hasta que exista y se haya comprobado.
- El usuario ya ha pedido expresamente publicar esta aplicación y su entrega original en el repositorio público Apps. La entrega original 2.1.0 ya está publicada con sus hashes verificados; no volver a pedir al propietario el ZIP para continuar.
- Conservar las comprobaciones históricas con su alcance real. No presentar Linux/Mono como una validación en Windows nativo.

- La navegación principal de cada programa utiliza, en este orden: **Descripción del programa**, **Cómo funciona y guía**, **Descargar** y **Notas de parche**. Código, licencias y contexto son documentación complementaria.
