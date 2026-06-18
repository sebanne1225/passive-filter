# Passive Filter — repo 現況メモ

VRChat 向け、ギミックが「他人視点で出っぱなし」になるのを防ぐ非破壊 NDMF ツール。
ビルド時に各トグルの初期状態を hidden 側へ倒す。

## Goal

アバターのトグルギミック（オブジェクト / メッシュ / パーティクル / ライト / 音）が、
同期ズレ・デフォルト ON・ローカル専用などの理由で「他人視点で勝手に出っぱなし」に
なるのを防ぐ。ビルド時に各トグルの初期状態を hidden 側へ自動補正する非破壊 NDMF ツール。
ランタイム監視（AFK / タイマー）はしない＝ default 補正のみの軽量実装。

## Current State

- 2026-06-19: テンプレ（`sebanne-unity-vrchat-tool-template`）から scaffold（初期 commit `e5dc469`）。MVP 設計は凍結済（下記）。**実装は未着手**（Runtime / Editor の .cs はまだ無い）。
- 次の一手: [[skills/annotated-plan-flow/SKILL]] の research フェーズで実装論点（NDMF パス順 / hidden 判定 / WriteDefaults / IEditorOnly）を詰める → 実装。

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
