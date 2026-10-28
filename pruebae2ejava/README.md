# Pruebas E2E con Java, Selenium y JUnit

Este proyecto reproduce los casos de búsqueda en Google y YouTube implementados en `pruebae2e`, usando Java 17+, Maven, Selenium 4 y JUnit 5. Selenium Manager administra ChromeDriver y Chrome estable.

## Ejecutar

Desde `pruebae2ejava`:

```bash
mvn test
```

Para ejecutar un caso por separado:

```bash
mvn -Dtest=WebSearchTests#buscarEnGoogleMuestraResultados test
mvn -Dtest=WebSearchTests#buscarVideoEnYouTubeMuestraResultados test
```

Después de ejecutar los casos, genera el reporte HTML en un segundo comando. Así también puedes obtenerlo cuando `mvn test` termina con fallos:

```bash
mvn surefire-report:report-only
```

El reporte queda en `target/reports/surefire.html` y los resultados XML en `target/surefire-reports/`.

Las consultas predeterminadas son `Playwright .NET` en Google y `Playwright tutorial` en YouTube. Se pueden cambiar con `GOOGLE_SEARCH_QUERY` y `YOUTUBE_SEARCH_QUERY`. Ambos sitios requieren Internet; Google puede limitar tráfico automatizado según la red.