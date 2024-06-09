# 問題取得用のAzure Functions

AZ-104 TrainingApp(https://github.com/kazuhiro-ogawa/az-104-react-app.git)  
で使用する、問題取得用のAPIです。Azure FunctionsでC#を用いて作成しています。
HTTPリクエストがトリガーでAzure SQL Databaseから問題を取得し、レスポンスで取得した問題を返します。

## システム構成図
![システム構成図_azurefunctions_getquestion](https://github.com/kazuhiro-ogawa/az-104-app-getQuestions/assets/105719508/3f01af61-49c6-4991-872d-5526246d2c1d)
