# Cómo utilizar SSH Backup

## Empezar

1. Descarga el ZIP de [Descargas](../descargas/README.md), extráelo y mantén el ejecutable en una ubicación fija.
2. Abre SSHBackup.exe con tu usuario de Windows.
3. En Servidores, añade un servidor o importa una plantilla CSV/XLSX. Indica alias, acceso SSH, carpeta de origen y destino local.
4. Usa Comprobar SSH y revisa la huella del servidor antes de aceptarla.
5. Marca los servidores y pulsa Iniciar copia en Copias.
6. Revisa el historial y el resultado de la verificación al terminar.

El programa usa el origen remoto en solo lectura. Los ZIP que ya existían en el servidor se conservan como archivos dentro de la copia, sin descomprimirlos allí.

## Opciones

- **Reanudación:** vuelve a intentar la copia con el mismo origen y destino; los temporales compatibles permiten continuar la descarga.
- **Novedades:** reutiliza archivos sin cambios de una copia compatible anterior. Cada ZIP resultante sigue siendo completo.
- **Programación:** elige hora y días en Herramientas. Necesita PC encendido, sesión iniciada y una ubicación fija del ejecutable.
- **Límite de red:** se configura en Ajustes y se comparte entre las conexiones SSH de la aplicación.
- **Actualizaciones:** un paquete firmado se abre desde Herramientas → Actualizaciones → Abrir paquete. No hay un canal online configurado por el hecho de publicar esta rama.

## Documentación detallada

- [Guía completa original 2.1.0](LEEME.txt).
- [Guía completa en PDF](SSHBackup_Guia_de_uso.pdf).
- [Notas de parche 2.1.0 en PDF](SSHBackup_Notas_del_parche_2.1.0.pdf).
- [Comprobaciones y límites conocidos](VALIDACION.txt).
- [Notas de parche](../notas-de-parche/README.md).

La guía original describe la distribución de la entrega del 23 de octubre. La entrega completa original ya está disponible en [Descargar](../descargas/README.md). La migración conserva el código existente y no cambia la versión del programa.
