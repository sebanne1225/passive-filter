# Passive Filter — repo 現況メモ

VRChat 向け、ギミックが「他人視点で出っぱなし」になるのを防ぐ非破壊 NDMF ツール。
ビルド時に各トグルの初期状態を hidden 側へ倒す。

## Goal

アバターのトグルギミック（オブジェクト / メッシュ / パーティクル / ライト / 音）が、
同期ズレ・デフォルト ON・ローカル専用などの理由で「他人視点で勝手に出っぱなし」に
なるのを防ぐ。ビルド時に各トグルの初期状態を hidden 側へ自動補正する非破壊 NDMF ツール。
ランタイム監視（AFK / タイマー）はしない＝ default 補正のみの軽量実装。

## Current State

- 2026-06-19: テンプレから scaffold → annotated-plan-flow（research/plan は `plans/001_passive-filter-mvp/`、git 外）→ **MVP 実装完了（T1〜T8）**。
  - 実装: Runtime `PassiveFilterSettings`（INDMFEditorOnly）+ Editor `PassiveFilterPlugin`（Transforming.AfterPlugin("nadena.dev.modular-avatar")）+ Core（`ToggleScanner` / `HiddenBindingClassifier` / `MenuParameterCollector` / `PassiveFilterProcessor` / `PassiveFilterLog`）+ Inspector。
  - アクセス層は **生 AnimatorController 直読/直書き**（afk-manager 流。research の virtual 案から実装中に変更。plan D2 参照）。
  - **compile 検証 PASS**（Unity MCP: `Packages/manifest.json` に file 参照追加 → force refresh → 0 errors / 0 warnings、4 型コンパイル確認）。commit 済（`00ae8ee`）。
- **実機テスト（ririka でベイク, 2026-06-19）の発見**:
  - ✅ 統合は成立: NDMF パスが MA 後（Transforming）に発火、例外なし、ガード/ループ正常。
  - ⚠️ 検出 0 件。理由判明 = ririka(=paryi_FX) のオブジェクトトグルは**古典 2 ステート層でなく、ネスト Simple1D BlendTree** に集約（各トグル = `param=X, 子0=off(m_IsActive=0)/子1=on(=1)` の 2 子サブツリー）。現 MVP 検出器（古典 2 ステート狙い）は拾えない。
  - 📊 param default 実態: object トグルはほぼ `def=0`（fold=no-op で無害）/ `def=1` はギミック 2 個のみ / ほぼ全て `saved=True`（saved が default を上書き→所有者不変、効くのは未保存/local/remote 初期）。→「全トグル fold + Saved 尊重」は実データ上ほぼ安全（裸化懸念は概ね杞憂）。
  - dump 参照: `.work/pf_fx_dump.txt`（paryi BlendTree トグル構造の実例 + 全 expr param の default/synced/saved）。
- **BlendTree トグル対応 実装完了（2026-06-19, B1-B6）**: `BlendTreeToggleScanner`（Simple1D 再帰 walk・2 子 on/off 検出・深さ32+循環ガード）追加、`ToggleHideResult` を float/IsFloat へ統一（hide 判定は `HiddenBindingClassifier.GetHideTargets`/`TryResolveHidden` に共通化）、Generating 早期パスで base FX param を捕捉し option 2 除外（NDMF `GetState` 共有）、Processor で両スキャナ統合（driver 単位で targets union・hidden 値矛盾は安全側 skip）+ 型対応 default（Bool/Int/Float）。
  - **compile PASS（×2）+ 敵対的レビュー（4 次元・34 agent・実 dump 照合）+ ririka 実機検証（Play/ApplyOnPlay 経由, 0 error）**:
    - 既定（MenuTogglesOnly + option 2）: BlendTree 70 検出 → 35 unique（merge・値矛盾 0）→ base 34 除外 → **補正 0 = base 衣装を完全保護（裸化なし）**。
    - option 3（base 含む）+ AllToggles: 35 全 fold（cloth/Object/Particle/HeartGun(対象4)/mesugaki(対象8) 等）= 裸化＝opt-in 想定挙動を実証。
  - **コンポーネント別検証（自作テストアバター・手動ベイク[ManualProcessAvatar], 0 error）**: 古典 2 ステート検出器が 5 件検出（ririka は BlendTree のみだったため古典スキャナのライブ初検証）。ベイク後クローンで `MeshRenderer.enabled` / `ParticleSystem.emission.enabled` / `Light.enabled` / `AudioSource.enabled` / `GameObject.activeSelf` が全て false ＝**全 HideKind の検出+分類+scene disable を end-to-end 実証**。
  - 未了: MA 追加トグルの live fold 確認（Test 3）はせばんぬ後日ハンズオン（機構は Tests 1&2 の base 捕捉/除外 + レビューの MA phase timing 照合で裏付け済み）。commit はせばんぬ明示指示待ち。
- **BlendTree 対応の確定設計（2026-06-19、せばんぬ合意）**:
  - 検出: Direct/Simple1D BlendTree を再帰 walk し、2 子 on/off サブツリー（子0=off(値0)/子1=on(値1)）を検出。param は Float（blendParameter）。on/off 判定は古典スキャナの target 分類を流用。
  - 反映: param default を off 子の閾値（通常 0）へ（animator defaultFloat + expression defaultValue）。**Saved は触らない**（default-off は no-op、実効は default-ON のみ）。scene 直接 disable も併用（GameObject/Renderer/ParticleEmission/Light/Audio）。レイヤー初期 state 変更は BlendTree では不要。
  - targeting: **全 default-ON を off へ**（synced/local/saved 問わず）。
  - 対象範囲: **option 2 = base avatar FX を除外し MA 追加分中心**。早期パス（MA merge 前）で base FX の param 名を捕捉 → late パス（Transforming.AfterPlugin(MA)）で base param を除外。base 名は安定（MA は自分の param のみ rename）なので頑健。**3-ready**: base 除外フィルタを ON/OFF できる構造にし、将来 opt-in「全部」(option 3) を 1 手で追加可能に。
  - 既知の限界: option 2 はユーザーが base FX を直接編集したトグル / pure-base アバターを見逃す（= 将来の opt-in 全部でカバー）。VRCFury 追加分の base/MA 判定は未検証（未導入）。ParticleSystem の emission を `EmissionModule.rateOverTime.scalar` で制御するトグル（`EmissionModule.enabled` でない）は hide 対象クラス外で検出されない（silent skip・非破壊上は安全。ririka 'hand heart L/R' で実機確認）。
  - 哲学合意: 本ツールは初期状態を上書きする性質（作者設計尊重とは別）。狙いはユーザー目線のアクシデント（消し忘れ）救済。

## Current Blocker

なし。

## MVP 設計（凍結版 2026-06-19）

### 動作（非破壊・ビルド時）

1. NDMF パスを MA / VRCFury のベイク後に走らせる
2. その時点の素の FX AnimatorController + VRCExpressionParameters を走査
3. トグルレイヤーを検出し、各トグルが animate してる対象の「無効(0)側」を hidden と判定
4. param / レイヤー初期を hidden へ ＋ 対象コンポーネントを scene 上でも直接 disable（併用）。フレーム 0 から確実に隠す（WriteDefaults / 同期前フレームの隙も塞ぐ）
5. Saved フラグは触らない（＝初期値だけ hidden。意図的な ON 保存は尊重）
6. ルートの除外リストにある対象はスキップ
7. 判定が曖昧なトグル（off clip 無 / 逆トグル / 複数状態 / 1 トグルが複数 obj 制御）はスキップ＋警告
8. 何を hidden にしたかを NDMF コンソールへログ出力

### hide 対象コンポーネント

GameObject active / Renderer 全般（Skinned・Mesh・Line・Trail・Particle 描画＝ `Renderer.enabled`）/
ParticleSystem 本体 / Light / AudioSource（音の出っぱなし・視覚と別軸）。

### 対象範囲

既定 = メニュー由来トグルのみ（安全）。「非メニューも含む（全防ぎ）」は設定で opt-in。

### 混在対応

ベイク後走査なので MA / VRCFury / 素の手組み を一律カバー（基盤分岐コード不要）。

### 設定 UI

アバタールートに設定コンポーネント 1 個（NDMF が検出して発火）。
有効 ON/OFF / 対象範囲スイッチ / 除外リスト（コンポーネント参照）。

### MVP OUT（→ 次フェーズ候補）

int・radial トグル / blendshape・material トグル / show（逆出現）/
AFK・タイマー / 毎回強制 hide（Saved 無効化）。

### 実装 research で詰める論点

- NDMF パス順（MA / VRCFury 後を保証する run-after / BuildPhase 指定）
- hidden 判定の精度と安全弁（曖昧トグルのスキップ条件）
- WriteDefaults の扱い
- 設定コンポーネントの IEditorOnly 化（アップロードで strip）

## Rules

- 非破壊を最優先にし、既存データや既存設定を直接書き換える前に確認手段を用意する
- まず短い plan を出してから作業する
- commit / push は明示的な指示があるまで行わない
- Editor ファイルの namespace は `Sebanne.PassiveFilter.Editor`、Runtime ファイルの namespace は `Sebanne.PassiveFilter` に統一する

### コード変更の原則

- 変更した全ての行が、依頼内容に直接たどれること（判定基準）
- 元からあった死にコードはこのプロジェクトでは報告だけして消さない
- 依頼を成立させるための連鎖変更（Component → Editor → Plugin 等）は「直接関係する」に含む
- 事前に plan で承認されたリファクタ・構造変更はこの原則の対象外

## ファイル構成

- `Runtime/` — 設定コンポーネント（MonoBehaviour, IEditorOnly）。asmdef `Sebanne.PassiveFilter`
- `Editor/` — NDMF プラグイン（BuildPass）+ Inspector。asmdef `Sebanne.PassiveFilter.Editor`
- `package.json` — VPM package 定義（vpmDeps: `com.vrchat.avatars` / `nadena.dev.ndmf`）
- `README.md` / `TOOL_INFO.md` / `CHANGELOG.md` — 公開面
- `BOOTH_PACKAGE/` — BOOTH 同梱テキスト
- `Documentation~/` — notes（technical / guide-source / archive）/ images
- ※ Runtime / Editor の `.cs` 実装はまだ無し（scaffold 直後）

## 次フェーズ候補

Notion 次フェーズ候補 DB（リポ=passive-filter）を参照。
