# SSH Backup

Aplicación de escritorio para Windows 10 y 11 que crea copias locales por SSH/SFTP, con varios servidores, alias, progreso e historial. Versión migrada: **2.1.0**, entregada el 23 de octubre de 2026. Requiere .NET Framework 4.8 para ejecutarse.

## Apartados

| Apartado | Contenido |
| --- | --- |
| [Descripción](DESCRIPCION.md) | Para qué sirve y sus requisitos |
| [Cómo funciona](docs/README.md) | Primeros pasos y guía detallada |
| [Descargas](descargas/README.md) | Paquete instalable, integridad y disponibilidad |
| [Notas de parche](notas-de-parche/README.md) | Cambios de cada versión, con lo más reciente arriba |
| [Código](Codigo/) | Fuentes y herramientas de compilación |
| [Contexto](CONTEXTO.md) | Estado y pendientes para continuar desde otro chat |

El archivo [app.json](app.json) reúne la versión, los enlaces y el estado de la descarga para que la web o una conversación futura puedan consultarlos.

Este directorio contiene el proyecto de la aplicación. Para continuarlo desde otra conversación, leer primero [AGENTS.md](AGENTS.md) y [CONTEXTO.md](CONTEXTO.md) desde esta misma rama, además de las instrucciones generales del repositorio Apps.

## Estado de publicación

El código y la documentación en texto están publicados y organizados. **Falta incorporar el ZIP original de la entrega 2.1.0**, con el ejecutable, dependencias y documentos de la distribución. Mientras falte, el apartado de descargas y app.json lo indican como no disponible.

## Documentación y actualización

- [Guía de uso en texto](Codigo/GUIA.txt).
- [Historial de notas de parche](notas-de-parche/README.md).
- [Notas originales de la entrega en texto](Codigo/NOTAS_DEL_PARCHE.txt).
- [Resumen de las comprobaciones](docs/VALIDACION.txt).
- Descarga del paquete ZIP y documentos PDF: pendientes de publicación.

El repositorio es público. La migración no configura por sí sola un canal automático del actualizador. Los siguientes pasos de actualización se aplican al paquete una vez esté disponible.

Desde la versión 2.0: extraer el ZIP y abrir `SSHBackup_2.1.0.sshupdate` dentro de **Herramientas → Actualizaciones → Abrir paquete**. Esperar a que terminen las copias antes de instalar. También se puede cerrar el programa y sustituir `SSHBackup.exe` en la misma ubicación, con el mismo usuario de Windows.

## Funciones

- Descargas por SSH/SFTP con contraseña o clave, huellas verificadas y origen de solo lectura.
- Reanudación tras un corte; ZIP final completo y verificado con SHA-256.
- Copiar solo novedades, opcional; cada ZIP final es independiente.
- Programación opcional con siete casillas de días, cualquier combinación y hora del ordenador. Requiere PC encendido y sesión iniciada.
- Comparador por hashes y buscador por nombre/ruta entre las copias.
- Límite opcional de descarga SSH en Mbps o MB/s, compartido entre conexiones.
- Actualizaciones firmadas, importación/exportación CSV/XLSX, modo claro y oscuro, y diseño con menú lateral.

Los ZIP originales del servidor se guardan íntegros dentro de la copia; no se descomprimen. El buscador no examina texto de documentos ni el interior de ZIP anidados. El modo novedades usa tamaño y fecha remotos; si se modifica contenido manteniendo ambos metadatos, hay que hacer una descarga completa.

## Código y compilación

Las fuentes, el registro de dependencias fijadas y las licencias están en [Codigo/](Codigo/). Las DLL aún no se han incorporado a esta rama. En Windows, usar `Codigo/compilar.ps1` con las herramientas de compilación de .NET Framework 4.8, según sus comprobaciones iniciales. `Codigo/SSHBackup.csproj` es el proyecto .NET Framework; el ejecutable incorpora las DLL necesarias.

`build.py` y las comprobaciones Python se usaron con un runtime Mono local, no incluido aquí. `build.py` admite `SSHBACKUP_MONO_ROOT` y `SSHBACKUP_NET48_REFS`; los verificadores esperan el runtime en las rutas locales que definen al principio. Para reproducirlos en otro entorno hay que preparar ese runtime y ajustar dichas rutas. No hace falta Python para ejecutar el programa portátil.

La clave privada de firma se conserva de forma privada fuera del repositorio. `Codigo/crear_actualizacion.py` recibe su ruta mediante `--clave`; la clave pública de verificación está en el código.

## Estado de validación

Pruebas locales en Linux/Mono superadas: interfaz en claro/oscuro y tamaño mínimo, programación y migración, SSH/SFTP, reanudación, novedades, comparación, búsqueda, límite agregado, importación/exportación y paquetes firmados. **Sigue pendiente la validación en Windows nativo**, especialmente el Programador de tareas y el arranque posterior a instalar una actualización. No se garantiza ausencia de fallos.

La migración a GitHub conserva los bytes de la entrega: no añade funciones ni cambia la versión. La descarga del paquete está pendiente de publicación; la consulta automática de novedades sigue sin canal web configurado.

## Integridad de la entrega

```text
SSHBackup_Completo_2.1.0.zip
SHA-256 d4b4eae3c04395aa0c2aab2df6bde746978403c5229943f9ca8adda3deb741ed

SSHBackup.exe (dentro del ZIP)
SHA-256 2b9936b40e9a7676d38359db0921b96100ea892a61b32523de4bf40ff47358c9
```

La captura de programación se incorporará al completar la publicación de los archivos de la entrega.
