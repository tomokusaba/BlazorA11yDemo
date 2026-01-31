---
title: "WebアクセシビリティをCI/CDで担保する ― axe DevTools × Playwright C#実践ガイド"
emoji: "♿"
type: "tech" # tech: 技術記事 / idea: アイデア
topics: ["accessibility", "playwright", "csharp", "staticwebapps", "azure"]
published: false
---

# はじめに

前回の記事「[Webアクセシビリティは"もしも"に備える設計](https://zenn.dev/tomokusaba/articles/93810f232cec91)」では、アクセシビリティの考え方や設計指針について解説しました🧭
今回はその実践編として、**CI/CDパイプラインでアクセシビリティを自動検査する仕組み**を構築していきます🔧

本記事では、**Blazor WebAssembly** を **Azure Static Web Apps** にホストする構成を題材に、**環境構築からGitHub Actionsでの自動化まで**を一気通貫で実装します🚀

# 今回のゴール

以下の流れを実現します🎯

1. 📦 Playwright C# + axe-coreでアクセシビリティテストを書く
2. 🔄 GitHub ActionsでPRごとに自動実行する
3. 📊 違反があればGitHub Actions Summaryに出力する（CIは止めない）

# 前提条件

- ✅ .NET 9 SDK がインストール済み
- ✅ Visual Studio 2022 または VS Code
- ✅ GitHub リポジトリがある
- ✅ Azure サブスクリプション（Static Web Appsデプロイ用）

:::message
**注意**: CI環境では `npx serve` を使用してBlazor WASMを配信します。SWA CLIはローカル開発でのみ使用します。
:::

# なぜCI/CDでアクセシビリティをチェックするのか？

手動テストだけでは抜け漏れが発生しがちです😮

| 課題 | CI/CDで解決 |
|------|------------|
| ⏰ 全ページを手動でチェックする時間がない | 自動で全ページを検査 |
| 🔄 機能追加時に既存のa11yが壊れる | 回帰を即座に検出 |
| 🧠 担当者の知識に依存する | ルールベースで一貫した検査 |

ただし、**自動テストで検出できるのは約30〜40%** です🧭
代替テキストの「内容」が適切か、キーボード操作の「体験」が自然か、などは人間の判断が必要です。
本記事では**自動で潰せるものを確実に潰す仕組み**を作ります🎯

# Step 1: プロジェクトのセットアップ

## 1.1 テストプロジェクトの作成

```powershell
# 新しいソリューションを作成
mkdir BlazorA11yDemo
cd BlazorA11yDemo
dotnet new sln

# Blazor WebAssemblyアプリを作成（Static Web Apps対応）
dotnet new blazorwasm -n BlazorA11yDemo.Client -f net9.0
dotnet sln add BlazorA11yDemo.Client

# テストプロジェクトを作成
dotnet new xunit -n BlazorA11yDemo.Tests -f net9.0
dotnet sln add BlazorA11yDemo.Tests

# 必要なパッケージをインストール
cd BlazorA11yDemo.Tests
dotnet add package Microsoft.Playwright
dotnet add package Deque.AxeCore.Playwright
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables

# ビルドしてPlaywrightブラウザをインストール
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium
```

:::message
**なぜBlazor WebAssemblyを選ぶのか？** 🤔

Azure Static Web Appsは静的ファイルホスティングに特化しており、Blazor WebAssembly（クライアントサイド）との相性が抜群です。サーバーレスでグローバル配信でき、無料プランもあります。
:::

## 1.2 プロジェクト構成

最終的なプロジェクト構成は以下のとおりです📁

```
BlazorA11yDemo/
├── BlazorA11yDemo.sln
├── BlazorA11yDemo.Client/        # Blazor WebAssembly（Static Web Apps対応）
│   ├── Pages/
│   │   ├── Home.razor            # / (ホーム)
│   │   ├── Counter.razor         # /counter (カウンター)
│   │   └── Weather.razor         # /weather (天気予報)
│   ├── wwwroot/
│   └── Program.cs
├── BlazorA11yDemo.Tests/
│   ├── BlazorA11yDemo.Tests.csproj
│   ├── GlobalUsings.cs
│   ├── AccessibilityTests.cs     # テストコード
│   └── appsettings.json
├── swa-cli.config.json           # SWA CLI設定
└── .github/
    └── workflows/
        └── azure-static-web-apps.yml
```

# Step 2: テストコードの実装

## 2.1 GlobalUsings.cs

よく使う名前空間をまとめておきます🧩

```csharp
global using Xunit;
global using Microsoft.Playwright;
global using Deque.AxeCore.Playwright;
global using Deque.AxeCore.Commons;
```

## 2.2 appsettings.json

テスト対象のURLを設定ファイルで管理します📝
ポート番号は `BlazorA11yDemo.Client/Properties/launchSettings.json` の `applicationUrl` に合わせてください。

```json
{
  "BaseUrl": "http://localhost:5212"
}
```

:::message
**ポート番号の確認方法** 🔍

`launchSettings.json` の `profiles` → `http` または `https` の `applicationUrl` を確認してください。
テンプレート作成時にランダムなポートが割り当てられます。
:::

## 2.3 swa-cli.config.json（リポジトリルートに配置）

SWA CLIの設定ファイルを作成します🛠️

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

テストコードを1ファイルにまとめます🎯

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
```

## 2.5 ローカルでテストを実行

### 方法1: dotnet run で直接起動（シンプル）

開発中はこちらが手軽です🚀

```powershell
# Blazor WASMを起動（別ターミナル）
cd BlazorA11yDemo.Client
dotnet run

# テストを実行（別ターミナル）
# ※ appsettings.json の BaseUrl を launchSettings.json のポートに合わせてください
cd BlazorA11yDemo.Tests
dotnet test
```

### 方法2: SWA CLI でエミュレート（本番に近い環境）

認証やルーティングなどSWAの機能を確認したい場合はこちら🔧

```powershell
# Blazor WASMをビルド（リポジトリルートで実行）
cd BlazorA11yDemo.Client
dotnet publish -c Release

# SWA CLIでローカルサーバーを起動（別ターミナル、ポート4280）
cd ..
swa start BlazorA11yDemo.Client/bin/Release/net9.0/publish/wwwroot

# テストを実行（別ターミナル）
# ※ appsettings.json の BaseUrl を http://localhost:4280 に変更
cd BlazorA11yDemo.Tests
dotnet test
```

:::message
**SWA CLIとは？** 💡

Azure Static Web Apps CLIは、ローカルでSWA環境をエミュレートするツールです。
認証、ルーティング、API統合など、本番環境と同じ動作をローカルで確認できます。
デフォルトポートは `4280` です。
:::

# Step 3: GitHub Actionsでの自動化

Azure Static Web Appsへのデプロイと、アクセシビリティテストを同時に実行するワークフローを作成します🛠️

## 3.1 ワークフローファイルの作成

`.github/workflows/azure-static-web-apps.yml` を作成します。

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
  # ビルド＆アクセシビリティテスト（PRごとに実行）
  # ─────────────────────────────────────────────
  build_and_test:
    if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed')
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore & Build
        run: dotnet build

      - name: Publish Blazor WASM
        run: dotnet publish BlazorA11yDemo.Client -c Release -o ./publish

      - name: Verify publish output
        run: |
          echo "=== Publish output contents ==="
          ls -la ./publish/wwwroot/
          echo "=== Checking index.html (first 20 lines) ==="
          head -20 ./publish/wwwroot/index.html

      - name: Install Playwright
        run: pwsh BlazorA11yDemo.Tests/bin/Debug/net9.0/playwright.ps1 install chromium --with-deps

      - name: Start HTTP Server
        run: |
          # npx serveでBlazor WASMをサーブ（ポート4280）
          echo "Starting HTTP server on port 4280..."
          npx --yes serve ./publish/wwwroot -l 4280 &
          SERVER_PID=$!
          echo "Server PID: $SERVER_PID"
          
          # サーバー起動を待機
          echo "Waiting for server to start..."
          for i in {1..30}; do
            if curl -s -o /dev/null -w "%{http_code}" http://localhost:4280 | grep -q "200"; then
              echo "✅ Server is ready! (attempt $i)"
              break
            fi
            echo "Waiting... (attempt $i)"
            sleep 1
          done
          
          # 最終確認
          curl -I http://localhost:4280 || echo "⚠️ Server may not be fully ready"

      - name: Run Accessibility Tests
        id: a11y_test
        continue-on-error: true  # テスト失敗でもCIを止めない
        run: |
          dotnet test BlazorA11yDemo.Tests --no-build \
            --logger "trx;LogFileName=results.trx" \
            --logger "console;verbosity=detailed" \
            2>&1 | tee test-output.txt
          echo "TEST_EXIT_CODE=$?" >> $GITHUB_ENV
        env:
          BaseUrl: 'http://localhost:4280'

      - name: Generate Test Summary
        if: always()
        run: |
          echo "## ♿ アクセシビリティテスト結果" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          
          if [ -f test-output.txt ]; then
            if grep -q "アクセシビリティ違反" test-output.txt; then
              echo "### ⚠️ 違反が検出されました" >> $GITHUB_STEP_SUMMARY
              echo "" >> $GITHUB_STEP_SUMMARY
              echo '```' >> $GITHUB_STEP_SUMMARY
              grep -A 50 "アクセシビリティ違反" test-output.txt | head -100 >> $GITHUB_STEP_SUMMARY
              echo '```' >> $GITHUB_STEP_SUMMARY
            elif grep -q "Passed:" test-output.txt; then
              echo "### ✅ すべてのテストに合格しました" >> $GITHUB_STEP_SUMMARY
              grep "Passed:" test-output.txt >> $GITHUB_STEP_SUMMARY
            else
              echo "### 📋 テスト出力" >> $GITHUB_STEP_SUMMARY
              echo '```' >> $GITHUB_STEP_SUMMARY
              tail -50 test-output.txt >> $GITHUB_STEP_SUMMARY
              echo '```' >> $GITHUB_STEP_SUMMARY
            fi
          fi

      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: accessibility-results
          path: |
            BlazorA11yDemo.Tests/TestResults/
            test-output.txt

      - name: Upload publish output for deploy
        uses: actions/upload-artifact@v4
        with:
          name: publish-output
          path: ./publish/wwwroot/

  # ─────────────────────────────────────────────
  # Static Web Appsへデプロイ
  # ─────────────────────────────────────────────
  deploy:
    if: |
      (github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed'))
      && vars.ENABLE_DEPLOY == 'true'
    runs-on: ubuntu-latest
    needs: build_and_test
    name: Deploy to SWA
    
    steps:
      - uses: actions/checkout@v4

      - name: Download publish output
        uses: actions/download-artifact@v4
        with:
          name: publish-output
          path: ./publish/wwwroot/

      - name: Build And Deploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_GRAY_MEADOW_072FA5010 }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "./publish/wwwroot"
          skip_app_build: true

  # ─────────────────────────────────────────────
  # PRクローズ時にプレビュー環境を削除
  # ─────────────────────────────────────────────
  close_pull_request:
    if: github.event_name == 'pull_request' && github.event.action == 'closed' && vars.ENABLE_DEPLOY == 'true'
    runs-on: ubuntu-latest
    name: Close Pull Request
    
    steps:
      - name: Close Pull Request
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_GRAY_MEADOW_072FA5010 }}
          action: "close"
```

:::message
**ワークフローのポイント** 🎯

1. **build_and_test**: ビルド → HTTPサーバー起動 → アクセシビリティテスト実行
2. **continue-on-error: true**: テスト失敗でもCIは止めず、結果をSummaryに出力
3. **npx serve**: SWA CLIより軽量で安定したHTTPサーバー
4. **deploy**: `ENABLE_DEPLOY=true` の場合のみStatic Web Appsへデプロイ
5. **close_pull_request**: PRマージ/クローズ時にプレビュー環境を削除
:::

## 3.2 Azure Static Web Appsのセットアップ

Azure PortalでStatic Web Appsリソースを作成し、シークレットとVariableを設定します🔐

### シークレットの設定

1. Azure Portal → Static Web Apps → 対象のリソース
2. 「デプロイトークンの管理」からトークンをコピー
3. GitHub → Settings → Secrets and variables → Actions → Secrets
4. シークレット名はAzureが自動生成したワークフローに合わせる（例: `AZURE_STATIC_WEB_APPS_API_TOKEN_GRAY_MEADOW_072FA5010`）

### デプロイの有効化

デプロイを有効にするには、GitHub Variableを設定します：

1. GitHub → Settings → Secrets and variables → Actions → Variables
2. `ENABLE_DEPLOY` = `true` を追加

| 設定 | キー | 値 |
|------|------|-----|
| Secret | `AZURE_STATIC_WEB_APPS_API_TOKEN_*` | Azureのデプロイトークン |
| Variable | `ENABLE_DEPLOY` | `true`（デプロイを有効にする場合）|

:::message alert
**注意**: `ENABLE_DEPLOY` が未設定または `false` の場合、デプロイジョブはスキップされます。
アクセシビリティテストの検証だけを行いたい場合は、この変数を設定しないでください。
:::

# Step 4: 段階的な導入戦略

いきなり全ての違反でCIを止めるのは現実的ではありません🧭

## 現在の設定（可視化フェーズ）

本ワークフローでは `continue-on-error: true` を使用しているため、アクセシビリティ違反があってもCIは止まりません。
違反はGitHub Actions Summaryに出力され、開発者が確認できます。

## 段階的なロールアウト

| Phase | 期間 | 設定 |
|-------|------|------|
| 📊 可視化（現在） | 最初の2週間 | `continue-on-error: true` で違反を記録するがCIは落とさない |
| ⚠️ 重大のみ | 3〜4週目 | Critical/Seriousのみブロック |
| 🛡️ 全違反 | 5週目以降 | `continue-on-error: false` で全ての違反でCIを止める |

Phase 3に移行する場合は、ワークフローから `continue-on-error: true` を削除してください：

```yaml
- name: Run Accessibility Tests
  id: a11y_test
  # continue-on-error: true  # この行を削除またはコメントアウト
  run: |
    dotnet test BlazorA11yDemo.Tests --no-build
```

# よくある違反と修正方法

テストを実行すると、よく以下の違反が検出されます🔍

## color-contrast（コントラスト不足）

```html
<!-- NG -->
<p style="color: #999;">薄いグレー</p>

<!-- OK: 4.5:1以上のコントラスト -->
<p style="color: #595959;">読みやすいグレー</p>
```

## image-alt（代替テキスト欠落）

```html
<!-- NG -->
<img src="product.jpg">

<!-- OK -->
<img src="product.jpg" alt="商品名: サンプル商品">
```

## label（フォームラベル欠落）

```html
<!-- NG -->
<input type="email" placeholder="メールアドレス">

<!-- OK -->
<label for="email">メールアドレス</label>
<input type="email" id="email">
```

## button-name（ボタン名欠落）

```html
<!-- NG -->
<button><svg>...</svg></button>

<!-- OK -->
<button aria-label="メニューを開く"><svg>...</svg></button>
```

# まとめ

本記事では、**Blazor WebAssembly + Azure Static Web Apps**を題材に、**Playwright C# + axe-core + GitHub Actions**でアクセシビリティを自動検査する仕組みを構築しました🎯

## 実装したこと

1. ✅ Blazor WebAssembly を Static Web Apps にホスト
2. ✅ `npx serve` でCI環境でのHTTPサーバー起動
3. ✅ Playwright C# + axe-core で WCAG 2.1 AA 検査
4. ✅ `continue-on-error: true` でテスト失敗でもCIを止めない
5. ✅ GitHub Actions Summaryにテスト結果を出力
6. ✅ `ENABLE_DEPLOY` 変数でデプロイを制御

## 次のステップ

- 🔧 認証が必要なページのテスト追加
- 📊 テスト結果のダッシュボード化
- 🧪 手動テストとの組み合わせ
- 🛡️ 段階的にCIを厳格化（Phase 2, 3への移行）

「自動で潰せるものは自動で潰し、人間の判断が必要なものに集中する」🤝
これがCI/CDでアクセシビリティを担保する意義です♿

---

## 参考リンク

### Azure Static Web Apps
- [Azure Static Web Apps ドキュメント](https://learn.microsoft.com/ja-jp/azure/static-web-apps/)
- [Deploy a Blazor app on Azure Static Web Apps](https://learn.microsoft.com/azure/static-web-apps/deploy-blazor)
- [Set up local development for Azure Static Web Apps](https://learn.microsoft.com/azure/static-web-apps/local-development)
- [SWA CLI - npm](https://www.npmjs.com/package/@azure/static-web-apps-cli)

### Blazor
- [Tooling for ASP.NET Core Blazor](https://learn.microsoft.com/aspnet/core/blazor/tooling)
- [ASP.NET Core Blazor project structure](https://learn.microsoft.com/aspnet/core/blazor/project-structure)

### アクセシビリティテスト
- [Deque.AxeCore.Playwright - NuGet](https://www.nuget.org/packages/Deque.AxeCore.Playwright)
- [axe-core NuGet packages - GitHub](https://github.com/dequelabs/axe-core-nuget)
- [axe-core Rules](https://github.com/dequelabs/axe-core/blob/master/doc/rule-descriptions.md)
- [Playwright for .NET](https://playwright.dev/dotnet/)

### アクセシビリティ基準
- [WCAG 2.1 達成基準（日本語）](https://waic.jp/docs/WCAG21/)
- [Web Content Accessibility Guidelines - Microsoft Compliance](https://learn.microsoft.com/compliance/regulatory/offering-wcag-2-1)

### 関連記事
- [前回の記事: Webアクセシビリティは"もしも"に備える設計](https://zenn.dev/tomokusaba/articles/93810f232cec91)
