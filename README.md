# webrtc-test

- WebRTCを使ったGUIアプリです。双方向の映像通信が行えます。
- 映像のみです、音声はありません。
- STUN/TURN/シグナリングサーバーは、テスト用として [Ayame](https://ayame-labo.shiguredo.app/) を使用しております。

### 開発環境
- Windows 11
- Visual Studio 2022
- C# WPF
- Microsoft.MixedReality.WebRTC（WebRTCのライブラリ）
- WebSocketSharp (WebSocketのライブラリ)

### ビルド
- Windows環境で、Visual Studio を使って行ってください。

### 実行方法
- Test.exe ファイルを実行してください。
- Windows 環境であれば動作すると思います。
- 実行時に mrwebrtc.dll のロード失敗のようなエラーが出た場合は、実行ファイル (Test.exe) と同じフォルダに mrwebrtc.dll をコピーしてください。mrwebrtc.dll は、プロジェクトのbinフォルダのずっと下、runtimes/winXX-XXX/native の下あたりにあります。
- Ayame の情報を test.cofig に記述し、実行ファイルと同じフォルダに置く必要があります。
