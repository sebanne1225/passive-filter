# Changelog

このファイルは `Passive Filter` の変更履歴を管理します。

## [1.0.0] - 2026-06-21

初回リリース。

### Added

- 非破壊 NDMF プラグイン: ビルド時にアバターのトグルギミックの初期状態を「非表示」へ自動補正
  - 対象コンポーネント: GameObject active / Renderer / ParticleSystem(emission) / Light / AudioSource
  - 初期値のみ補正し、Saved（保存表示）は尊重。設定コンポーネントは IEditorOnly でアップロード時に strip
  - 対象範囲（メニュー由来のみ / 全トグル）と補正する対象（あとから足したギミックだけ〔安全〕 / 全部〔元アバター含む〕）の 2 軸設定 + 除外リスト（サブツリー一括）
  - 危険な組合せ（全トグル × 全部）選択時は裸化注意を表示
- トグル検出:
  - 古典 2 ステートトグル
  - Simple1D BlendTree に集約されたトグル（最適化アバターで一般的な 2 子 on/off）
  - 多バリアント(>2 子)の clean な Simple1D 変種トグル
  - 複数対象を多重切替する packed / mixed BlendTree（安全に補正できないため検出して通知のみ・補正はしない）
  - Modular Avatar リアクティブトグル（Object Toggle / Menu Item を直読し、MA がベイクする前に初期非表示へ整合補正）。MA 未導入環境では無効（古典 / BlendTree 検出はそのまま動作）
  - base avatar 由来のトグルは既定で除外し、後付け（MA / VRCFury）分を中心に補正。同一パラメータの結果は統合し、hidden 値が矛盾する場合・判定が曖昧な場合は安全側にスキップ
- 通知: 安全に補正できなかったトグルを NDMF のエラーレポートへ集約通知（クリックで対象へジャンプ）。検出・補正の詳細ログを Unity コンソールへ出すオプション（既定オフ）
