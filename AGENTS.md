# Trabajar en Apps

## Organización del repositorio

- Responder en español, de forma clara y breve.
- Este repositorio público reúne varias aplicaciones. `main` contiene el índice y las normas generales; cada aplicación tiene una rama permanente propia y una carpeta `apps/<nombre>/`.
- Antes de actuar, identificar la aplicación y leer las instrucciones y el estado actuales de su rama. No asumir que el historial de otra conversación sigue vigente ni que todas las ramas contienen las mismas aplicaciones.
- Mantener el código, documentación y descargas de una aplicación en su rama. No fusionar automáticamente esas ramas en `main` ni modificar otra aplicación por una petición de este proyecto.
- Actualizar la portada de `main` cuando se incorpore una aplicación real, con enlaces comprobados a su rama y archivos. No inventar proyectos, versiones ni enlaces de descarga.

## Cambios y comprobaciones

- Leer la revisión remota actual antes de escribir. Conservar cambios ajenos y usar commits coherentes y actualización con revisión esperada.
- Compilar y comprobar el flujo afectado por cambios de código. Reutilizar verificaciones vigentes cuando los bytes no cambien; no afirmar pruebas que no se hayan realizado.
- Mantener instrucciones para continuar cada aplicación desde otra conversación y registrar lo verificado y lo pendiente. No copiar conversaciones completas.
- Antes de sustituir una descarga, comprobar versión e integridad. Conservar las licencias de las dependencias y las notas de los cambios reales.
- No enviar correos, invitar colaboradores, crear automatizaciones, cambiar visibilidad ni modificar servicios externos salvo que el usuario lo pida.

## Publicación y datos privados

- El contenido del repositorio y sus ramas es público. Nunca añadir credenciales, claves privadas, tokens, datos personales, configuración real de servidores, copias de datos ni registros reales.
- Las claves privadas de firma de actualizaciones se guardan fuera de GitHub. Conservar la compatibilidad de sus claves públicas; no rotarlas sin un plan explícito.
- No incorporar archivos del repositorio privado de la web ni de servicios ajenos. Este repositorio contiene únicamente las aplicaciones que el usuario pida publicar.
- La publicación de código y archivos no configura por sí sola la actualización automática de una aplicación ni autoriza ejecutarla en los equipos o servidores del usuario.

## Catálogo y organización de cada programa

- Mantener README.md como índice humano y catalogo.json como índice de metadatos. Las entradas enlazan el app.json de la rama de cada programa; no inventar disponibilidad de descargas.
- Cada aplicación reúne README.md, DESCRIPCION.md, docs/, descargas/, notas-de-parche/, Codigo/, CONTEXTO.md, AGENTS.md y app.json dentro de apps/<nombre>/.
- Las notas nuevas se añaden arriba y las anteriores se conservan. Una actualización coordina paquete comprobado, versión, hash, enlaces y notas. Corregir una descripción no crea una versión nueva.
- Mantener el código de la aplicación en su rama y reflejar sus enlaces en el índice de main. El repositorio privado de la web y el canal del actualizador requieren su configuración propia; no afirmar que se actualizan por el mero hecho de publicar aquí.
