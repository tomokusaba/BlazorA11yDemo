# BlazorA11yDemo

[![Azure Static Web Apps CI/CD](https://github.com/tomokusaba/BlazorA11yDemo/actions/workflows/azure-static-web-apps.yml/badge.svg)](https://github.com/tomokusaba/BlazorA11yDemo/actions/workflows/azure-static-web-apps.yml)

Blazor WebAssembly + Playwright + axe-core を使用したアクセシビリティ自動テストのデモプロジェクトです。

## 概要

このプロジェクトは、CI/CDパイプラインでWebアクセシビリティを自動検査する仕組みを実装しています。

- **Blazor WebAssembly** アプリケーション
- **Playwright** によるブラウザ自動化
- **axe-core** によるWCAG 2.1 AA準拠チェック
- **Azure Static Web Apps** へのデプロイ
- **GitHub Actions** による自動テスト

## プロジェクト構成

```
BlazorA11yDemo/
├── BlazorA11yDemo.Client/          # Blazor WebAssembly アプリ
│   ├── Pages/
│   │   ├── Home.razor              # / (ホーム)
│   │   ├── Counter.razor           # /counter (カウンター)
│   │   └── Weather.razor           # /weather (天気予報)
│   └── ...
├── BlazorA11yDemo.Tests/           # アクセシビリティテスト
│   ├── AccessibilityTests.cs       # Playwright + axe-core テスト
│   ├── appsettings.json            # テスト設定
│   └── ...
├── docs/                           # 関連記事
│   └── readme.md
├── .github/workflows/
│   └── azure-static-web-apps.yml   # CI/CDワークフロー
└── swa-cli.config.json             # SWA CLI設定
```

## 技術スタック

| カテゴリ | 技術 |
|----------|------|
| フレームワーク | .NET 9.0, Blazor WebAssembly |
| テスト | xUnit, Playwright 1.57.0 |
| アクセシビリティ | Deque.AxeCore.Playwright 4.11.0 |
| ホスティング | Azure Static Web Apps |
| CI/CD | GitHub Actions |

## 必要条件

- .NET 9 SDK
- Node.js 20以上（Playwrightブラウザインストール用）
- PowerShell（playwright.ps1実行用）

## セットアップ

### 1. リポジトリのクローン

```bash
git clone https://github.com/tomokusaba/BlazorA11yDemo.git
cd BlazorA11yDemo
```

### 2. 依存関係の復元とビルド

```bash
dotnet restore
dotnet build
```

### 3. Playwrightブラウザのインストール

```bash
pwsh BlazorA11yDemo.Tests/bin/Debug/net9.0/playwright.ps1 install chromium
```

## 使い方

### アプリケーションの起動

```bash
cd BlazorA11yDemo.Client
dotnet run
```

デフォルトで `http://localhost:5105` で起動します（ポートは環境により異なる場合があります）。

### アクセシビリティテストの実行

別ターミナルでアプリを起動した状態で：

```bash
cd BlazorA11yDemo.Tests
dotnet test
```

> **Note**: `appsettings.json` の `BaseUrl` をアプリのポートに合わせてください。

### SWA CLIでのローカル実行（オプション）

```bash
# ビルド
dotnet publish BlazorA11yDemo.Client -c Release

# SWA CLIで起動（ポート4280）
swa start BlazorA11yDemo.Client/bin/Release/net9.0/publish/wwwroot
```

## テスト内容

### 検査対象ページ

| パス | ページ名 |
|------|----------|
| `/` | Home |
| `/counter` | Counter |
| `/weather` | Weather |

### 検査基準

WCAG 2.1 Level A, AA に準拠：
- `wcag2a`
- `wcag2aa`
- `wcag21aa`

### テストケース

1. **Page_ShouldHaveNoAccessibilityViolations** - 各ページの静的なアクセシビリティ検査
2. **Counter_AfterInteraction_ShouldBeAccessible** - ボタン操作後の動的なアクセシビリティ検査

## CI/CD

GitHub Actionsで以下を自動実行：

1. **build_and_test** - ビルド → HTTPサーバー起動 → アクセシビリティテスト
2. **deploy** - Azure Static Web Appsへデプロイ（`ENABLE_DEPLOY=true`の場合）

### GitHub設定

| 種類 | キー | 説明 |
|------|------|------|
| Secret | `AZURE_STATIC_WEB_APPS_API_TOKEN_*` | Azureデプロイトークン |
| Variable | `ENABLE_DEPLOY` | `true`でデプロイ有効化 |

### テスト結果

テスト結果は **GitHub Actions Summary** に出力されます。現在は `continue-on-error: true` で、テスト失敗でもCIは止まりません。

## 関連記事

- [WebアクセシビリティをCI/CDで担保する ― axe DevTools × Playwright C#実践ガイド](docs/readme.md)

## ライセンス

MIT

## 参考リンク

- [Azure Static Web Apps ドキュメント](https://learn.microsoft.com/ja-jp/azure/static-web-apps/)
- [Playwright for .NET](https://playwright.dev/dotnet/)
- [Deque.AxeCore.Playwright - NuGet](https://www.nuget.org/packages/Deque.AxeCore.Playwright)
- [WCAG 2.1 達成基準（日本語）](https://waic.jp/docs/WCAG21/)
