# Descargar

**Guarda 1.1.0** · Windows 10/11 con .NET Framework 4.8 o 4.8.1.

- **[Descargar Guarda.exe](https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/Guarda.exe)** — ejecutable portable. Puedes abrirlo directamente.
- **[Descargar el paquete completo](https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/Guarda_v1.1.0_Completo.zip)** — ejecutable, código, guía y comprobaciones.

El paquete se entrega completo, sin bibliotecas adicionales de SSH Backup ni instalación de componentes propios. Requiere el .NET Framework indicado.

Para actualizar desde Copia Fácil o Guarda 1.0.1, cierra la aplicación y utiliza el nuevo `Guarda.exe`. Recupera las preferencias de Copia Fácil cuando Guarda aún no tiene preferencias propias.

Desde **1.1.0**, utiliza **Actualizaciones → Buscar → Actualizar y reiniciar** para las futuras versiones. Las actualizaciones son manuales desde la app; no se instalan de forma silenciosa. El anuncio firmado se comprueba con la clave pública integrada. El canal ya anuncia esta versión 1.1.0, por lo que ella aparecerá como actualizada hasta la siguiente entrega.

## Integridad y validación

Las huellas de los archivos están en [SHA256SUMS.txt](SHA256SUMS.txt). El actualizador utiliza una [copia fija del ejecutable 1.1.0](versiones/1.1.0/Guarda.exe), vinculada al [canal firmado](../actualizaciones/stable.xml).

El motor, el actualizador y la interfaz se comprobaron en Linux/Mono. **Sigue pendiente probar el cierre, sustitución y reinicio completos en Windows real y con un USB físico.** Consulta [VALIDACION.txt](../docs/VALIDACION.txt).

La entrega anterior [1.0.1](Guarda_v1.0.1_Completo.zip) se conserva como archivo histórico.

[Descripción del programa](../DESCRIPCION.md) · [Cómo funciona y guía](../docs/README.md) · [Notas de parche](../notas-de-parche/README.md).
