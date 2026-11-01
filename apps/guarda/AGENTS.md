# Trabajar en Guarda

Aplicar también las instrucciones generales de `AGENTS.md` en la raíz del repositorio.

## Alcance y organización

- Aplicación: Guarda. Rama permanente: `guarda`. Carpeta: `apps/guarda/`.
- Leer `CONTEXTO.md`, `app.json` y las notas de parche antes de continuar. Consultar la revisión remota vigente antes de publicar.
- Mantener la navegación principal: Descripción del programa, Cómo funciona y guía, Descargar y Notas de parche, en ese orden.
- La rama `main` solo contiene el catálogo general. No fusionar la rama de Guarda en `main` ni alterar SSH Backup por una petición de Guarda.
- No publicar registros reales, preferencias del usuario, copias de sus datos, credenciales, herramientas de transferencia ni el entorno de compilación.

## Comportamiento que debe conservarse

- Windows, WinForms, .NET Framework 4.8, ejecutable AnyCPU sin elevación.
- Copia completa de una carpeta a un destino accesible, con fecha local invariante `yyyy_MM_dd` al principio del nombre.
- Varias copias el mismo día se numeran sin sobrescribir archivos ni carpetas existentes.
- No cambiar ni borrar el contenido del origen ni copias anteriores. Bloquear el destino igual al origen o anidado en él, también mediante rutas físicas resueltas.
- Verificación SHA-256 activada por defecto. Los enlaces y puntos de reanálisis se indican como omisiones; no presentar su copia como completa.
- Mantener los sufijos de copia en curso o incompleta cuando no termina correctamente. No confundirlos con una copia finalizada.
- Las preferencias de Copia Fácil se leen como alternativa si todavía no existen preferencias de Guarda.

## Cambios y publicación

- Motor en `Codigo/BackupEngine.cs`; interfaz y preferencias en `Codigo/Program.cs`; pruebas en `pruebas/`.
- Compilar y comprobar el flujo afectado. Si cambia el motor, utilizar las pruebas funcionales pertinentes; si cambia la ventana, revisar el flujo y el aspecto.
- `Codigo/build.py` incorpora y verifica el manifiesto de Windows mediante `winres.py`. No omitir esta comprobación cuando se compile con Mono.
- Reutilizar comprobaciones vigentes si los bytes no cambian. No afirmar ejecución nativa en Windows ni una prueba de USB físico sin haberla realizado.
- Una nueva entrega coordina las versiones declaradas, ejecutable, paquete completo, `descargas/SHA256SUMS.txt`, `app.json`, notas y contexto. Las notas se añaden arriba.
- No hay programación, copia incremental, SSH ni actualizador en línea. No describirlos como funcionalidades existentes.
