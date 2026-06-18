# Passive Filter

VRChat アバターのトグルギミックが、他人視点で「出っぱなし」になるのを防ぐ非破壊ツールです。ビルド時に各ギミックの初期状態を自動で「非表示」へ倒します。

## 何ができるか

- アバター内のトグルギミックを自動検出し、初期状態を「非表示」へ補正します
- 対象は GameObject / メッシュ（Renderer）/ パーティクル / ライト / 音（AudioSource）
- 「他人視点で勝手に出る」原因（同期ズレ・デフォルト ON・ローカル専用トグル）を塞ぎます
- 除外リストで「常時出したい物」は対象外にできます
- 元のアバターには直接変更を加えない非破壊設計です（NDMF のビルド時に適用）

## 対応環境

- Unity `2022.3`
- VRChat SDK（Avatars）
- VCC / VPM ベースの VRChat プロジェクトを推奨します

## VCC / VPM での導入

### 推奨: VCC / VPM から導入

1. VCC に追加する URL として `https://sebanne1225.github.io/sebanne-listing/index.json` を追加します。
2. package 一覧から `Passive Filter` (`com.sebanne.passive-filter`) を追加します。
3. Unity を開き、package が導入されていることを確認します。

参考ページ (`VCC` 追加先ではありません): `https://sebanne1225.github.io/sebanne-listing/`

### 補助: Git URL / Release zip から導入

- repo: `https://github.com/sebanne1225/passive-filter`
- Git URL や local package での導入は、開発確認や手動検証向けの補助導線です
- GitHub Release の zip も補助導線として使えます。`com.sebanne.passive-filter-<version>.zip` を展開すると、直下に `package.json` が見える package 構成です

## 使い方

1. アバターのルートに `Passive Filter` コンポーネントを追加します
2. 必要なら除外リスト（常時表示したい物）と対象範囲（メニュー外トグルも含むか）を設定します
3. いつも通りアップロードします。ビルド時に各ギミックが初期非表示へ自動補正されます

何を非表示にしたか・スキップしたかは、ビルド時に NDMF コンソールへ出力されます。

## 制限事項

- 初期値のみを非表示化します（Saved で意図的に表示保存した物は、その意思を尊重して触りません）
- 対象は bool トグル × GameObject / Renderer / ParticleSystem / Light / AudioSource です。int・radial・blendshape のトグルは未対応です（次バージョン候補）
- 判定が曖昧なトグルは安全のためスキップします（NDMF コンソールに警告を出します）

## ライセンス

MIT License です。詳細は `LICENSE` を参照してください。
