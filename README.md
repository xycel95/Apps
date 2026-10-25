# Apps de XYCEL

Repositorio público para gestionar los programas de XYCEL. **Una rama permanente por aplicación**: cada programa reúne su código, descripción, guía, descargas y notas de parche en su propia rama.

## Aplicaciones

| Programa | Versión documentada | Rama | Descripción y uso | Notas de parche | Descarga |
| --- | --- | --- | --- | --- | --- |
| [SSH Backup](https://github.com/xycel95/Apps/tree/ssh-backup/apps/ssh-backup) | 2.1.0 | [ssh-backup](https://github.com/xycel95/Apps/tree/ssh-backup) | [Descripción](https://github.com/xycel95/Apps/blob/ssh-backup/apps/ssh-backup/DESCRIPCION.md) · [Guía](https://github.com/xycel95/Apps/blob/ssh-backup/apps/ssh-backup/docs/README.md) | [Historial](https://github.com/xycel95/Apps/blob/ssh-backup/apps/ssh-backup/notas-de-parche/README.md) | [Pendiente del ZIP original](https://github.com/xycel95/Apps/blob/ssh-backup/apps/ssh-backup/descargas/README.md) |

El [catálogo estructurado](catalogo.json) enlaza los metadatos de cada aplicación para facilitar su consulta desde la web u otras conversaciones. El estado concreto de cada descarga se consulta en el app.json de su rama.

## Dentro de cada programa

- **Descripción:** qué hace y para quién sirve.
- **Cómo funciona:** instrucciones de uso y requisitos.
- **Descargas:** el paquete comprobado y sus archivos necesarios.
- **Notas de parche:** lo más reciente arriba, conservando el historial.
- **Código:** fuentes y licencias.
- **Contexto:** lo verificado y lo pendiente para continuar desde otro chat.

main es el índice general. El código de cada aplicación permanece en su rama y en su carpeta apps/<nombre>/; no se fusionan automáticamente todas las aplicaciones en main.

## Cuando haya una actualización

Trabajar en la rama del programa, publicar el paquete verificado, actualizar la versión y enlaces de app.json, y añadir sus notas arriba del historial. Comprobar la descarga antes de sustituir un archivo anterior. Actualizar este catálogo cuando corresponda.

Subir archivos aquí no instala ni ejecuta el programa en ningún servidor. Tampoco cambia automáticamente la web ni configura el canal del actualizador de una aplicación.

## Continuar desde otra conversación

Indica xycel95/Apps y el programa. Lee primero AGENTS.md y, en la rama elegida, el README.md, AGENTS.md y CONTEXTO.md de la aplicación.

Este repositorio es público. No añadir credenciales, claves privadas, configuración real de servidores ni datos de copias. El repositorio privado de la web se mantiene separado.
