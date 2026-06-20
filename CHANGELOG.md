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
- BlendTree トグル対応: ネスト Simple1D BlendTree に集約されたトグル（最適化アバターで一般的）も検出・補正
  - 古典 2 ステートと BlendTree の両検出器を統合。同一パラメータの結果は統合し、hidden 値が矛盾する場合は安全側にスキップ
  - base avatar 由来のトグルは既定で除外し、MA / VRCFury 追加分を中心に補正（base FX のパラメータを MA merge 前に捕捉。`IncludeBaseAvatarToggles` で全対象へ切替可能な構造）
