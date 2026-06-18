# Changelog

このファイルは `Passive Filter` の変更履歴を管理します。

## [0.1.0] - YYYY-MM-DD

### Added

- テンプレートから初期立ち上げ。repo 名・package id・namespace をツール固有の識別子に置換
- MVP 実装: ビルド時にアバターのトグルギミックの初期状態を「非表示」へ自動補正する非破壊 NDMF プラグイン
  - 対象コンポーネント: GameObject active / Renderer / ParticleSystem(emission) / Light / AudioSource
  - 対象範囲スイッチ（メニュー由来のみ / 全トグル）+ 除外リスト（サブツリー一括）
  - 判定が曖昧なトグルはスキップして警告ログ、適用結果を Unity コンソールへ出力
  - 初期値のみ補正（Saved は尊重）。設定コンポーネントは IEditorOnly でアップロード時に strip
