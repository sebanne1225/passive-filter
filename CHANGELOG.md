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
  - base avatar 由来のトグルは既定で除外し、MA / VRCFury 追加分を中心に補正（base FX のパラメータを MA merge 前に捕捉）
- MA リアクティブトグル対応: Modular Avatar の Object Toggle / Menu Item を直読し、初期表示される単純トグルをビルド前に初期非表示へ補正（MA が Direct BlendTree に畳む前に MA の作法で補正）。複雑なトグル（無条件 / 複数同パラメータ / 多状態 / 連鎖 等）は安全のためスキップして警告。Modular Avatar 未導入の環境では無効（古典 / Simple1D 検出はそのまま動作）
- 対象範囲を 2 軸 UI に整理: 「対象範囲」（メニューのみ / 全トグル）と「補正する対象」（あとから足したギミックだけ〔安全〕 / 全部〔元アバター含む〕）を Inspector に公開。危険な組合せ（全トグル × 全部）の選択時は裸化注意を表示
