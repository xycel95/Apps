# Componentes de terceros

Visor Tapo es independiente de TP-Link/Tapo. Estos nombres identifican los
dispositivos compatibles y no implican patrocinio ni afiliación.

- Python 3.13.17, distribución embebida oficial para Windows x64.
  Licencia PSF, incluida en `runtime/LICENSE.txt`.
  https://www.python.org/downloads/release/python-31317/
  Código fuente: https://www.python.org/ftp/python/3.13.17/
- PySide6 / Qt 6.12.0 y Shiboken 6.12.0, bibliotecas dinámicas LGPL/GPL según
  cada componente. Se incluyen sus avisos y textos en los directorios
  `runtime/Lib/site-packages/*dist-info/licenses` y en el paquete PySide6.
  Código fuente y condiciones: https://code.qt.io/cgit/pyside/pyside-setup.git/
  y https://code.qt.io/cgit/qt/ . Puedes sustituir las bibliotecas dinámicas
  por versiones compatibles para modificar y depurar estos componentes.
- pytapo 3.4.30, MIT. Código fuente, documentación y avisos del autor:
  https://github.com/JurajNyiri/pytapo
- FFmpeg 9.0.2 essentials build de Gyan, ejecutables independientes.
  Se mantienen los avisos, documentación y licencia del distribuidor en
  `licencias/FFmpeg`. Distribución y fuentes/builds:
  https://www.gyan.dev/ffmpeg/builds/
  https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz
  Se invoca como proceso separado para preparar y comprobar los vídeos.
- Requests, urllib3, PyCryptodome, python-kasa, aiofiles, rtp, tzdata y las
  dependencias transitivas mantienen las licencias de sus respectivos
  autores. Sus metadatos y avisos se conservan en `runtime/Lib/site-packages`.
- Microsoft Visual C++ Runtime, redistribuibles a través de msvc-runtime.
  Se conservan sus metadatos. https://pypi.org/project/msvc-runtime/

`build_inputs.json` contiene las versiones, URL de origen y SHA-256 de todos
los paquetes utilizados. Los componentes no se modifican; el adaptador de
conversión y la interfaz se implementan en `src`.
