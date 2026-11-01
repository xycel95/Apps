# Descripción del programa

**Guarda** crea copias completas e independientes de una carpeta. Su función principal es copiar tus archivos a otro destino y colocar la fecha al principio del nombre de la nueva carpeta.

## Qué permite hacer

- Elegir carpeta de origen y carpeta de destino, incluidas unidades USB, discos y carpetas de red accesibles.
- Copiar archivos, subcarpetas y carpetas vacías.
- Usar nombres como `2026_11_01_Facturas`, con el formato `aaaa_mm_dd` y la fecha local del ordenador al iniciar la copia.
- Conservar varias copias del mismo día mediante sufijos `_02`, `_03`, etc., sin sobrescribir las anteriores.
- Comprobar el contenido del destino con SHA-256, con la casilla activada por defecto.
- Ver el progreso, cancelar y abrir la copia o el informe al terminar.
- Recordar las últimas opciones utilizadas.

El contenido original no se renombra. Cada ejecución genera una copia nueva; no hace sincronización ni elimina archivos antiguos.

## Requisitos y alcance

Windows 10/11 con .NET Framework 4.8 o 4.8.1. El ejecutable es portable y no necesita permisos de administrador para trabajar con carpetas a las que tu usuario tenga acceso.

Es una copia de carpetas de usuario, no una imagen de recuperación de Windows. No incluye SSH, programación de tareas, copia incremental, restauración automática ni actualizador en línea.

Los enlaces simbólicos, uniones y otros puntos de reanálisis no se siguen: se indican en el informe y esa copia se marca como incompleta. Los archivos abiertos que no puedan leerse o que cambien durante el proceso pueden impedir terminar la copia. Se copia el contenido principal de los archivos; no se reproducen permisos NTFS, flujos alternativos ni cifrado.

## Validación disponible

El motor inicial superó 16 pruebas en Linux/Mono. La ventana de la versión 1.0.1 se abrió y completó una copia real en ese entorno. Aún no se ha probado en un Windows real ni con un pendrive físico. Los detalles están en el [informe de validación](docs/VALIDACION.txt).

[Cómo funciona y guía](docs/README.md) · [Descargar](descargas/README.md) · [Notas de parche](notas-de-parche/README.md).
