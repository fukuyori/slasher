# Numadora セキュリティ・ネットワーク 言語統合設計

> Status: 設計ドキュメント。実装はまだ初期段階のため **互換性配慮なしのハードカット**
> で v0.2 spec に追加する (Q-L3 / Q6 と同じ判断)。本ノートで確定した形で
> `numadora-language-spec.md` を直接更新する。
>
> 以前の `EXPORT EFFECT FUNC` (能力クラスなし) や `EXPORT INTERACTIVE EFFECT FUNC` の
> 形式は **完全に廃止**。能力クラスは常に明示する。
>
> v0.2 では `EFFECT` / `INTERACTIVE` 修飾子と `policy_denied` エラー コードを言語に
> 入れたが、能力クラス・トラスト プロファイル・ネットワーク呼び出しは依然として
> ランタイム ポリシーと外部ドキュメントに分散している。本ノートはこれらを
> **言語の一級概念** として統合する。
>
> 関連:
>
> - `numadora-language-spec.md` v0.2 (現行 spec)
> - `security-policy.md` (能力クラス・プロファイル・脅威モデル — 言語側がここに合わせる)
> - `numadora-lineage-policy-plan.md` (lineage モデル)
> - `peer-network-model.md` (ピア プロトコル設計)
> - `slasher-plugin-architecture.md` (プラグイン契約 — 能力宣言の最終登録ポイント)

---

## 0. 動機

v0.2 では以下が実現されている:

- `EXPORT EFFECT FUNC` で副作用ありを示す
- `EXPORT INTERACTIVE EFFECT FUNC` でユーザ承認必須を示す
- ランタイムで `NumadoraPolicyEvaluator` が `policy_denied` を投げる

不足している統合:

- **能力クラスの粒度がない**: `EFFECT` は「副作用あり」という単一フラグ。`security-policy.md`
  で定義された 12 能力クラス (Observe, User-input, File-write, Destructive, Network 等)
  と対応していない
- **必要能力の静的宣言がない**: スクリプトが「これは file-write と network-out を使う」と
  ファイル先頭で宣言する仕組みがない
- **ピア通信が言語に入っていない**: `slasher/peer` モジュールがなく、ピア委譲は HTTP API
  経由のみ
- **トラスト プロファイルが値レベルにない**: ピアから受けた値のトラスト ラベルが型システムで
  追跡されない
- **lineage 情報が言語に露出していない**: `numadora-lineage-policy-plan.md` の lineage は
  ランタイム メタデータのみで、スクリプトからは見えない

これらを段階的に言語へ統合する。

## 1. 三段階の統合計画

| Tier | 内容 | 採用判断 |
|---|---|---|
| **Tier A** | EFFECT 修飾子の能力クラス化、`REQUIRES` 宣言、`slasher/peer` モジュール、トラスト プロファイル列挙型 | **v0.3 で採用** |
| **Tier B** | トラスト ラベル付き型 (`Trusted[T, profile]`)、lineage 値の露出 | **v0.4 検討** |
| **Tier C** | アルジェブラ的エフェクト ハンドラ、capability token としての値渡し | **v1 以降検討** |

本ノートは主に Tier A を確定し、Tier B は方向性、Tier C は記録のみ。

---

## 2. Tier A 設計

### 2.1 能力クラス: 言語のキーワード化

`security-policy.md` の 12 能力クラス + Q-S1 採用の `system-info` で **計 13 クラス**:

```text
observe          file-read         file-write       destructive
user-input       browser-data      clipboard        process-app
network-out      network-in        peer-delegate    secrets
unattended       scheduling        system-info       ← Q-S1 採用
```

これらは予約語ではなく **能力クラス識別子**。Q-S4 採用により、`EFFECT(...)` と
`REQUIRES(...)` の **括弧内のみ** で能力クラスとして認識される (コンテキスト認識)。
括弧外では通常の識別子として扱う (例: `LET observe = ...` は合法、ただし Linter 警告対象)。

能力クラスは **閉集合**。v0.3 ではユーザ定義不可。新規能力の追加は spec 改訂を伴う。

`system-info` の対象範囲:

- 時刻取得 (`std/io.now`)
- CWD (`std/io.cwd`)
- 軽い環境情報 (`std/io.env` の安全な使用)

`env` から secret 漏洩リスクが顕在化したら、v0.4 で `env-read` 等への細分化を検討する。

### 2.2 EFFECT 修飾子の拡張

#### 2.2.1 構文

```ebnf
func-modifier := "INTERACTIVE"
               | "EFFECT" ("(" capability-list ")")?
capability-list := capability ("," capability)*
capability    := "observe" | "file-read" | "file-write" | "destructive"
               | "user-input" | "browser-data" | "clipboard"
               | "process-app" | "network-out" | "network-in"
               | "peer-delegate" | "secrets" | "unattended" | "scheduling"
```

#### 2.2.2 EFFECT は能力クラス必須 (ハードカット)

`EFFECT` 修飾子には **能力クラスを必ず指定** する。引数なしの `EXPORT EFFECT FUNC` は
構文エラー (`effect_class_required`)。

互換性配慮なし。v0.2 仕様で書かれた `EXPORT EFFECT FUNC ...` は新仕様でパースエラー。
既存 `.numai` (`scripts/numadora-host/slasher/*.numai`) と spec 9.2 / 6.5 は本ノート確定後に
一斉書き換え。

#### 2.2.3 INTERACTIVE 修飾子の規則 (Q-S2 採用)

INTERACTIVE は能力クラスに **直交するメタ修飾**。常に EFFECT(class) 併記必須:

| 形 | 可否 | 意味 |
|---|---|---|
| `INTERACTIVE EFFECT(user-input) FUNC ...` | ✅ 推奨 | 入力 + ユーザ承認必須 |
| `INTERACTIVE EFFECT(destructive) FUNC ...` | ✅ | 破壊操作 + 承認必須 |
| `INTERACTIVE EFFECT(network-out, peer-delegate) FUNC ...` | ✅ | ピア委譲 + 承認必須 |
| `INTERACTIVE EFFECT FUNC ...` | ❌ 構文エラー | 能力クラス必須 |
| `INTERACTIVE FUNC ...` | ❌ 構文エラー (`interactive_without_effect`) | 副作用ない関数に承認は無意味 |

#### 2.2.3 実例

```numadora
# .numai 抜粋

# 観測のみ
EXPORT EFFECT(observe) FUNC info(target: WindowRef): WindowInfo

# 入力 (ユーザ承認必須は INTERACTIVE で重ねる)
EXPORT INTERACTIVE EFFECT(user-input) FUNC text(content: string): unit

# ファイル書き込み + 破壊的
EXPORT EFFECT(file-write, destructive) FUNC delete(path: string, allow-destructive: bool, dry-run: bool): array[string]

# ネットワーク送信
EXPORT EFFECT(network-out) FUNC http-get(url: string): Option[HttpResponse]

# ピア委譲 (送信側) + ネットワーク
EXPORT EFFECT(network-out, peer-delegate) FUNC delegate-run(target: PeerRef, script: string, profile: string): string

# 複数の能力組み合わせ
EXPORT INTERACTIVE EFFECT(browser-data, network-out) FUNC browser-upload(...): unit
```

#### 2.2.4 純粋性検査との関係

純粋関数 (any `EFFECT` 修飾なし) は `EFFECT(...)` 関数を呼べない (既存規則と同じ)。
能力クラスは「不純さの種類分け」を提供するが、純粋/不純の二分法は変わらない。

### 2.3 REQUIRES 宣言

スクリプトが必要とする能力をファイル先頭で宣言する:

```ebnf
script-requires := "REQUIRES" "(" capability-list ")" newline
```

#### 2.3.1 配置

`MODULE` 宣言の **直後** (任意)。`IMPORT` より前。

```numadora
MODULE notepad-check
REQUIRES (process-app, user-input, observe)

IMPORT slasher/app AS app
IMPORT slasher/input AS input
...
```

#### 2.3.2 検査

- check 段階: スクリプト内で実際に呼ばれている `EFFECT(...)` の能力集合を計算
- 計算結果が `REQUIRES` の集合に **含まれる** ことを検証
- 集合外なら `requires_missing_capability` エラー (どの能力が宣言から漏れているかを `details` で示す)
- 宣言が実際より広い (使ってない能力が REQUIRES にある) のは **警告** (`requires_unused_capability`)

#### 2.3.3 伝播 (Q-S3 採用)

**`REQUIRES` を持つのは `main` 関数を持つモジュール (実行可能スクリプト) のみ**。
ライブラリ モジュールは関数ごとに `EFFECT(class)` を宣言するだけで `REQUIRES` を書かない。

main モジュールの `REQUIRES` は **import 先のライブラリ関数が使う能力を含めた全集合** を
カバーする必要があり、check 段階でこれを推移的に計算して検証する。

例: main が `IMPORT slasher/files AS files` を呼び、`files.delete(...)` (`EFFECT(file-write, destructive)`)
を使うなら、main の `REQUIRES` には少なくとも `file-write, destructive` を含める必要がある。
不足していれば `requires_missing_capability` で `details` に「どの import / どの関数が要求しているか」
を含める。

#### 2.3.3 ランタイム連携

run 時、Slasher は `REQUIRES` の集合と現在の **能力プロファイル** を突合:

- profile = `observe` のとき、`REQUIRES (user-input)` が含まれていれば run 拒否 (`policy_denied`)
- profile = `interactive` のとき、`REQUIRES (destructive, secrets)` は拒否
- profile = `destructive` 等の上位プロファイルは下位を含む (順序は `security-policy.md` を参照)

これにより **「実行前に静的に判断」** が可能になる。INTERACTIVE 関数を含むスクリプトは
`allowInteractiveInput` 承認なしでは run しない、という現状判断を強化する形。

### 2.4 slasher/peer ホスト モジュール

ピア通信を **言語の一級概念** として `.numai` で公開する。

```numadora
# slasher/peer.numai (概要)
MODULE slasher/peer

--- 信頼プロファイル (security-policy.md 由来)。
EXPORT TYPE TrustProfile = "known" | "observed" | "interactive"

--- 登録済ピアへの不透明参照。
EXPORT OPAQUE TYPE PeerRef

--- ピアの観測情報。
EXPORT RECORD PeerInfo {
  name: string,
  trust-profile: TrustProfile,
  endpoint: string,
  is-online: bool,
}

--- 名前空間エントリ。
EXPORT RECORD NamespaceEntry {
  name: string,
  kind: string,            --- "windows" | "screen" | "input" | ...
  is-readable: bool,
  is-invokable: bool,
}

# 観測系 (network-out のみ、peer-delegate なし)
EXPORT EFFECT(network-out) FUNC list-peers(): array[PeerInfo]
EXPORT EFFECT(network-out) FUNC find-peer(name: string): Option[PeerRef]
EXPORT EFFECT(network-out) FUNC info(target: PeerRef): PeerInfo
EXPORT EFFECT(network-out) FUNC namespace-list(target: PeerRef, path: string): array[NamespaceEntry]
EXPORT EFFECT(network-out) FUNC namespace-read(target: PeerRef, path: string): string

# 委譲系 (network-out + peer-delegate)
EXPORT EFFECT(network-out, peer-delegate) FUNC delegate-run(
  target: PeerRef,
  script: string,
  required-profile: string,
  purpose: string
): string                  --- 戻り値は run-id

EXPORT EFFECT(network-out) FUNC delegate-status(target: PeerRef, run-id: string): string

# 着信系 (network-in、ホスト側でのみ意味を持つ — スクリプトが書く必要は通常ない)
# 初版ではエクスポートしない (将来検討)
```

#### 2.4.1 ピア委譲の再帰禁止 (Q-S6 採用)

**委譲経由で実行された run は `peer-delegate` 能力の使用を禁止する**。

- 各 run コンテキストに `delegation-depth: int` を記録 (初回 = 0、委譲経由で起動 = 1)
- `delegation-depth >= 1` の run が `delegate-run` を呼んだ場合、`policy_denied`
  (`code = "policy_recursive_delegation"`)
- run artifact には委譲経路 (`delegated-from: peer1 -> peer2`) を記録、監査可能
- 将来の信頼チェーン上限緩和は別 PR で検討

この制約は言語仕様ではなくランタイム ポリシーで強制 (`NumadoraPolicyEvaluator`)。
スクリプトは `delegate-run` を構文上書ける (静的検査では弾かない) が、実行時に拒否される。

#### 2.4.2 ピア委譲の典型形

```numadora
MODULE remote-deploy
REQUIRES (network-out, peer-delegate, observe)

IMPORT slasher/peer AS peer

EXPORT FUNC main()
  LET workstation = peer.find-peer("workstation")
                      OR FAIL "workstation not registered"

  LET info = workstation.info()
  io.log("delegating to " + info.name + " (trust=" + info.trust-profile + ")")

  LET run-id = workstation.delegate-run(
    "MODULE remote-task\nIMPORT ...\nEXPORT FUNC main() ... END",
    "interactive",
    "remote-deploy"
  )

  io.log("delegated run id: " + run-id)
END
```

### 2.5 トラスト プロファイル列挙型

`TrustProfile = "known" | "observed" | "interactive"` を `slasher/peer.numai` で公開
(2.4 参照)。`security-policy.md` の信頼プロファイル分類が言語型として現れる。

将来の Tier B でトラスト ラベル付き型 (`Trusted[T, profile]`) を導入したとき、
この列挙型がラベルの集合となる。

### 2.6 spec 改訂事項 (v0.3)

`numadora-language-spec.md` への追加:

| 項目 | 内容 |
|---|---|
| 第 1 章 字句構造 | 能力クラス識別子を「キーワードでも識別子でもない第 4 の名前空間」として記述 |
| 第 2 章 型システム | `TrustProfile` 等の string-literal union 例を能力クラスと連動させる |
| 第 3 章 式と文 | `REQUIRES` 宣言を top-level 要素に追加 |
| 第 6 章 module と import | top-level 要素に `script-requires` を追加 |
| 第 9 章 ホスト バインディング | `EFFECT(class)` の能力クラス引数を 9.2 に追加 |
| 付録 A EBNF | `func-modifier`, `script-requires` を更新 |
| 付録 B エラー | `requires_missing_capability`, `requires_unused_capability`, `effect_class_unspecified` を追加 |

---

## 3. Tier B 方向性 (v0.4 検討)

### 3.1 トラスト ラベル付き型

```numadora
EXPORT EFFECT(network-out) FUNC namespace-read(target: PeerRef, path: string): Trusted[string, observed]
```

戻り値は「`observed` プロファイルのピアから来た文字列」というラベル付き型。

利用側:

```numadora
LET data = workstation.namespace-read(target, "/clipboard")
# data の型は Trusted[string, "observed"]

input.text(data)            # 型エラー: input.text は Trusted[string, "interactive"+] を要求
```

`Trusted[T, profile]` のサブタイプ規則:

- `Trusted[T, "interactive"]` <: `Trusted[T, "observed"]` <: `Trusted[T, "known"]` (信頼度高 → 低)
- 高信頼の値は低信頼パラメータに渡せる
- 低信頼の値を高信頼パラメータに渡すには **明示的な昇格関数** が必要 (`elevate-trust(value, profile)` で記録ログを残す)

これは情報フロー型 (information-flow types) の軽量版。AI 生成スクリプトが意図せず
信用してはいけないデータを INTERACTIVE 関数に渡すことを **静的に防ぐ**。

### 3.2 lineage 値の露出

```numadora
EXPORT RECORD Lineage {
  source: string,            --- 例: "peer:workstation", "user:keyboard", "file:input.csv"
  classification: string,    --- "public" | "personal" | "secret"
  acquired-at-ms: int,
}

EXPORT EFFECT(observe) FUNC lineage-of[T](value: T): Option[Lineage]
```

各 `EFFECT` 関数の戻り値に lineage が付与され、`lineage-of` で取り出せる。
ホスト関数のポリシー判定はこの lineage を参照する。

これは `numadora-lineage-policy-plan.md` を言語に持ち上げる。

---

## 4. Tier C 記録 (v1 以降)

### 4.1 アルジェブラ的エフェクト ハンドラ

```numadora
WITH-HANDLER {
  network-out -> redirect-to(local-cache)
  destructive -> deny
} DO
  ... ユーザ提供スクリプト ...
END
```

スクリプト全体に対するエフェクト ポリシーをスコープで囲む。Koka, Eff, Effekt 等の
代数的エフェクトに着想。

採用判断は v1 以降で、まず Tier A の実装経験を蓄積してから。

### 4.2 Capability token としての値渡し

```numadora
FUNC privileged-task(net: NetworkOutCap, file: FileWriteCap)
  ...
END
```

能力をトークン値として明示的に受け渡す。Pony や POSIX capability に着想。Tier A の
`REQUIRES` で十分な場合は採用しない。

---

## 5. ハードカット適用範囲

互換性配慮なし (実装は初期段階)。本ノート確定後、以下を一斉に新形式へ書き換える:

| 対象 | 旧形式 | 新形式 |
|---|---|---|
| `numadora-language-spec.md` 9.2 | `EXPORT EFFECT FUNC` 例 | `EXPORT EFFECT(class) FUNC` 例 |
| `numadora-language-spec.md` 6.5 | EXPORT 修飾子一覧 | `EFFECT(class)` 構文を必須形として記述 |
| `numadora-language-spec.md` 6 章 | (REQUIRES なし) | `script-requires` 構文を top-level に追加 |
| `numadora-language-spec.md` 9 章 | (slasher/peer なし) | ピア委譲節を追加 |
| `scripts/numadora-host/slasher/*.numai` | 全 12 ファイル `EXPORT EFFECT FUNC` | `EXPORT EFFECT(class) FUNC` (本ノート確定後に書き換え) |
| `scripts/numadora-samples/*.numa` (v0.2 化済 6 ファイル) | (REQUIRES なし) | 必要に応じて `REQUIRES` 追加 |
| `slasher-numadora-integration.md` 能力テーブル | EFFECT/INTERACTIVE 列 | EFFECT(class) / INTERACTIVE 列 |
| `security-policy.md` 能力テーブル | v0.1 関数名 | `EFFECT(class)` 修飾子に基づく自動生成可能形 |
| `slasher-plugin-architecture.md` `IAppOpsPlugin` | (能力宣言なし) | `Capabilities: array[string]` 追加 |
| `peer-network-model.md` | (slasher/peer 言及なし) | ピア委譲スクリプト例を追加 |

### 5.1 PR 計画

| PR | 内容 | 依存 |
|---|---|---|
| **Sec PR-A** | 能力クラス識別子、`EFFECT(class)` 必須化、`REQUIRES`、`slasher/peer` を spec に追加 | (前提) |
| **Sec PR-B** | パーサ更新 (能力クラス、REQUIRES、INTERACTIVE EFFECT 強制) | Sec PR-A, Lang PR-C |
| **Sec PR-C** | `scripts/numadora-host/slasher/*.numai` 全 12 ファイルを `EFFECT(class)` 形式に書き換え | Sec PR-A |
| **Sec PR-D** | `slasher/peer.numai` を新規作成 | Sec PR-C |
| **Sec PR-E** | check 段階での REQUIRES 計算と能力集合検証 | Sec PR-B |
| **Sec PR-F** | ランタイム ポリシー評価器を能力クラス対応に、再帰委譲ガード追加 | Sec PR-E |
| **Sec PR-G** | `security-policy.md` / `peer-network-model.md` / `numadora-lineage-policy-plan.md` の整合 | Sec PR-A〜F |
| **Sec PR-H** | サンプル `.numa` に `REQUIRES` 追加 (notepad-check 等) | Sec PR-A, Lang PR-B+C |

AppOps/Lang PR 群と並行可能 (主に Lang PR-C/E に依存)。

---

## 6. 既存ドキュメントへの影響

### 6.1 security-policy.md

- 能力クラス表 (12 クラス) が **言語キーワード** として現れる旨を冒頭で明記
- 能力テーブル (現状 v0.1 関数名) を v0.3 で `EFFECT(class)` 修飾子から自動生成可能になることを明記
- スクリプト プロファイル → `REQUIRES` 静的宣言の関係を追加
- ピア委譲の `network-out` + `peer-delegate` 能力の組み合わせを定義

### 6.2 numadora-lineage-policy-plan.md

- Tier A 段階では言語側に lineage 露出なし、ランタイム ポリシーのみで継続
- Tier B 案として「lineage 値の言語側露出」を将来検討項目に追加

### 6.3 peer-network-model.md

- ピア プロトコルは現状維持
- 言語側からは `slasher/peer` モジュール経由で利用する形を追記
- 新規追加: 「ピア委譲スクリプトの v0.3 例」セクション

### 6.4 slasher-numadora-integration.md

- 能力テーブルを `EFFECT(class)` 列付きに更新
- `slasher/peer` 行を追加

### 6.5 slasher-plugin-architecture.md

- プラグイン契約の `.numai` 例に `EFFECT(class)` を反映
- プラグイン登録時に「自プラグインが提供する能力クラスの宣言」を追加 (`PluginRequirements`
  に `Capabilities: array[string]` を追加)

---

## 7. ネットワーク層の言語面と実装面の対応

| 層 | 実装 | 言語側の表現 |
|---|---|---|
| Network 層 (`Slasher.Network`) | Peers, HTTP client, ディスカバリ | `slasher/peer` ホスト モジュール (Tier A)、`std/http` (既存) |
| ピア識別 / 信頼プロファイル | `PeerTrustProfile` C# 型 | `TrustProfile` 列挙型 (Tier A) |
| ピア能力公開 | `PeerCapabilities` C# 型 | `slasher/peer.namespace-list()` の戻り値 (Tier A) |
| ピア委譲 run | `POST /peer/run` (将来) | `slasher/peer.delegate-run(...)` (Tier A) |
| 認証 (bearer token / 将来 mTLS) | サーバ側ミドルウェア | 言語側には露出しない (環境変数 / 設定) |
| ディスカバリ (mDNS) | 将来実装 | 言語側には露出しない (自動的に `list-peers` の結果に反映) |

---

## 8. AI 生成可読性への影響

能力クラスを言語に入れると AI が見るシグネチャが冗長になる。緩和策:

- 能力クラスは **少数の確定セット** (12 クラス) に限定し、AI に学習させやすくする
- `EFFECT(class)` のクラスは IDE / `numac doc` で関数シグネチャの一部として常に表示
- AI 生成プロンプトに「能力クラスは security-policy.md の 12 種から選ぶ」と明記

利点 (可読性が下がる以上に得られるもの):

- AI が「このスクリプトは何ができるか」を **シグネチャだけで** 推測できる
- AI が REQUIRES を見て即座に「危険そうな宣言があれば user に確認」を判断できる
- AI 生成スクリプトのレビューで「許可されていない能力を使っている」を機械検出できる

---

## 9. Q-S1〜S6 確定事項

すべて採用済 (一括採用)。

| Q | 確定 | 反映先 |
|---|---|---|
| **S1** 能力クラスのリスト | `security-policy.md` の 12 クラス + **`system-info`** の計 13 クラス。time/random/env は `system-info` に統合。env から secret 漏洩リスク顕在時に細分化検討 | 2.1 |
| **S2** INTERACTIVE と能力クラス | INTERACTIVE は能力クラスに直交するメタ修飾。**EFFECT(class) 併記必須**。`INTERACTIVE EFFECT FUNC` (能力クラスなし) や `INTERACTIVE FUNC` (EFFECT なし) は構文エラー | 2.2.3 |
| **S3** REQUIRES の伝播 | **main 持ちモジュール (実行可能スクリプト) のみ** が `REQUIRES` を持つ。ライブラリは `EFFECT(class)` のみ。main の `REQUIRES` は import 先含む全能力を含む必要あり (推移検証) | 2.3.3 |
| **S4** capability namespace | **コンテキスト認識**。`EFFECT(...)` と `REQUIRES(...)` の括弧内のみ能力クラスとして解釈。閉集合 (13 種、ユーザ定義不可) | 2.1 |
| **S5** Tier B の優先度 | **Tier A 完了後に検討**。Tier A の実装経験を蓄積してから `Trusted[T, profile]` を導入 | 3 章 (将来検討として残置) |
| **S6** ピア委譲の再帰 | **再帰委譲を禁止**。委譲経由 run は `peer-delegate` 能力を実行時拒否 (`policy_recursive_delegation`)。run artifact に経路記録 | 2.4.1 |

### 9.1 確定後の主要なエラー コード

| code | 意味 |
|---|---|
| `effect_class_required` | `EFFECT` に能力クラス指定がない |
| `effect_class_unknown` | 13 クラス以外を指定 |
| `interactive_without_effect` | INTERACTIVE 単独使用 |
| `requires_missing_capability` | スクリプトが使う能力が REQUIRES に含まれていない |
| `requires_unused_capability` (warning) | REQUIRES に宣言したが実際は使われていない能力 |
| `policy_recursive_delegation` | 委譲経由 run からの delegate-run 試行 |

---

## 10. 改訂履歴

- v0.1 — 初版起草。Tier A (EFFECT 能力クラス、REQUIRES、slasher/peer、TrustProfile)、
  Tier B (トラスト ラベル付き型、lineage 露出)、Tier C (エフェクト ハンドラ、capability
  token) の三段階を提示。
- v0.2 — Q-S1〜S6 を一括採用、ハードカット方針 (互換性配慮なし) を確定。13 能力クラス
  (system-info 追加)、INTERACTIVE EFFECT(class) 必須、REQUIRES は main のみ、
  ピア委譲の再帰禁止を明文化。`scripts/numadora-host/slasher/*.numai` と
  `numadora-language-spec.md` の一斉書き換えを 5 章に列挙。
