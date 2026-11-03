# HANDOFF

- 最終更新: 2026-11-03（日本時間） / 作業場所: ko-pi

## 今の状態
- 0.1.0 を Release(固定リンク releases/latest/download/AudioSwitcher.exe)。NAS の make/somethingプログラム/windows-audio-switcher/0.1.0/ にも同じ exe。仕様書の機能は全部入れた(ショートカット+ホイールで選択・離して確定、登録デバイスの追加/削除/表示名/使う・使わない/接続状態/並べ替え、循環、選択UI(右下・時計の上)、設定画面(操作・デバイスと順番・表示・アプリ)、自動起動、タスクトレイ)
- 確認済み: ko-pi で中身のテスト(Core 18件)、Windows 向けのビルド。GitHub Actions(windows-2022)で exe 作成・起動(--self-test。CI にはデバイスがないので0個)・画面の画像(選択UI 3場面・設定4ページ × ダーク/ライト)を見て確認

## 未確認
- **実機の Windows での動き全部**(ショートカットとホイール、切り替え、Bluetooth イヤホンの接続/切断の反映、自動起動、タスクトレイ、DPI の違う画面)。ko-pi には Windows がないので、ユーザーに試してもらう

## 次にやること
- ユーザーに試してもらい、感想で直す
