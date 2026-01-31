using System.Text;
using Microsoft.Extensions.Configuration;
using Deque.AxeCore.Commons;

namespace BlazorA11yDemo.Tests;

public class AccessibilityTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private readonly string _baseUrl;

    public AccessibilityTests()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        _baseUrl = config["BaseUrl"] ?? "http://localhost:5000";
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    /// <summary>
    /// Blazor標準テンプレートのページ一覧
    /// </summary>
    public static TheoryData<string, string> TargetPages => new()
    {
        { "/", "Home" },
        { "/counter", "Counter" },
        { "/weather", "Weather" },
    };

    [Theory]
    [MemberData(nameof(TargetPages))]
    public async Task Page_ShouldHaveNoAccessibilityViolations(string path, string pageName)
    {
        // 1. ページに遷移
        await _page.GotoAsync($"{_baseUrl}{path}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 2. axe-coreでアクセシビリティ検査を実行
        var options = new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions
            {
                Type = "tag",
                Values = ["wcag2a", "wcag2aa", "wcag21aa"]
            }
        };

        var result = await _page.RunAxe(options);

        // 3. 違反があればテストを失敗させる
        if (result.Violations.Length > 0)
        {
            var message = FormatViolations(pageName, path, result.Violations);
            Assert.Fail(message);
        }
    }

    [Fact]
    public async Task Counter_AfterInteraction_ShouldBeAccessible()
    {
        // 1. Counterページに遷移
        await _page.GotoAsync($"{_baseUrl}/counter");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 2. ボタンを数回クリックしてUIを変化させる
        var button = _page.Locator("button", new() { HasText = "Click me" });
        await button.ClickAsync();
        await button.ClickAsync();
        await button.ClickAsync();

        // 3. 状態変化後もアクセシビリティを検査
        var options = new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions
            {
                Type = "tag",
                Values = ["wcag2a", "wcag2aa", "wcag21aa"]
            }
        };

        var result = await _page.RunAxe(options);

        if (result.Violations.Length > 0)
        {
            var message = FormatViolations("Counter（操作後）", "/counter", result.Violations);
            Assert.Fail(message);
        }
    }

    private static string FormatViolations(string pageName, string path, AxeResultItem[] violations)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"♿ {pageName} ({path}) でアクセシビリティ違反が {violations.Length} 件見つかりました：");
        sb.AppendLine();

        foreach (var violation in violations)
        {
            sb.AppendLine($"【{violation.Impact}】{violation.Id}");
            sb.AppendLine($"  説明: {violation.Description}");
            sb.AppendLine($"  ヘルプ: {violation.HelpUrl}");

            foreach (var node in violation.Nodes.Take(3))
            {
                sb.AppendLine($"  - 要素: {node.Html}");
            }

            if (violation.Nodes.Length > 3)
            {
                sb.AppendLine($"  ... 他 {violation.Nodes.Length - 3} 件");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
