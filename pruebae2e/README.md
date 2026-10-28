# Pruebas E2E de búsqueda en Google y YouTube

Proyecto de pruebas con .NET 10, NUnit y Playwright para buscar en Google y YouTube, y verificar los resultados.

## Preparación

Se requiere PowerShell 7 (`pwsh`) para ejecutar el instalador generado por Playwright. Desde la carpeta `pruebae2e`, instala Chromium:

```bash
dotnet build
pwsh ./bin/Debug/net10.0/playwright.ps1 install chromium
```

En Linux puedes instalar también las dependencias del sistema si el navegador las solicita:

```bash
pwsh ./bin/Debug/net10.0/playwright.ps1 install-deps chromium
```

## Ejecutar

Este comando ejecuta las pruebas y genera un reporte HTML con resumen, estados y errores desplegables. El proceso conserva el código de salida de las pruebas, así que seguirá indicando fallo si alguna no pasa.

```bash
dotnet run --project tools/TestReport
```

El reporte se guarda en `TestResults/report.html`. Para volver a generar el reporte a partir de un TRX existente, sin ejecutar las pruebas:

```bash
dotnet run --project tools/TestReport -- TestResults/results.trx
```

Las consultas predeterminadas son `Playwright .NET` en Google y `Playwright tutorial` en YouTube. Puedes personalizarlas así:

```bash
GOOGLE_SEARCH_QUERY="pruebas e2e con Playwright" dotnet run --project tools/TestReport
YOUTUBE_SEARCH_QUERY="tutorial de Playwright" dotnet run --project tools/TestReport
```

Las pruebas requieren acceso a Internet. Google puede limitar solicitudes automatizadas y YouTube puede mostrar una pantalla de consentimiento según la región y la red.