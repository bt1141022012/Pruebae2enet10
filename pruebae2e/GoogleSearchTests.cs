using Microsoft.Playwright;

namespace GoogleSearchE2E;

public class GoogleSearchTests
{
    [Test]
    public async Task Buscar_en_Google_muestra_resultados()
    {
        var searchTerm = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_QUERY") ?? "Playwright .NET";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync("https://www.google.com", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        var searchBox = page.Locator("textarea[name='q'], input[name='q']").First;
        await searchBox.WaitForAsync();
        await searchBox.FillAsync(searchTerm);
        await searchBox.PressAsync("Enter");

        await page.WaitForURLAsync(url =>
        {
            var uri = new Uri(url);
            return uri.AbsolutePath == "/search" || uri.AbsolutePath.StartsWith("/sorry", StringComparison.Ordinal);
        });

        var resultUri = new Uri(page.Url);
        Assert.That(
            resultUri.AbsolutePath,
            Is.EqualTo("/search"),
            $"Google respondió '{resultUri.AbsolutePath}' en vez de '/search'. Puede estar bloqueando la solicitud o pidiendo verificación. Título: {await page.TitleAsync()}");

        var firstResult = page.Locator("#search h3").First;
        try
        {
            await firstResult.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        }
        catch (TimeoutException exception)
        {
            var bodyText = await page.Locator("body").InnerTextAsync();
            var pageSummary = bodyText[..Math.Min(bodyText.Length, 2000)];
            throw new AssertionException(
                $"Google no mostró un resultado visible. URL: {page.Url}; título: {await page.TitleAsync()}.{Environment.NewLine}{pageSummary}",
                exception);
        }

        Assert.That(await firstResult.IsVisibleAsync(), Is.True);
    }

    [Test]
    public async Task Buscar_video_en_YouTube_muestra_resultados()
    {
        var searchTerm = Environment.GetEnvironmentVariable("YOUTUBE_SEARCH_QUERY") ?? "Playwright tutorial";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync("https://www.youtube.com", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        var searchBox = page.Locator("input#search, input[name='search_query']").First;
        await searchBox.WaitForAsync();
        await searchBox.FillAsync(searchTerm);
        await searchBox.PressAsync("Enter");

        await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/results");

        var firstVideo = page.Locator("ytd-video-renderer a#video-title").First;
        await firstVideo.WaitForAsync();
        Assert.That(await firstVideo.IsVisibleAsync(), Is.True);
        Assert.That(await firstVideo.GetAttributeAsync("href"), Does.StartWith("/watch"));
    }
}