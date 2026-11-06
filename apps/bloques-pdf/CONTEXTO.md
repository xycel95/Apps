# Estado de Bloques PDF

- Versión actual: **1.0.4**, 6 de noviembre de 2026.
- Repositorio: `xycel95/Apps`; rama permanente `bloques-pdf`; carpeta `apps/bloques-pdf`.
- ZIP: https://github.com/xycel95/Apps/releases/download/bloques-pdf-v1.0.4/BloquesPDF_1.0.4_Windows.zip
- SHA-256 del ZIP: `8f938be6ad4e5042cf262557124627429bc96b43f1807b53c7edf1d827dba82c`.
- SHA-256 de `BloquesPDF.exe`: `eded5eb265c440a639940d10f5852dc494b9536044611f60601097058da2500d`.

## Funcionamiento acordado

Botón para seleccionar varios PDF, arrastre de PDF o carpetas y selección de carpeta completa. Sin límite fijo de entrada; 50 originales por bloque por defecto. Se pregunta el destino al crear los bloques.

Cada documento se añade una sola vez y la exportación solo contiene PDF. No hay opción de copias ni CSV, TXT o marcadores en el destino. Errores dentro de la aplicación. Nombres BLOQUE 1.pdf, BLOQUE 2.pdf, etc. Carpetas BloquesPDF_yyyy.MM.dd_HH.mm.ss; sufijos (2), (3), etc. ante coincidencias. Los originales no se modifican; las firmas deben validarse en los originales. El modo de carpetas preserva sus bytes.

## Bloqueo de descarga y cambio de compilación

El usuario comunicó «Se ha detectado un virus» al descargar 1.0.3 en Edge. No se conoce el nombre de la detección ni se ha confirmado su causa exacta.

1.0.4 sustituye el EXE con bibliotecas fusionadas por el EXE normal del SDK, con 18 DLL y .config separados. No se usa ILRepack. El usuario debe extraer todo el ZIP y mantener sus componentes juntos. Estos archivos son del programa y no se exportan con los PDF.

La entrega se ha compilado y comprobado en un Windows limpio de GitHub Actions. Defender no detectó amenazas en los componentes ni en el ZIP. No se desactivó protección ni se añadieron exclusiones. El análisis y la ausencia de detecciones están limitados a ese motor y firmas; no afirmar que se ha comprobado el antivirus del equipo del usuario.

## Evidencia

- Ejecución: https://github.com/xycel95/Apps/actions/runs/42289188880
- Revisión de fuentes: `9c9b394c0abe8707d294997b8f46edc40ea40ca0`.
- 16 comprobaciones nativas superadas y arranque/cierre del EXE distribuido comprobado.
- Defender 1.1.26100.4, firmas 1.463.163.0.
- Descarga pública, SHA-256, CRC, manifiesto de componentes y fuentes comprobados.
- Informes actuales: docs/VALIDACION-WINDOWS-1.0.4.json, docs/DEFENDER-1.0.4.txt y docs/PUBLICACION.json.
- La evidencia de versiones previas se conserva con su sufijo.

## Continuación

C# / Windows Forms / .NET Framework 4.8, SDK .NET 10 y PDFsharp 6.2.4. Lee Codigo/COMPILAR.txt. Los scripts de build preparan el paquete normal y lo comprueban con Defender y pruebas de Windows antes de publicarlo. Al cambiar la versión, coordina csproj, Creator, Validate-Windows.ps1 y el flujo de publicación; no sobreescribas una versión existente sin comprobar sus bytes.

No hay firma Authenticode ni actualizador automático. Pendientes: antivirus y políticas del equipo del usuario e impresora física. Las pruebas nativas crean solo PDF sintéticos; no publicar documentos reales, credenciales ni certificados privados.
