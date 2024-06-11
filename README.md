# 問題取得用のAzure Functions

AZ-104 TrainingApp(https://github.com/kazuhiro-ogawa/az-104-react-app.git)  
で使用する、問題取得用のAPIです。Azure FunctionsでC#を用いて作成しています。
HTTPリクエストがトリガーでAzure SQL Databaseから問題を取得し、レスポンスで取得した問題を返します。

## システム構成図
![getquestion](https://github.com/kazuhiro-ogawa/az-104-app-getQuestions/assets/105719508/171c5699-9c69-4cff-8677-fb7843f61aeb)
