# CLAUDE.md — windows-audio-switcher

Windows の常駐アプリ。ショートカットキーを押したままマウスホイールで音声の出力先を選び、キーを離すと切り替える。
仕様はユーザーの「Windows Audio Switcher アプリケーション要求仕様書」(2026-11-03)。要点は README と下。

**ユーザーへの返答・報告は日本語で。** 時刻は日本時間。確かめていないこと(特に実機の Windows での動き)は「未確認」と書く。

## 構成

```
src/AudioSwitcher.Core   Windows に依存しない中身(net8.0)。Linux(ko-pi)でもテストできる
  Models.cs              設定(AppSettings)・登録デバイス(DeviceEntry)・ショートカット(Hotkey)
  Selection.cs           選んでいる途中(SelectionSession: 未接続を飛ばす・循環・離したときだけ確定)、ホイールの段数
  HotkeyTracker.cs       キーの押し・離しからショートカットの開始/終了を判定。設定画面の記録
  DeviceRegistry.cs      登録の追加・削除・並べ替え・Windows の状態との突き合わせ(切断しても登録は残す)
  SettingsStore.cs       JSON の保存(%APPDATA%\AudioSwitcher\settings.json。こわれていたら別名で残して初期値)
  OverlayPlacement.cs    選択UIの位置(作業領域の角 = 右下ならタスクバーの時計の上)
src/AudioSwitcher.App    WPF 本体(net8.0-windows)。画面は XAML を使わずコードで組んでいる
  Interop/CoreAudio.cs   MMDevice API と IPolicyConfig(既定の出力先を変える。非公開だが Win7〜11 で同じ形)
  Services/InputHook.cs  低レベルのキーボード/マウスのフック。押している間だけホイールを横取り。Alt・Win を離したときにメニューが開かないよう 0xE8 を送る
  Views/OverlayWindow.cs 選択UI(フォーカスを取らない・クリックを通す)
  Views/SettingsWindow.cs 設定画面(操作・デバイスと順番・表示・アプリ)
  Program.cs             常駐・タスクトレイ・二重起動の防止・--self-test・--render-preview
  Preview.cs             本物のデバイスなしで、選択UIと設定画面を PNG にする
tests/AudioSwitcher.Core.Tests  xunit
```

## 開発の手順

1. ko-pi では `dotnet test tests/AudioSwitcher.Core.Tests`(中身のテスト)と `dotnet build src/AudioSwitcher.App`(Windows 向けのビルドが通るか。動かすことはできない)
2. push すると GitHub Actions(windows-2022)が: テスト → 1ファイルの exe(self-contained)→ `--self-test`(デバイスを読めるか)→ `--render-preview`(画面の画像、成果物 `preview`)
3. 見た目を変えたら、Actions の成果物 `preview` を `gh run download -n preview` で取って、画像を見て確かめる
4. 版を出すとき: `src/AudioSwitcher.App/AudioSwitcher.App.csproj` の `<Version>` と app.manifest を上げ、タグ `vX.Y.Z` を push → Release(固定リンク `releases/latest/download/AudioSwitcher.exe`)
5. 完成品は NAS の `make/somethingプログラム/windows-audio-switcher/<版>/` にも置く(Release と同じ exe。ハッシュを確かめる)。`GitHubのリンク.md` と `最新版をダウンロード（GitHub）.url` も

## 決めたこと

- 既定のショートカットは Ctrl + Alt(ホイールと組み合わせて使うアプリが少ない)。ショートカットに別のキーが足されたら(Ctrl+Alt+Del など)選択UIを引っこめて切り替えない
- ホイールを回さずに離したら何もしない。今の出力先にもどして離しても何もしない
- 切り替えは既定で「すべて(Console・Multimedia・Communications)」。設定で通話用はそのままにもできる
- 初回だけ、今つながっている出力デバイスを全部登録する(あとは手で追加・削除)
- 接続は Windows の DEVICE_STATE_ACTIVE。無効にしたデバイスも「未接続」扱い
- 未接続のデバイスは選ぶときに飛ばす。登録は残す。つながっても自動で切り替えない(仕様)

## 引き継ぎ

区切りごとに HANDOFF.md を更新して commit・push。リポジトリは非公開。秘密(トークンなど)は書かない。
