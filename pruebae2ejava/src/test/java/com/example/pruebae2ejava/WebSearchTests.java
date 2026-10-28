package com.example.pruebae2ejava;

import java.net.URI;
import java.time.Duration;
import java.util.List;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.openqa.selenium.By;
import org.openqa.selenium.TimeoutException;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;
import org.openqa.selenium.chrome.ChromeDriver;
import org.openqa.selenium.chrome.ChromeOptions;
import org.openqa.selenium.support.ui.WebDriverWait;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class WebSearchTests {
    private WebDriver driver;
    private WebDriverWait wait;

    @BeforeEach
    void startBrowser() {
        ChromeOptions options = new ChromeOptions();
        options.setBrowserVersion("stable");
        options.addArguments("--headless=new", "--no-sandbox", "--disable-dev-shm-usage");
        driver = new ChromeDriver(options);
        wait = new WebDriverWait(driver, Duration.ofSeconds(30));
    }

    @AfterEach
    void closeBrowser() {
        if (driver != null) {
            driver.quit();
        }
    }

    @Test
    void buscarEnGoogleMuestraResultados() {
        String searchTerm = System.getenv().getOrDefault("GOOGLE_SEARCH_QUERY", "Playwright .NET");
        driver.get("https://www.google.com");

        WebElement searchBox = wait.until(current -> {
            List<WebElement> boxes = current.findElements(By.cssSelector("textarea[name='q'], input[name='q']"));
            return boxes.stream().filter(WebElement::isDisplayed).findFirst().orElse(null);
        });
        searchBox.sendKeys(searchTerm);
        searchBox.submit();

        wait.until(current -> {
            String path = URI.create(current.getCurrentUrl()).getPath();
            return path.equals("/search") || path.startsWith("/sorry");
        });

        String resultPath = URI.create(driver.getCurrentUrl()).getPath();
        assertEquals("/search", resultPath,
                "Google respondió " + resultPath + " en vez de /search. Puede estar limitando la automatización.");

        try {
            WebElement firstResult = wait.until(current -> {
                List<WebElement> results = current.findElements(By.cssSelector("#search h3"));
                return results.stream().filter(WebElement::isDisplayed).findFirst().orElse(null);
            });
            assertTrue(firstResult.isDisplayed(), "El primer resultado de Google debe estar visible.");
        } catch (TimeoutException exception) {
            String body = driver.findElement(By.tagName("body")).getText();
            String pageSummary = body.substring(0, Math.min(body.length(), 2000));
            throw new AssertionError("Google no mostró resultados. URL: " + driver.getCurrentUrl()
                    + "; título: " + driver.getTitle() + System.lineSeparator() + pageSummary, exception);
        }
    }

    @Test
    void buscarVideoEnYouTubeMuestraResultados() {
        String searchTerm = System.getenv().getOrDefault("YOUTUBE_SEARCH_QUERY", "Playwright tutorial");
        driver.get("https://www.youtube.com");

        WebElement searchBox = wait.until(current -> {
            List<WebElement> boxes = current.findElements(By.cssSelector("input#search, input[name='search_query']"));
            return boxes.stream().filter(WebElement::isDisplayed).findFirst().orElse(null);
        });
        searchBox.sendKeys(searchTerm);
        searchBox.submit();

        wait.until(current -> URI.create(current.getCurrentUrl()).getPath().equals("/results"));

        WebElement firstVideo = wait.until(current -> {
            List<WebElement> videos = current.findElements(By.cssSelector("ytd-video-renderer a#video-title"));
            return videos.stream().filter(WebElement::isDisplayed).findFirst().orElse(null);
        });
        assertTrue(firstVideo.isDisplayed(), "Debe aparecer al menos un video visible.");
        assertEquals("/watch", URI.create(firstVideo.getAttribute("href")).getPath(),
            "El resultado debe enlazar a un video.");
    }
}