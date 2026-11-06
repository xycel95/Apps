# Cómo funciona y guía

## Empezar

1. [Descarga el ZIP de Windows](../descargas/README.md) y descomprímelo.
2. Abre **BloquesPDF.exe**.
3. Añade documentos mediante **Añadir PDF**, arrastrando archivos o carpetas, o con **Añadir carpeta**.
4. Revisa **Documentos añadidos** y **Bloques previstos**.

Puedes seleccionar todos los PDF de una carpeta con **Ctrl+A** en el Explorador de Windows y arrastrarlos a la ventana. El mismo archivo no se añade dos veces. Archivos distintos con el mismo nombre se conservan como documentos diferentes.

## Crear los bloques

1. Deja **50** en **PDF originales por bloque**, o indica otra cantidad.
2. Elige **Unir en PDF para imprimir**.
3. Revisa **Copias de cada documento**: el valor inicial es **2**. Para unir cada documento una sola vez, elige **1**.
4. Pulsa **Crear bloques**. El programa te preguntará **dónde guardarlos**.
5. Al terminar, pulsa **Abrir resultado**.

Dentro del destino se crea una carpeta nueva `BloquesPDF_fecha_hora_identificador`. Contiene los archivos **BLOQUE 1.pdf**, **BLOQUE 2.pdf**, etc. Cada trabajo empieza de nuevo en 1 y conserva los resultados anteriores en sus propias carpetas.

| PDF originales | Tamaño del bloque | Archivos resultantes |
| --- | --- | --- |
| 100 | 50 | BLOQUE 1.pdf y BLOQUE 2.pdf, con 50 originales cada uno. |
| 103 | 50 | Tres PDF, con 50, 50 y 3 originales. |
| 3.000 | 50 | 60 PDF, con 50 originales cada uno. |

Los documentos se ordenan por nombre y ruta en orden natural, por ejemplo `documento2.pdf` antes de `documento10.pdf`.

## Imprimir las copias

El PDF final incluye las repeticiones elegidas. **Selecciona 1 copia en el cuadro de impresión** para no duplicarlas otra vez.

- Con **1 copia de cada documento** en la aplicación, 50 originales de una página generan un PDF de 50 páginas.
- Con **2 copias de cada documento**, generan un PDF de 100 páginas.
- Un original de dos páginas se repite como `1, 2, 1, 2` cuando eliges dos copias.

No se añaden portadas ni páginas en blanco. Revisa la configuración de doble cara y el primer bloque antes de una tirada grande.

## Conservar originales firmados en carpetas

Selecciona **Carpetas con los PDF firmados originales** si quieres copiarlos sin unirlos. Se crean carpetas **BLOQUE 1**, **BLOQUE 2**, etc. Las copias de impresión se eligen después en la impresora.

## Errores, cancelación y seguimiento

Los PDF se comprueban antes de generar los bloques unidos. Si uno está dañado o protegido y no puede abrirse, el trabajo se detiene y muestra el problema. No se desbloquean PDF con contraseña.

**Cancelar** conserva los bloques ya completos y elimina el bloque en curso. Cada resultado incorpora:

- `LEEME_RESULTADO.txt`: estado, errores e instrucciones de impresión.
- `detalle_documentos.csv`: originales, bloque, páginas, copias y huellas SHA-256.

Los originales no se modifican. Las firmas digitales deben validarse en ellos; los bloques unidos son copias para imprimir.

[Descripción del programa](../DESCRIPCION.md) · [Descargar](../descargas/README.md) · [Notas de parche](../notas-de-parche/README.md).
