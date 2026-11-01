# SSH Backup: qué hace

SSH Backup crea copias locales de carpetas remotas mediante SSH/SFTP desde Windows. Permite organizar varios servidores con alias y elegir el destino de cada uno.

## Funciones

- Descarga archivos y subcarpetas, incluidos los ZIP originales, sin descomprimirlos en el servidor.
- Conserva el origen remoto en solo lectura.
- Reanuda descargas interrumpidas y verifica el ZIP final.
- Ofrece un modo opcional de novedades; cada copia final sigue siendo completa e independiente.
- Incluye historial, comparación por hashes y búsqueda de nombres y rutas.
- Permite programar copias y limitar la velocidad de descarga SSH de forma opcional.
- Importa y exporta listados CSV/XLSX, admite contraseña o clave SSH y conserva las huellas aceptadas.
- Incluye modo claro/oscuro y actualizaciones firmadas mediante paquetes .sshupdate.

## Requisitos

Windows 10 u 11 y .NET Framework 4.8. El uso normal no requiere Python ni Microsoft Excel.

## Versión documentada

2.2.0, del 1 de noviembre de 2026. El canal oficial está preparado para consultar e instalar desde la aplicación. Se han superado 90 comprobaciones locales Linux/Mono. El informe de validación distingue las pruebas nativas de Windows que confirme Actions de lo que sigue pendiente en el equipo de destino.

La programación requiere que el ordenador esté encendido y que la sesión de Windows esté iniciada.

## Enlaces

- [Cómo utilizarlo](docs/README.md).
- [Descargas y su estado](descargas/README.md).
- [Notas de parche](notas-de-parche/README.md).
- [Código fuente](Codigo/).

Desde 2.2.0, el canal oficial permite comprobar las novedades firmadas, descargar e instalar desde la aplicación con progreso y cancelación.
