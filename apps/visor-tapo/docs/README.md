# Cómo funciona y guía

Aplicación independiente para Windows que permite ver el directo de una cámara
Tapo y consultar sus grabaciones de microSD desde la red local.

**Versión 1.2.0 · Antes: Visor C230.** Proyecto no oficial, sin afiliación con
TP-Link ni Tapo. El nombre identifica las cámaras para las que se desarrolla.

## Compatibilidad

La [lista de cámaras compatibles](COMPATIBILIDAD.md) revisa **133 modelos, kits
y variantes** a partir de sus fichas y del soporte oficial de TP-Link:

| Directo | Modelos y variantes |
| --- | ---: |
| Compatible por RTSP documentado | 64 |
| Con condiciones de conexión u objetivo | 10 |
| Sin directo compatible con esta versión | 47 |
| Sin información técnica suficiente | 12 |

La C230 está entre los modelos con directo compatible. La lista se puede buscar
dentro del programa, en **«Modelos y compatibilidad»**.

Para la microSD, **«Previsto con pytapo» es una previsión técnica**, sujeta al
hardware, firmware y autenticación. RTSP no garantiza ese acceso. Esta versión
no implementa cámaras hijas ni grabaciones almacenadas detrás de un hub.

## Requisitos

- Windows 10/11 de 64 bits para el paquete portable.
- Cámara y PC accesibles en la misma red local privada IPv4.
- Para el directo: cámara con RTSP y una «Cuenta de cámara» configurada en Tapo.
- Para grabaciones: microSD con vídeos, modelo/firmware compatible con pytapo y
  las credenciales necesarias para acceder a sus grabaciones.
- No es necesario instalar Python, VLC o FFmpeg al abrir el ejecutable portable.

## Abrir y conectar

1. Descarga **`Visor_Tapo_1.2.0_Windows.exe`**.
2. Ábrelo directamente. Puedes moverlo o renombrarlo: el programa y sus dependencias están dentro. La preparación temporal se realiza automáticamente al abrir y se limpia al cerrar.
3. Pulsa **«Buscar cámaras en casa»** y selecciona tu cámara. Si no aparece,
   consulta la IP en Tapo → tu cámara → Ajustes → Información del dispositivo.
4. Para el directo, introduce el usuario y contraseña de **«Cuenta de cámara»**,
   creada en Tapo → tu cámara → Ajustes → Configuración avanzada.
5. Para la microSD, introduce la contraseña de tu cuenta Tapo/TP-Link ID en el
   apartado correspondiente. No introduzcas la contraseña de otro proveedor de
   identidad, como Google, en ese campo.
6. Pulsa **«Abrir directo»**, o entra en **Grabaciones**, elige una fecha y pulsa
   **«Consultar microSD»**.

Los dos tipos de credenciales son independientes. La ayuda **«¿Dónde saco los
datos?»** explica su configuración. El botón **«Probar acceso a microSD»**
comprueba ese acceso; no es una prueba del reproductor de directo.

## Funciones

- Directo con sonido, calidad alta o ligera, pausa y pantalla completa.
- Capturas PNG.
- Consulta del catálogo microSD por fecha y reproducción de una copia local.
- Vídeos continuos separados en tramos de hasta cinco minutos.
- Continuación al siguiente tramo y exportación MP4.
- Giros cortos en modelos motorizados compatibles.
- Diagnóstico exportable sin contraseñas ni fotogramas.
- Modo oscuro y guardado opcional de claves protegido por Windows.

**La grabación seleccionada se descarga antes de reproducirse.** El programa
gestiona una cámara cada vez. No borra ni formatea la microSD, no cambia su
programación de grabación y no ofrece acceso a vídeos de Tapo Care.

Las copias consultadas permanecen en el PC. **«Copias en este PC»** abre su
carpeta para que puedas gestionarlas con el Explorador.

## Actualizar desde Visor C230

Cierra la versión anterior y abre el nuevo ejecutable portable. Se conserva el
directorio de datos histórico `%LOCALAPPDATA%\VisorC230` para mantener ajustes,
claves protegidas y vídeos ya descargados. Las nuevas capturas y exportaciones
usan el prefijo `Tapo_`; las copias antiguas siguen siendo accesibles.

## Resolver problemas

- Si no aparece en la búsqueda, revisa que ambos dispositivos compartan red y
  que Windows permita la comunicación en la red privada. También puedes poner
  la IP manualmente.
- Si falla el directo, verifica la «Cuenta de cámara», la IP y la calidad ligera.
- Si aparece el catálogo pero falla un vídeo, son fases distintas: no presupongas
  que la contraseña es incorrecta. Pulsa **«Guardar diagnóstico»** y adjunta ese
  archivo al informe del error.
- Si la cámara está ocupada, cierra otras reproducciones antes de reintentar.
- El último minuto puede no aparecer mientras la cámara termina de escribirlo.

## Privacidad

Las conexiones a la cámara se realizan por su dirección local. Las contraseñas
se transmiten al proceso de conexión por una entrada privada, sin incluirlas
en argumentos de procesos ni en el diagnóstico. El guardado permanente es
opcional y usa DPAPI del usuario de Windows. Sin esa opción no se guardan claves.

No publiques capturas con **«Mostrar contraseñas»** activado, archivos de ajustes
ni vídeos privados al informar de un problema. La app no contiene credenciales
preconfiguradas ni conecta una cámara automáticamente al abrirse.

## Desarrollo

Ejecuta los comandos desde [`Codigo`](../Codigo/).

Entorno previsto: Python 3.13 y FFmpeg/ffprobe disponibles en `PATH` o en `bin`.

```sh
python -m pip install -r requirements-dev.txt
python src/app.py
```

Pruebas automatizadas, sin contactar con cámaras reales:

```sh
PYTHONPATH=src python -m unittest discover -s tests -v
PYTHONPATH=src QT_QPA_PLATFORM=offscreen python tests/ui_smoke.py
```

En PowerShell, asigna `$env:PYTHONPATH="src"` antes de ejecutar las pruebas.
Para crear el ejecutable único, desde Windows x64:

```sh
python -m pip install -r requirements-build-windows.txt
python build_onefile.py
```

`build_windows.py` genera el paquete interno de dependencias; el archivo que se entrega al usuario es el `.exe` generado por `build_onefile.py`.

Las dependencias de distribución se fijan por versión y SHA-256 en
`build_inputs.json`. El ejecutable único incluye icono, versión y manifiesto sin elevación. Los archivos de funcionamiento se preparan en una carpeta temporal privada; los ajustes y vídeos siguen en la carpeta de datos del usuario de Windows. El script compila y empaqueta; la publicación en GitHub es un paso separado.

## Informar de compatibilidad

Consulta [CONTRIBUTING.md](CONTRIBUTING.md) para preparar el informe de compatibilidad. Registrar una cámara requiere indicar las funciones verificadas,
modelo, versión de hardware, firmware y versión de la app. No se marca un modelo
como plenamente probado por el hecho de que se detecte su IP o se abra el directo.

## Créditos y licencia

Código de la aplicación: [MIT](../LICENSE). Dependencias bajo sus respectivas
licencias, con avisos y fuentes de referencia en [TERCEROS.md](TERCEROS.md).
El acceso a la microSD utiliza [pytapo](https://github.com/JurajNyiri/pytapo),
una biblioteca no oficial de Juraj Nyíri y sus colaboradores.

Guía de conexión del fabricante:
https://www.tp-link.com/es/support/faq/2680/
