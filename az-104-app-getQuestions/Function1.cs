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

            // ���ϐ�����Azure AD�̔F�؏����擾
            string tenantId = Environment.GetEnvironmentVariable("SQL_TENANT_ID");
            string clientId = Environment.GetEnvironmentVariable("SQL_CLIENT_ID");
            string clientSecret = Environment.GetEnvironmentVariable("SQL_CLIENT_SECRET");

            // �F�ؗp�̃N���C�A���g�A�v���P�[�V�������\�z
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            // �f�[�^�x�[�X�p�̃X�R�[�v���w�肵�ăA�N�Z�X�g�[�N�����擾
            string[] scopes = new string[] { "https://database.windows.net/.default" };
            AuthenticationResult result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            string accessToken = result.AccessToken;

            // �f�[�^�x�[�X�ڑ���������\�z
            string server = Environment.GetEnvironmentVariable("SQL_SERVER");
            string database = Environment.GetEnvironmentVariable("SQL_DATABASE");
            string connectionString = $"Server={server};Database={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

            // �N�G���p�����[�^����J�e�S�������擾
            string categoryName = req.Query["categoryName"];

            if (string.IsNullOrEmpty(categoryName))
            {
                return new BadRequestObjectResult("Please provide a valid categoryName.");
            }

            // ������i�[����f�B�N�V���i����������
            var questionsDict = new Dictionary<int, Question>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.AccessToken = accessToken; // �f�[�^�x�[�X�ڑ��ɃA�N�Z�X�g�[�N�����g�p
                    await conn.OpenAsync();

                    // �J�e�S��������CategoryID���擾����N�G��
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

                    // ����ƑI�������擾����N�G��
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

                            // �f�o�b�O�p���O
                            log.LogInformation($"Reading QuestionID: {questionId}");

                            // ���₪�f�B�N�V���i���ɑ��݂��Ȃ��ꍇ�A�V���������ǉ�
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

                                // �f�o�b�O�p���O
                                log.LogInformation($"QuestionID: {questionId}, ImagePath: {questionsDict[questionId].ImagePath}");
                            }

                            // ����ɑ΂���I������ǉ�
                            questionsDict[questionId].Choices.Add(new Choice
                            {
                                ChoiceID = reader.GetInt32(reader.GetOrdinal("ChoiceID")),
                                ChoiceText = reader.GetString(reader.GetOrdinal("ChoiceText")),
                                IsCorrect = reader.GetBoolean(reader.GetOrdinal("IsCorrect"))
                            });
                        }
                    }
                }

                // ����ƑI�����̃l�X�g�����\�������O�ɏo��
                var questions = questionsDict.Values.ToList();
                log.LogInformation($"Questions: {JsonConvert.SerializeObject(questions)}");

                return new OkObjectResult(questions);
            }
            catch (Exception ex)
            {
                // �G���[�������̃��O�o��
                log.LogError($"Error during database operation: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        // �����\���N���X
        public class Question
        {
            public int QuestionID { get; set; }
            public string QuestionText { get; set; }
            public string AnswerExplanation { get; set; }
            public string ImagePath { get; set; }
            public List<Choice> Choices { get; set; }
        }

        // �I������\���N���X
        public class Choice
        {
            public int ChoiceID { get; set; }
            public string ChoiceText { get; set; }
            public bool IsCorrect { get; set; }
        }
    }
}
