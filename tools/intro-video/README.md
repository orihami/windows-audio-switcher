# 紹介動画の作り方

1. GitHub Actions の成果物 `preview`(アプリ本物の選択UI・設定画面の画像。`video-*-dark.png` は背景が透明)を取る
   `gh run download <run id> -R orihami/windows-audio-switcher -n preview -D pv`
2. `python3 tools/intro-video/make_intro.py pv src/AudioSwitcher.App/app.ico AudioSwitcher-intro.mp4`
   (確認用の静止画は、最後に `--stills` を付ける)

必要: Python の Pillow、ffmpeg、Noto Sans CJK。1920×1080・30fps・39秒・音なし。
