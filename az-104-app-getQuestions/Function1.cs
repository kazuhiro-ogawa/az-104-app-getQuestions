using System;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace az_104_app_getQuestions
{
    public static class Function1
    {
        [FunctionName("GetQuestionsByCategory")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // 環境変数からAzure ADの認証情報を取得
            string tenantId = Environment.GetEnvironmentVariable("SQL_TENANT_ID");
            string clientId = Environment.GetEnvironmentVariable("SQL_CLIENT_ID");
            string clientSecret = Environment.GetEnvironmentVariable("SQL_CLIENT_SECRET");

            // 認証用のクライアントアプリケーションを構築
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            // データベース用のスコープを指定してアクセストークンを取得
            string[] scopes = new string[] { "https://database.windows.net/.default" };
            AuthenticationResult result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            string accessToken = result.AccessToken;

            // データベース接続文字列を構築
            string server = Environment.GetEnvironmentVariable("SQL_SERVER");
            string database = Environment.GetEnvironmentVariable("SQL_DATABASE");
            string connectionString = $"Server={server};Database={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

            // クエリパラメータからカテゴリ名を取得
            string categoryName = req.Query["categoryName"];

            if (string.IsNullOrEmpty(categoryName))
            {
                return new BadRequestObjectResult("Please provide a valid categoryName.");
            }

            // 質問を格納するディクショナリを初期化
            var questionsDict = new Dictionary<int, Question>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.AccessToken = accessToken; // データベース接続にアクセストークンを使用
                    await conn.OpenAsync();

                    // カテゴリ名からCategoryIDを取得するクエリ
                    string getCategoryIDQuery = "SELECT CategoryID FROM Categories WHERE Category = @Category";
                    int categoryId;

                    using (SqlCommand getCategoryCmd = new SqlCommand(getCategoryIDQuery, conn))
                    {
                        getCategoryCmd.Parameters.AddWithValue("@Category", categoryName);
                        object resultObj = await getCategoryCmd.ExecuteScalarAsync();
                        if (resultObj == null)
                        {
                            return new BadRequestObjectResult("Category not found.");
                        }
                        categoryId = Convert.ToInt32(resultObj);
                    }

                    // 質問と選択肢を取得するクエリ
                    string query = @"
                        SELECT 
                            q.QuestionID,
                            q.QuestionText,
                            q.AnswerExplanation,
                            q.ImageID,
                            ISNULL(i.ImagePath, '') AS ImagePath,
                            c.ChoiceID,
                            c.ChoiceText,
                            c.IsCorrect
                        FROM 
                            Questions q
                        LEFT JOIN 
                            Choices c ON q.QuestionID = c.QuestionID
                        LEFT JOIN
                            Images i ON q.ImageID = i.ImageID
                        WHERE 
                            q.CategoryID = @CategoryID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CategoryID", categoryId);

                        SqlDataReader reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            int questionId = reader.GetInt32(reader.GetOrdinal("QuestionID"));

                            // デバッグ用ログ
                            log.LogInformation($"Reading QuestionID: {questionId}");

                            // 質問がディクショナリに存在しない場合、新しい質問を追加
                            if (!questionsDict.ContainsKey(questionId))
                            {
                                questionsDict[questionId] = new Question
                                {
                                    QuestionID = questionId,
                                    QuestionText = reader.GetString(reader.GetOrdinal("QuestionText")),
                                    AnswerExplanation = reader.GetString(reader.GetOrdinal("AnswerExplanation")),
                                    ImagePath = reader.GetString(reader.GetOrdinal("ImagePath")),
                                    Choices = new List<Choice>()
                                };

                                // デバッグ用ログ
                                log.LogInformation($"QuestionID: {questionId}, ImagePath: {questionsDict[questionId].ImagePath}");
                            }

                            // 質問に対する選択肢を追加
                            questionsDict[questionId].Choices.Add(new Choice
                            {
                                ChoiceID = reader.GetInt32(reader.GetOrdinal("ChoiceID")),
                                ChoiceText = reader.GetString(reader.GetOrdinal("ChoiceText")),
                                IsCorrect = reader.GetBoolean(reader.GetOrdinal("IsCorrect"))
                            });
                        }
                    }
                }

                // 質問と選択肢のネストした構造をログに出力
                var questions = questionsDict.Values.ToList();
                log.LogInformation($"Questions: {JsonConvert.SerializeObject(questions)}");

                return new OkObjectResult(questions);
            }
            catch (Exception ex)
            {
                // エラー発生時のログ出力
                log.LogError($"Error during database operation: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        // 質問を表すクラス
        public class Question
        {
            public int QuestionID { get; set; }
            public string QuestionText { get; set; }
            public string AnswerExplanation { get; set; }
            public string ImagePath { get; set; }
            public List<Choice> Choices { get; set; }
        }

        // 選択肢を表すクラス
        public class Choice
        {
            public int ChoiceID { get; set; }
            public string ChoiceText { get; set; }
            public bool IsCorrect { get; set; }
        }
    }
}
