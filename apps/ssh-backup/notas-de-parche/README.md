# Notas de parche de SSH Backup

Las versiones nuevas se añaden arriba. Se conserva el historial anterior.

## 2.2.1 — 1 de noviembre de 2026

Corrige los botones **Novedades en la web** y **Notas en GitHub** para abrir sus URL en el navegador. El flujo de actualización en línea de 2.2.0 se conserva.

Para el primer salto desde 2.0/2.1, cierra el programa y sustituye `SSHBackup.exe` en su ubicación habitual, usando el mismo usuario Windows. Desde 2.2, **Herramientas → Actualizaciones → Comprobar actualizaciones** permite descargar e instalar desde la app.

[Descargar](../descargas/README.md) · [Validación](../docs/VALIDACION-2.2.1.md)

## 2.2.0 — 1 de noviembre de 2026

**Actualizaciones en línea desde la aplicación, como en GUARDA.**

- Canal oficial preparado: **Herramientas → Actualizaciones → Comprobar actualizaciones**.
- Revisa las novedades y pulsa **Instalar actualización**, con progreso y cancelación de la descarga.
- Firma RSA y SHA-256 con la clave original; conserva configuración, credenciales, huellas e historial.
- El instalador confirma que está listo antes de cerrar la app y conserva `SSHBackup.exe.previous`. Si falla el inicio del proceso nuevo, restaura el ejecutable anterior.
- Durante la instalación no arrancan copias manuales ni programadas.
- Continúan los canales personalizados, los paquetes `.sshupdate` y los enlaces a las novedades de la web y GitHub.

**Salto inicial desde 2.0/2.1:** termina las copias, cierra SSH Backup y sustituye `SSHBackup.exe` por el nuevo en la misma ubicación, con el mismo usuario Windows. La entrega 2.1 tiene un fallo en su instalador antiguo; este primer salto se hace manualmente. Desde 2.2, comprueba e instala las próximas versiones desde la app.

Comprueba que no haya copias en marcha. Mantén el EXE en la misma ubicación y utiliza el mismo usuario de Windows. Si lo mueves, vuelve a guardar la programación.

[Descargar](../descargas/README.md) · [Comprobaciones y límites](../docs/VALIDACION-2.2.0.md) · [Novedades en la web](https://xycel95.es/aplicaciones/ssh-backup/#updates-title)

---

## 2.1.0 — 23 de octubre de 2026

**Diseño renovado y programación por días.**

NOVEDADES
- Menú lateral con iconos y sección seleccionada visible.
- Tipografía más clara, botones y paneles redondeados, campos renovados.
- Estilo común en Copias, Servidores, Historial, Ajustes, Herramientas y diálogos.
- Modo claro y oscuro; se conserva la preferencia guardada.
- Siete casillas independientes para programar: lunes a domingo.
- Cualquier combinación, con accesos Todos los días y Ninguno.
- Resumen de la selección y de la hora; no permite activar sin días.
- Las selecciones anteriores de la versión 2.0 se migran automáticamente.
- Los ajustes largos permiten desplazarse al reducir la ventana.
- Los avisos de cambios en el origen detallan qué nombres han aparecido o desaparecido.

SE MANTIENEN LAS FUNCIONES DE LA VERSIÓN 2.0
Reanudación de descargas, copiar solo novedades opcional, copias programadas opcionales, comparador, buscador histórico, actualizaciones firmadas y límite opcional de descarga SSH en Mbps o MB/s. El límite es compartido entre conexiones. La programación usa la hora del ordenador; requiere PC encendido y sesión de Windows iniciada. No hay un canal público de actualizaciones preconfigurado.

ACTUALIZAR
Desde 2.0: extrae SSHBackup_2.1.0.sshupdate y ábrelo en Herramientas > Actualizaciones > Abrir paquete. Espera a que terminen las copias antes de instalar.
Alternativa y versiones 1.x: cierra el programa, sustituye SSHBackup.exe en su ubicación habitual y abre con el mismo usuario Windows. Se conservan servidores, accesos, huellas, historial y ajustes. Si mueves el EXE, vuelve a guardar la programación.

PRUEBAS
Pruebas locales de interfaz en claro, oscuro y tamaño mínimo, navegación, programación y migración, copias reales SSH/SFTP, reanudación, novedades, comparación, búsqueda, límite agregado, importación/exportación y actualización firmada con restauración del EXE anterior.
Entorno: Linux/Mono. La ejecución nativa Windows, el Programador de tareas y el arranque tras actualizar quedan pendientes de validación en el equipo de destino.

---

La organización del repositorio del 25 de octubre de 2026 no es una versión nueva del programa ni cambia su código. La [descarga original 2.1.0](../descargas/README.md) está disponible; se han comprobado su integridad y el paquete firmado.
