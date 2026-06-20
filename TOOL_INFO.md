# TOOL_INFO

このファイルは、`Passive Filter` の repo 補助文書です。README の代わりではなく、公開準備や listing 反映時に確認したい情報を短くまとめています。

## 基本情報

- ツール名: `Passive Filter`
- package名: `com.sebanne.passive-filter`
- 表示名: `Passive Filter`
- Runtime asmdef: `Sebanne.PassiveFilter`
- Editor asmdef: `Sebanne.PassiveFilter.Editor`
- 現在 version: `1.0.0`

## 公開メタ情報

- GitHub repo: `https://github.com/sebanne1225/passive-filter`
- changelogUrl: `https://github.com/sebanne1225/passive-filter/blob/main/CHANGELOG.md`
- listing repo: `https://github.com/sebanne1225/sebanne-listing`
- 参考 listing page (`VCC` 追加先ではない): `https://sebanne1225.github.io/sebanne-listing/`
- VCC に追加する URL: `https://sebanne1225.github.io/sebanne-listing/index.json`
- listing 側に追加する `githubRepos`: `sebanne1225/passive-filter`
- BOOTH 販売名: TBD（候補: パッシブフィルター）

## 公開スコープの要約

- トグルギミックの初期状態を「非表示」へ自動補正し、他人視点の出っぱなしを防ぐ
- 対象: GameObject active / Renderer 全般 / ParticleSystem / Light / AudioSource
- 非破壊（NDMF ビルド時に適用）。除外リスト・対象範囲スイッチあり

## 導入導線の前提

- 主導線は VCC / VPM
- Git URL / local package 導入は補助扱い

## 既知の制限

- 初期値のみ非表示化（Saved 尊重）。int / radial / blendshape トグルは未対応
- 判定が曖昧なトグルは安全のためスキップ（コンソールに警告）
