using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;

internal static class Program
{
    private const string TestProject = "GoogleSearchE2E.csproj";
    private const string ResultsDirectory = "TestResults";
    private const string TrxFileName = "results.trx";

    private static async Task<int> Main(string[] args)
    {
        var projectDirectory = FindProjectDirectory();
        var resultsDirectory = Path.Combine(projectDirectory, ResultsDirectory);
        Directory.CreateDirectory(resultsDirectory);

        var trxPath = args.Length == 0
            ? Path.Combine(resultsDirectory, TrxFileName)
            : Path.GetFullPath(args[0]);
        var reportPath = Path.Combine(Path.GetDirectoryName(trxPath)!, "report.html");
        var testExitCode = 0;

        if (args.Length == 0)
        {
            if (File.Exists(trxPath))
            {
                File.Delete(trxPath);
            }

            testExitCode = await RunTestsAsync(projectDirectory, resultsDirectory);
        }
        else if (args.Length != 1)
        {
            Console.Error.WriteLine("Uso: dotnet run --project tools/TestReport [ruta/al/resultado.trx]");
            return 2;
        }

        if (!File.Exists(trxPath))
        {
            Console.Error.WriteLine($"No se encontró el archivo TRX: {trxPath}");
            return testExitCode == 0 ? 1 : testExitCode;
        }

        try
        {
            GenerateReport(trxPath, reportPath);
            Console.WriteLine($"Reporte HTML generado: {reportPath}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"No se pudo generar el reporte: {exception.Message}");
            return testExitCode == 0 ? 1 : testExitCode;
        }

        return testExitCode;
    }

    private static async Task<int> RunTestsAsync(string projectDirectory, string resultsDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(TestProject);
        startInfo.ArgumentList.Add("--logger");
        startInfo.ArgumentList.Add($"trx;LogFileName={TrxFileName}");
        startInfo.ArgumentList.Add("--results-directory");
        startInfo.ArgumentList.Add(resultsDirectory);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("No se pudo iniciar dotnet test.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.Write(await standardOutput);
        Console.Error.Write(await standardError);
        return process.ExitCode;
    }

      private static string FindProjectDirectory()
      {
        foreach (var startPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
          var directory = new DirectoryInfo(startPath);
          while (directory is not null)
          {
            if (File.Exists(Path.Combine(directory.FullName, TestProject)))
            {
              return directory.FullName;
            }

            directory = directory.Parent;
          }
        }

        throw new DirectoryNotFoundException($"No se encontró {TestProject} en el workspace ni junto al generador.");
      }

    private static void GenerateReport(string trxPath, string reportPath)
    {
        var document = XDocument.Load(trxPath);
        var root = document.Root ?? throw new InvalidDataException("El archivo TRX no contiene una ejecución.");
        var xmlNamespace = root.Name.Namespace;
        var results = root.Descendants(xmlNamespace + "UnitTestResult")
            .Select(result => new TestResult(
                Name: (string?)result.Attribute("testName") ?? "Prueba sin nombre",
                Outcome: (string?)result.Attribute("outcome") ?? "Unknown",
                Duration: ParseDuration((string?)result.Attribute("duration")),
                Error: result.Element(xmlNamespace + "Output")?
                    .Element(xmlNamespace + "ErrorInfo")?
                    .Element(xmlNamespace + "Message")?.Value ?? string.Empty))
            .ToArray();

        var passed = results.Count(result => result.Outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase));
        var failed = results.Count(result => result.Outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase)
            || result.Outcome.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var skipped = results.Length - passed - failed;
        var passRate = results.Length == 0 ? 0 : (int)Math.Round((double)passed / results.Length * 100);
        var times = root.Element(xmlNamespace + "Times");
        var started = ParseDate((string?)times?.Attribute("start"));
        var finished = ParseDate((string?)times?.Attribute("finish"));
        var runDuration = started.HasValue && finished.HasValue
            ? (finished.Value - started.Value).ToString("m'm 's's'", CultureInfo.InvariantCulture)
            : "No disponible";
        var runStatus = failed > 0 ? "Con fallos" : results.Length > 0 ? "Correcta" : "Sin pruebas";
        var statusClass = failed > 0 ? "failed" : results.Length > 0 ? "passed" : "skipped";
        var runDate = started?.ToUniversalTime().ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.GetCultureInfo("es-ES"))
            ?? "Fecha no disponible";

        var rows = new StringBuilder();
        foreach (var result in results)
        {
            var resultClass = GetStatusClass(result.Outcome);
            var resultLabel = GetStatusLabel(result.Outcome);
            var errorSummary = GetErrorSummary(result.Error);
            var errorDetails = string.IsNullOrWhiteSpace(result.Error)
                ? ""
                : $"<details class=\"error-details\"><summary>Ver detalle del error</summary><pre>{Encode(result.Error)}</pre></details>";

            rows.Append($"""
                <tr class="test-row" data-search="{Encode((result.Name + " " + result.Outcome).ToLowerInvariant())}">
                  <td><span class="status {resultClass}"><span class="status-dot"></span>{Encode(resultLabel)}</span></td>
                  <td><span class="test-name">{Encode(result.Name)}</span>{(errorSummary.Length > 0 ? $"<span class=\"error-summary\">{Encode(errorSummary)}</span>" : "")}{errorDetails}</td>
                  <td class="duration-cell">{FormatDuration(result.Duration)}</td>
                </tr>
                """);
        }

        if (results.Length == 0)
        {
            rows.Append("<tr><td class=\"empty-state\" colspan=\"3\">El archivo TRX no contiene pruebas.</td></tr>");
        }

        var html = $$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="light">
              <title>Reporte E2E · Búsquedas web</title>
              <style>
                :root {
                  color-scheme: light;
                  --ink: #1d2929;
                  --muted: #647170;
                  --line: #dce4e1;
                  --paper: #f4f6f3;
                  --surface: #ffffff;
                  --green: #176d58;
                  --green-soft: #e6f3ed;
                  --red: #a63c35;
                  --red-soft: #faeae7;
                  --amber: #896116;
                  --amber-soft: #f8f0dc;
                  --accent: #286c69;
                }
                * { box-sizing: border-box; }
                body { margin: 0; background: var(--paper); color: var(--ink); font-family: Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif; font-size: 14px; line-height: 1.5; }
                .shell { max-width: 1120px; margin: 0 auto; padding: 38px 28px 56px; }
                .masthead { display: flex; align-items: center; gap: 16px; padding-bottom: 28px; border-bottom: 1px solid var(--line); }
                .mark { display: grid; width: 42px; height: 42px; place-items: center; background: var(--ink); color: #fff; font-size: 13px; font-weight: 750; letter-spacing: .04em; }
                .eyebrow { margin: 0 0 3px; color: var(--muted); font-size: 11px; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }
                h1 { margin: 0; font-size: 20px; font-weight: 680; letter-spacing: 0; }
                .run-state { display: inline-flex; align-items: center; gap: 8px; margin-left: auto; padding: 7px 11px; border: 1px solid var(--line); background: var(--surface); font-size: 12px; font-weight: 650; }
                .run-state.passed { color: var(--green); }
                .run-state.failed { color: var(--red); }
                .run-state.skipped { color: var(--amber); }
                .state-dot, .status-dot { width: 7px; height: 7px; border-radius: 50%; background: currentColor; }
                .intro { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; padding: 30px 0 25px; }
                .intro h2 { margin: 0; font-size: 25px; font-weight: 650; letter-spacing: 0; }
                .intro p { margin: 5px 0 0; color: var(--muted); }
                .run-meta { color: var(--muted); text-align: right; }
                .run-meta strong { display: block; color: var(--ink); font-size: 14px; font-weight: 650; }
                .metrics { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); border: 1px solid var(--line); background: var(--surface); }
                .metric { min-height: 104px; padding: 17px 20px; border-right: 1px solid var(--line); }
                .metric:last-child { border-right: 0; }
                .metric-label { display: block; color: var(--muted); font-size: 12px; }
                .metric-value { display: block; margin-top: 8px; font-size: 27px; font-weight: 680; line-height: 1.1; font-variant-numeric: tabular-nums; }
                .metric.passed .metric-value { color: var(--green); }
                .metric.failed .metric-value { color: var(--red); }
                .metric.skipped .metric-value { color: var(--amber); }
                .results { margin-top: 34px; }
                .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 13px; }
                .section-heading h3 { margin: 0; font-size: 16px; font-weight: 680; }
                .filter { width: min(100%, 270px); padding: 9px 11px; border: 1px solid var(--line); border-radius: 4px; background: var(--surface); color: var(--ink); font: inherit; }
                .filter:focus { outline: 2px solid #90b8af; outline-offset: 1px; }
                .table-wrap { overflow: hidden; border: 1px solid var(--line); background: var(--surface); }
                table { width: 100%; border-collapse: collapse; text-align: left; }
                th { padding: 11px 16px; background: #edf1ee; color: var(--muted); font-size: 10px; font-weight: 750; letter-spacing: .09em; text-transform: uppercase; }
                td { padding: 15px 16px; border-top: 1px solid var(--line); vertical-align: top; }
                .status { display: inline-flex; align-items: center; gap: 7px; min-width: 82px; font-size: 12px; font-weight: 650; }
                .status.passed { color: var(--green); }
                .status.failed { color: var(--red); }
                .status.skipped { color: var(--amber); }
                .test-name { display: block; font-weight: 640; overflow-wrap: anywhere; }
                .error-summary { display: block; margin-top: 4px; color: var(--red); font-size: 12px; overflow-wrap: anywhere; }
                .error-details { margin-top: 9px; color: var(--muted); font-size: 12px; }
                .error-details summary { width: fit-content; cursor: pointer; color: var(--accent); font-weight: 600; }
                .error-details pre { max-height: 360px; overflow: auto; margin: 9px 0 0; padding: 13px; background: #f6f7f5; color: #384341; font: 11px/1.55 ui-monospace, SFMono-Regular, Consolas, monospace; white-space: pre-wrap; overflow-wrap: anywhere; }
                .duration-cell { width: 125px; color: var(--muted); font-variant-numeric: tabular-nums; white-space: nowrap; }
                .empty-state { padding: 28px; color: var(--muted); text-align: center; }
                .footer { margin-top: 16px; color: var(--muted); font-size: 11px; }
                @media (max-width: 680px) {
                  .shell { padding: 22px 14px 36px; }
                  .masthead { gap: 11px; }
                  .run-state { padding: 6px 8px; }
                  .intro { align-items: flex-start; flex-direction: column; padding: 23px 0 18px; }
                  .intro h2 { font-size: 21px; }
                  .run-meta { text-align: left; }
                  .metrics { grid-template-columns: repeat(2, minmax(0, 1fr)); }
                  .metric { min-height: 86px; padding: 13px; }
                  .metric:nth-child(2) { border-right: 0; }
                  .metric:nth-child(n+3) { border-top: 1px solid var(--line); }
                  .metric-value { font-size: 23px; }
                  .section-heading { align-items: flex-start; flex-direction: column; }
                  .filter { width: 100%; }
                  th, td { padding: 11px 9px; }
                  th:first-child, td:first-child { width: 85px; }
                  .duration-cell { width: 76px; }
                }
                @media print {
                  body { background: white; }
                  .shell { max-width: none; padding: 0; }
                  .filter { display: none; }
                  .error-details pre { max-height: none; }
                }
              </style>
            </head>
            <body>
              <main class="shell">
                <header class="masthead">
                  <div class="mark" aria-hidden="true">E2E</div>
                  <div><p class="eyebrow">Calidad · Automatización</p><h1>Reporte de ejecución</h1></div>
                  <div class="run-state {{statusClass}}"><span class="state-dot"></span>{{runStatus}}</div>
                </header>
                <section class="intro">
                  <div><h2>Búsquedas web</h2><p>Google y YouTube · .NET 10 y Playwright</p></div>
                  <div class="run-meta"><strong>{{runDate}}</strong>Duración total · {{runDuration}}</div>
                </section>
                <section class="metrics" aria-label="Resumen de resultados">
                  <div class="metric"><span class="metric-label">Pruebas ejecutadas</span><strong class="metric-value">{{results.Length}}</strong></div>
                  <div class="metric passed"><span class="metric-label">Correctas</span><strong class="metric-value">{{passed}}</strong></div>
                  <div class="metric failed"><span class="metric-label">Fallidas</span><strong class="metric-value">{{failed}}</strong></div>
                  <div class="metric skipped"><span class="metric-label">Omitidas</span><strong class="metric-value">{{skipped}}</strong></div>
                </section>
                <section class="results">
                  <div class="section-heading"><h3>Detalle de pruebas</h3><input class="filter" id="filter" type="search" placeholder="Filtrar pruebas..." aria-label="Filtrar pruebas"></div>
                  <div class="table-wrap">
                    <table>
                      <thead><tr><th>Estado</th><th>Prueba</th><th>Duración</th></tr></thead>
                      <tbody>{{rows}}</tbody>
                    </table>
                  </div>
                </section>
                <p class="footer">Tasa de éxito: {{passRate}}% · Generado automáticamente desde los resultados TRX de NUnit.</p>
              </main>
              <script>
                const filter = document.querySelector('#filter');
                const rows = [...document.querySelectorAll('.test-row')];
                filter.addEventListener('input', () => {
                  const query = filter.value.trim().toLowerCase();
                  for (const row of rows) row.hidden = !row.dataset.search.includes(query);
                });
              </script>
            </body>
            </html>
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static TimeSpan ParseDuration(string? value)
        => TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration) ? duration : TimeSpan.Zero;

    private static DateTimeOffset? ParseDate(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date
            : null;

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalSeconds < 1
            ? $"{duration.TotalMilliseconds:0} ms"
            : $"{duration.TotalSeconds:0.00} s";

    private static string GetStatusClass(string outcome)
        => outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase)
            ? "passed"
            : outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                || outcome.Equals("Error", StringComparison.OrdinalIgnoreCase)
                ? "failed"
                : "skipped";

    private static string GetStatusLabel(string outcome)
        => outcome.ToLowerInvariant() switch
        {
            "passed" => "Correcta",
            "failed" or "error" => "Fallida",
            "notexecuted" or "inconclusive" or "aborted" => "Omitida",
            _ => outcome
        };

    private static string GetErrorSummary(string error)
    {
        var summary = error.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? "";

        if (summary.Contains("libatk-1.0.so.0", StringComparison.OrdinalIgnoreCase))
        {
            return "Chromium no pudo iniciar: falta la dependencia del sistema libatk.";
        }

        return summary.Length > 180 ? summary[..177] + "..." : summary;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private sealed record TestResult(string Name, string Outcome, TimeSpan Duration, string Error);
}