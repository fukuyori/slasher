# Architecture Tests

5 層構成 + プラグイン アーキテクチャの依存方向ルールを CI で強制するテスト群。

## 設計参照

- `docs/slasher-layer-architecture.md` 1.3 / 4 章 — 5 層構成の依存方向と NetArchTest 採用判断
- `docs/slasher-plugin-architecture.md` 6.1 — プラグイン独立性ルール
- `docs/slasher-plugin-architecture.md` 11.4 — プラグイン命名規約

## テスト一覧

| ファイル | 目的 |
|---|---|
| `AssemblyHelpers.cs` | Slasher アセンブリ解決 + 失敗メッセージ整形 |
| `DependencyTests.cs` | 5 層 (Api/Core/Io/Network/AppOps) の依存方向 |
| `PluginIsolationTests.cs` | AppOps プラグイン間の独立性 |
| `NamespaceConventionTests.cs` | プラグイン命名規約と層 namespace の整理 |

## PR-1 前後の挙動

`docs/slasher-plugin-architecture.md` 9 章 PR-1 (フォルダ移動 + namespace 一括書き換え)
完了前は、本テスト群が参照する `Slasher.Core` / `Slasher.Network` / `Slasher.AppOps`
名前空間が存在しないため、ほとんどのテストが **vacuous truth** として通過する
(空集合に対する制約は常に満たされる)。

PR-1 完了後、これらのテストは v0.2.1 設計の 5 層構成を破壊する変更を CI で
即座に検出する **ガード レール** として機能し始める。

## 実行

```powershell
dotnet test tests/Slasher.Tests/Slasher.Tests.csproj --filter "FullyQualifiedName~Architecture"
```

または全テスト実行:

```powershell
dotnet test
```

## 拡張ポイント

将来追加検討:

- **Capability テーブル整合**: プラグインの `PluginRequirements.Capabilities` と
  `.numai` の `EXPORT EFFECT(class) FUNC` の能力クラス集合が一致することを実行時検査
  (テストとしてではなく、起動時 `plugin_capabilities_mismatch` で fail-fast)
- **OS 属性の付与確認**: `[SupportedOSPlatform("windows")]` がプラグイン実装クラスに
  正しく付与されているか (`SupportedOS` 配列との整合)
- **interface 経由依存の強制**: プラグインから他プラグインへの依存を Core interface
  経由のみに制限 (現状は具象 namespace 禁止のみ)
