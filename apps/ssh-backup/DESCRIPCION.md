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

2.1.0, entregada el 23 de octubre de 2026. Las pruebas registradas corresponden a Linux/Mono. Sigue pendiente comprobar Windows nativo, el Programador de tareas y el arranque tras actualizar.

La programación requiere que el ordenador esté encendido y que la sesión de Windows esté iniciada. No hay un canal de actualizaciones online preconfigurado.

## Enlaces

- [Cómo utilizarlo](docs/README.md).
- [Descargas y su estado](descargas/README.md).
- [Notas de parche](notas-de-parche/README.md).
- [Código fuente](Codigo/).
