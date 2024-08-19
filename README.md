# webrtc-test

- WebRTCを使ったGUIアプリです。1対1の双方向の映像通信が行えます。
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
- Ayame の情報を test.cofig に記述し、実行ファイルと同じフォルダに置く必要があります。（詳細は別途）
- 実行時に mrwebrtc.dll のロード失敗のようなエラーが出た場合は、実行ファイル (Test.exe) と同じフォルダに mrwebrtc.dll をコピーしてください。mrwebrtc.dll は、プロジェクトのbinフォルダのずっと下、runtimes/winXX-XXX/native の下あたりにあります。

### 操作方法
- 「送受信」「送信のみ」「受信のみ」のどれかを選んで、「開始」ボタンを押してください。
- 相手と接続が完了し通信が始まると、上部に「稼働中」と表示されます。

### 問題点
- 自分と相手で、このアプリをそれぞれ起動するとき、先に「開始」したアプリの映像が送信されない場合があります。
(Answer時のSDPデータが、a=inactiveになってしまう)
- 後から「開始」したアプリの映像送信はうまくいきます。
