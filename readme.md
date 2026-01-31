---
title: "WebアクセシビリチE��をCI/CDで拁E��すめE Eaxe DevTools ÁEPlaywright C#実践ガイチE
emoji: "♿"
type: "tech" # tech: 技術記亁E/ idea: アイチE��
topics: ["accessibility", "playwright", "csharp", "staticwebapps", "azure"]
published: false
---

# はじめに

前回の記事「[WebアクセシビリチE��は"もしめEに備える設訁E(https://zenn.dev/tomokusaba/articles/93810f232cec91)」では、アクセシビリチE��の老E��方めE��計指針につぁE��解説しました🧭
今回はそ�E実践編として、E*CI/CDパイプラインでアクセシビリチE��を�E動検査する仕絁E��**を構築してぁE��ます🔧

本記事では、E*Blazor WebAssembly** めE**Azure Static Web Apps** にホストする構�Eを題材に、E*環墁E��築からGitHub Actionsでの自動化まで**を一気通貫で実裁E��ます🚀

# 今回のゴール

以下�E流れを実現します🎯

1. 📦 Playwright C# + axe-coreでアクセシビリチE��チE��トを書ぁE
2. 🔄 GitHub ActionsでPRごとに自動実行すめE
3. 💬 違反があれ�EPRにコメントで通知する

# 前提条件

- ✁E.NET 9 SDK がインスト�Eル済み
- ✁EVisual Studio 2022 また�E VS Code
- ✁EGitHub リポジトリがあめE
- ✁EAzure サブスクリプション�E�Etatic Web AppsチE�Eロイ用�E�E
- ✁ESWA CLI 2.0.2以上！Enpm install -g @azure/static-web-apps-cli`�E�E

:::message alert
**SWA CLI バ�Eジョンに関する重要な注愁E* ⚠�E�E

Microsoft は SWA CLI のセキュリチE��強化�Eため、バージョン 2.0.2 以上へのアチE�Eグレードを忁E��としてぁE��す、E
古ぁE��ージョンを使用してぁE��場合�E、忁E��最新版にアチE�EチE�Eトしてください、E

```powershell
npm install -g @azure/static-web-apps-cli@latest
```
:::

# なぜCI/CDでアクセシビリチE��をチェチE��するのか！E

手動チE��トだけでは抜け漏れが発生しがちです😮

| 課顁E| CI/CDで解決 |
|------|------------|
| ⏰ 全ペ�Eジを手動でチェチE��する時間がなぁE| 自動で全ペ�Eジを検査 |
| 🔄 機�E追加時に既存�Ea11yが壊れめE| 回帰を即座に検�E |
| 🧠 拁E��老E�E知識に依存すめE| ルールベ�Eスで一貫した検査 |

ただし、E*自動テストで検�Eできるのは紁E0、E0%** です🧭
代替チE��スト�E「�E容」が適刁E��、キーボ�Eド操作�E「体験」が自然か、などは人間�E判断が忁E��です、E
本記事では**自動で潰せるも�Eを確実に潰す仕絁E��**を作ります🎯

# Step 1: プロジェクト�EセチE��アチE�E

## 1.1 チE��ト�Eロジェクト�E作�E

```powershell
# 新しいソリューションを作�E
mkdir BlazorA11yDemo
cd BlazorA11yDemo
dotnet new sln

# Blazor WebAssemblyアプリを作�E�E�Etatic Web Apps対応！E
dotnet new blazorwasm -n BlazorA11yDemo.Client -f net9.0
dotnet sln add BlazorA11yDemo.Client

# チE��ト�Eロジェクトを作�E
dotnet new xunit -n BlazorA11yDemo.Tests -f net9.0
dotnet sln add BlazorA11yDemo.Tests

# 忁E��なパッケージをインスト�Eル
cd BlazorA11yDemo.Tests
dotnet add package Microsoft.Playwright
dotnet add package Deque.AxeCore.Playwright
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables

# ビルドしてPlaywrightブラウザをインスト�Eル
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium
```

:::message
**なぜBlazor WebAssemblyを選ぶのか！E* 🤁E

Azure Static Web Appsは静的ファイルホスチE��ングに特化しており、Blazor WebAssembly�E�クライアントサイド）との相性が抜群です。サーバ�Eレスでグローバル配信でき、無料�Eランもあります、E
:::

## 1.2 プロジェクト構�E

最終的なプロジェクト構�Eは以下�Eとおりです📁E

```
BlazorA11yDemo/
├── BlazorA11yDemo.sln
├── BlazorA11yDemo.Client/        # Blazor WebAssembly�E�Etatic Web Apps対応！E
━E  ├── Pages/
━E  ━E  ├── Home.razor            # / (ホ�Eム)
━E  ━E  ├── Counter.razor         # /counter (カウンター)
━E  ━E  └── Weather.razor         # /weather (天気予報)
━E  ├── wwwroot/
━E  └── Program.cs
├── BlazorA11yDemo.Tests/
━E  ├── BlazorA11yDemo.Tests.csproj
━E  ├── GlobalUsings.cs
━E  ├── AccessibilityTests.cs     # チE��トコーチE
━E  └── appsettings.json
├── swa-cli.config.json           # SWA CLI設宁E
└── .github/
    └── workflows/
        └── azure-static-web-apps.yml
```

# Step 2: チE��トコード�E実裁E

## 2.1 GlobalUsings.cs

よく使ぁE��前空間をまとめておきます🧩

```csharp
global using Xunit;
global using Microsoft.Playwright;
global using Deque.AxeCore.Playwright;
global using Deque.AxeCore.Commons;
```

## 2.2 appsettings.json

チE��ト対象のURLを設定ファイルで管琁E��ます📁E
ポ�Eト番号は `BlazorA11yDemo.Client/Properties/launchSettings.json` の `applicationUrl` に合わせてください、E

```json
{
  "BaseUrl": "http://localhost:5212"
}
```

:::message
**ポ�Eト番号の確認方況E* 🔍

`launchSettings.json` の `profiles` ↁE`http` また�E `https` の `applicationUrl` を確認してください、E
チE��プレート作�E時にランダムなポ�Eトが割り当てられます、E
:::

## 2.3 swa-cli.config.json�E�リポジトリルートに配置�E�E

SWA CLIの設定ファイルを作�Eします🛠�E�E

```json
{
  "$schema": "https://aka.ms/azure/static-web-apps-cli/schema",
  "configurations": {
    "blazor-a11y": {
      "appLocation": "BlazorA11yDemo.Client",
      "outputLocation": "bin/Release/net9.0/publish/wwwroot",
      "appBuildCommand": "dotnet publish -c Release",
      "run": "dotnet watch run",
      "appDevserverUrl": "http://localhost:5000"
    }
  }
}
```

## 2.4 AccessibilityTests.cs

チE��トコードを1ファイルにまとめます🎯

```csharp
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
    /// Blazor標準テンプレート�Eペ�Eジ一覧
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
        // 1. ペ�Eジに遷移
        await _page.GotoAsync($"{_baseUrl}{path}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 2. axe-coreでアクセシビリチE��検査を実衁E
        var options = new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions
            {
                Type = "tag",
                Values = ["wcag2a", "wcag2aa", "wcag21aa"]
            }
        };

        var result = await _page.RunAxe(options);

        // 3. 違反があれ�EチE��トを失敗させる
        if (result.Violations.Length > 0)
        {
            var message = FormatViolations(pageName, path, result.Violations);
            Assert.Fail(message);
        }
    }

    [Fact]
    public async Task Counter_AfterInteraction_ShouldBeAccessible()
    {
        // 1. Counterペ�Eジに遷移
        await _page.GotoAsync($"{_baseUrl}/counter");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 2. ボタンを数回クリチE��してUIを変化させめE
        var button = _page.Locator("button", new() { HasText = "Click me" });
        await button.ClickAsync();
        await button.ClickAsync();
        await button.ClickAsync();

        // 3. 状態変化後もアクセシビリチE��を検査
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
            var message = FormatViolations("Counter�E�操作後！E, "/counter", result.Violations);
            Assert.Fail(message);
        }
    }

    private static string FormatViolations(string pageName, string path, AxeResultItem[] violations)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"♿ {pageName} ({path}) でアクセシビリチE��違反ぁE{violations.Length} 件見つかりました�E�E);
        sb.AppendLine();

        foreach (var violation in violations)
        {
            sb.AppendLine($"【{violation.Impact}】{violation.Id}");
            sb.AppendLine($"  説昁E {violation.Description}");
            sb.AppendLine($"  ヘルチE {violation.HelpUrl}");

            foreach (var node in violation.Nodes.Take(3))
            {
                sb.AppendLine($"  - 要素: {node.Html}");
            }

            if (violation.Nodes.Length > 3)
            {
                sb.AppendLine($"  ... 仁E{violation.Nodes.Length - 3} 件");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
```

## 2.5 ローカルでチE��トを実衁E

### 方況E: dotnet run で直接起動（シンプル�E�E

開発中はこちらが手軽です🚀

```powershell
# Blazor WASMを起動（別ターミナル�E�E
cd BlazorA11yDemo.Client
dotnet run

# チE��トを実行（別ターミナル�E�E
# ※ appsettings.json の BaseUrl めElaunchSettings.json のポ�Eトに合わせてください
cd BlazorA11yDemo.Tests
dotnet test
```

### 方況E: SWA CLI でエミュレート（本番に近い環墁E��E

認証めE��ーチE��ングなどSWAの機�Eを確認したい場合�Eこちら🔧

```powershell
# Blazor WASMをビルド（リポジトリルートで実行！E
cd BlazorA11yDemo.Client
dotnet publish -c Release

# SWA CLIでローカルサーバ�Eを起動（別ターミナル、�EーチE280�E�E
cd ..
swa start BlazorA11yDemo.Client/bin/Release/net9.0/publish/wwwroot

# チE��トを実行（別ターミナル�E�E
# ※ appsettings.json の BaseUrl めEhttp://localhost:4280 に変更
cd BlazorA11yDemo.Tests
dotnet test
```

:::message
**SWA CLIとは�E�E* 💡

Azure Static Web Apps CLIは、ローカルでSWA環墁E��エミュレートするツールです、E
認証、ルーチE��ング、API統合など、本番環墁E��同じ動作をローカルで確認できます、E
チE��ォルト�Eート�E `4280` です、E
:::

# Step 3: GitHub Actionsでの自動化

Azure Static Web AppsへのチE�Eロイと、アクセシビリチE��チE��トを同時に実行するワークフローを作�Eします🛠�E�E

## 3.1 ワークフローファイルの作�E

`.github/workflows/azure-static-web-apps.yml` を作�Eします、E

```yaml
name: Azure Static Web Apps CI/CD

on:
  push:
    branches: [main]
  pull_request:
    types: [opened, synchronize, reopened, closed]
    branches: [main]

env:
  DOTNET_VERSION: '9.0.x'

jobs:
  # ─────────────────────────────────────────────
  # アクセシビリチE��チE��ト！ERごとに実行！E
  # ─────────────────────────────────────────────
  accessibility_test:
    if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed')
    runs-on: windows-latest
    
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install SWA CLI
        run: npm install -g @azure/static-web-apps-cli

      - name: Restore & Build
        run: dotnet build

      - name: Publish Blazor WASM
        run: dotnet publish BlazorA11yDemo.Client -c Release

      - name: Install Playwright
        run: pwsh BlazorA11yDemo.Tests/bin/Debug/net9.0/playwright.ps1 install chromium

      - name: Start SWA Emulator
        run: |
          # SWA CLIでポート4280でサーブ（CI環境では固定ポートを使用）
          npx --yes @azure/static-web-apps-cli start BlazorA11yDemo.Client/bin/Release/net9.0/publish/wwwroot --port 4280 &
          sleep 15
        shell: bash

      - name: Run Accessibility Tests
        run: dotnet test BlazorA11yDemo.Tests --no-build --logger "trx;LogFileName=results.trx"
        env:
          BaseUrl: 'http://localhost:4280'  # SWA CLIのポ�Eトに合わせる

      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: accessibility-results
          path: BlazorA11yDemo.Tests/TestResults/

      - name: Comment on PR (on failure)
        if: failure() && github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: '## ♿ アクセシビリチE��チE��トが失敗しました\n\n[Actionsの結果を確認](${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }})'
            })

  # ─────────────────────────────────────────────
  # Static Web Appsへデプロイ
  # ─────────────────────────────────────────────
  deploy:
    if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed')
    runs-on: ubuntu-latest
    needs: accessibility_test  # テスト成功後にデプロイ
    name: Deploy to SWA
    
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Publish Blazor WASM
        run: dotnet publish BlazorA11yDemo.Client -c Release -o publish

      - name: Build And Deploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "publish/wwwroot"
          skip_app_build: true

  # ─────────────────────────────────────────────
  # PRクローズ時にプレビュー環境を削除
  # ─────────────────────────────────────────────
  close_pull_request:
    if: github.event_name == 'pull_request' && github.event.action == 'closed'
    runs-on: ubuntu-latest
    name: Close Pull Request
    
    steps:
      - name: Close Pull Request
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          action: "close"
```

:::message
**ワークフローのポインチE* 🎯

1. **accessibility_test**: PRごとにアクセシビリチE��チE��トを実行！Eindows�E�E
2. **deploy**: チE��ト�E功後にStatic Web AppsへチE�Eロイ�E�Einux�E�E
3. **close_pull_request**: PRマ�Eジ/クローズ時にプレビュー環墁E��削除

チE��トが失敗すると `needs: accessibility_test` によりチE�EロイがブロチE��されます！E
:::

## 3.2 Azure Static Web AppsのセチE��アチE�E

Azure PortalでStatic Web Appsリソースを作�Eし、`AZURE_STATIC_WEB_APPS_API_TOKEN`を取得してGitHub Secretsに設定します🔁E

1. Azure Portal ↁEStatic Web Apps ↁE作�E
2. GitHubリポジトリを連携
3. チE�Eロイト�Eクンを取征E
4. GitHub ↁESettings ↁESecrets ↁE`AZURE_STATIC_WEB_APPS_API_TOKEN` を追加

# Step 4: 段階的な導�E戦略

ぁE��なり�Eての違反でCIを止めるのは現実的ではありません🧭

## 段階的なロールアウチE

| Phase | 期間 | 設宁E|
|-------|------|------|
| 📊 可視化 | 最初�E2週閁E| 違反を記録するがCIは落とさなぁE|
| ⚠�E�E重大のみ | 3、E週目 | Critical/SeriousのみブロチE�� |
| 🛡�E�E全違反 | 5週目以陁E| 全ての違反でCIを止める |

Phase 1では、テスト�E最後に `|| true` を追加してCIを落とさなぁE��ぁE��します！E

```yaml
- name: Run Accessibility Tests
  run: dotnet test BlazorA11yDemo.Tests --no-build || true
```

# よくある違反と修正方況E

チE��トを実行すると、よく以下�E違反が検�Eされます🔁E

## color-contrast�E�コントラスト不足�E�E

```html
<!-- NG -->
<p style="color: #999;">薁E��グレー</p>

<!-- OK: 4.5:1以上�EコントラスチE-->
<p style="color: #595959;">読みめE��ぁE��レー</p>
```

## image-alt�E�代替チE��スト欠落�E�E

```html
<!-- NG -->
<img src="product.jpg">

<!-- OK -->
<img src="product.jpg" alt="啁E��吁E サンプル啁E��">
```

## label�E�フォームラベル欠落�E�E

```html
<!-- NG -->
<input type="email" placeholder="メールアドレス">

<!-- OK -->
<label for="email">メールアドレス</label>
<input type="email" id="email">
```

## button-name�E��Eタン名欠落�E�E

```html
<!-- NG -->
<button><svg>...</svg></button>

<!-- OK -->
<button aria-label="メニューを開ぁE><svg>...</svg></button>
```

# まとめE

本記事では、E*Blazor WebAssembly + Azure Static Web Apps**を題材に、E*Playwright C# + axe-core + GitHub Actions**でアクセシビリチE��を�E動検査する仕絁E��を構築しました🎯

## 実裁E��たこと

1. ✁EBlazor WebAssembly めEStatic Web Apps にホスチE
2. ✁ESWA CLI でローカルエミュレーション
3. ✁EPlaywright C# + axe-core で WCAG 2.1 AA 検査
4. ✁EチE��ト失敗時はチE�EロイをブロチE��
5. ✁EPRごとにプレビュー環墁E��自動作�E

## 次のスチE��チE

- 🔧 認証が忁E��なペ�EジのチE��ト追加
- 📊 チE��ト結果のダチE��ュボ�Eド化
- 🧪 手動チE��トとの絁E��合わぁE

「�E動で潰せるも�Eは自動で潰し、人間�E判断が忁E��なも�Eに雁E��する」🤁E
これがCI/CDでアクセシビリチE��を担保する意義です♿

---

## 参老E��ンク

### Azure Static Web Apps
- [Azure Static Web Apps ドキュメンチE(https://learn.microsoft.com/ja-jp/azure/static-web-apps/)
- [Deploy a Blazor app on Azure Static Web Apps](https://learn.microsoft.com/azure/static-web-apps/deploy-blazor)
- [Set up local development for Azure Static Web Apps](https://learn.microsoft.com/azure/static-web-apps/local-development)
- [SWA CLI - npm](https://www.npmjs.com/package/@azure/static-web-apps-cli)

### Blazor
- [Tooling for ASP.NET Core Blazor](https://learn.microsoft.com/aspnet/core/blazor/tooling)
- [ASP.NET Core Blazor project structure](https://learn.microsoft.com/aspnet/core/blazor/project-structure)

### アクセシビリチE��チE��チE
- [Deque.AxeCore.Playwright - NuGet](https://www.nuget.org/packages/Deque.AxeCore.Playwright)
- [axe-core NuGet packages - GitHub](https://github.com/dequelabs/axe-core-nuget)
- [axe-core Rules](https://github.com/dequelabs/axe-core/blob/master/doc/rule-descriptions.md)
- [Playwright for .NET](https://playwright.dev/dotnet/)

### アクセシビリチE��基溁E
- [WCAG 2.1 達�E基準（日本語）](https://waic.jp/docs/WCAG21/)
- [Web Content Accessibility Guidelines - Microsoft Compliance](https://learn.microsoft.com/compliance/regulatory/offering-wcag-2-1)

### 関連記亁E
- [前回の記亁E WebアクセシビリチE��は"もしめEに備える設訁E(https://zenn.dev/tomokusaba/articles/93810f232cec91)

