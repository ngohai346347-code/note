using System.Text.Json.Serialization;

namespace EnglishFlashcard3D.Models;

public class LoginResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("user")]
    public AuthUser? User { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public class AuthUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "student";
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";
    [JsonPropertyName("department")]
    public string? Department { get; set; }
}

public class TopicsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("topics")]
    public List<ApiTopic> Topics { get; set; } = new();
}

public class ApiTopic
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = "ALL";

    [JsonPropertyName("card_count")]
    public int CardCount { get; set; }
}

public class GenerateTopicResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("topic")]
    public ApiTopic? Topic { get; set; }
}

public class GenerateBatchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("topic")]
    public ApiTopic? Topic { get; set; }

    [JsonPropertyName("new_cards")]
    public List<ApiVocabCard> NewCards { get; set; } = new();

    [JsonPropertyName("total_cards")]
    public int TotalCards { get; set; }
}

public class TopicCardsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("topic")]
    public ApiTopic? Topic { get; set; }

    [JsonPropertyName("cards")]
    public List<ApiVocabCard> Cards { get; set; } = new();
}

public class ApiVocabCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("word_en")]
    public string Word_En { get; set; } = string.Empty;

    [JsonPropertyName("ipa_en")]
    public string? Ipa_En { get; set; }

    [JsonPropertyName("meaning_vi")]
    public string Meaning_Vi { get; set; } = string.Empty;

    [JsonPropertyName("example_en")]
    public string Example_En { get; set; } = string.Empty;

    [JsonPropertyName("example_meaning_vi")]
    public string Example_Meaning_Vi { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string? Difficulty { get; set; }

    [JsonPropertyName("learned")]
    public bool Learned { get; set; }
}

public class AssignmentsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("assignments")]
    public List<ApiAssignment> Assignments { get; set; } = new();
}

public class ApiAssignment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("skill")]
    public string Skill { get; set; } = "writing";

    [JsonPropertyName("target_level")]
    public string Target_Level { get; set; } = "B1";

    [JsonPropertyName("instructions")]
    public string Instructions { get; set; } = string.Empty;
    [JsonPropertyName("due_date")]
    public string? Due_Date { get; set; }
    [JsonPropertyName("submission_count")]
    public int Submission_Count { get; set; }
}

public class SubmissionsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("submissions")]
    public List<ApiSubmission> Submissions { get; set; } = new();
}

public class ApiSubmission
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("assignment_title")]
    public string Assignment_Title { get; set; } = string.Empty;

    [JsonPropertyName("student_name")]
    public string Student_Name { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("feedback")]
    public string? Feedback { get; set; }
    [JsonPropertyName("assignment_id")]
    public string Assignment_Id { get; set; } = string.Empty;
    [JsonPropertyName("student_id")]
    public string Student_Id { get; set; } = string.Empty;
    [JsonPropertyName("assessed_level")]
    public string? Assessed_Level { get; set; }
    [JsonPropertyName("submitted_at")]
    public string Submitted_At { get; set; } = string.Empty;
}

public class ApiStatsResponse { [JsonPropertyName("stats")] public ApiStats Stats { get; set; } = new(); }
public class ApiStats
{
    [JsonPropertyName("total_students")] public int Total_Students { get; set; }
    [JsonPropertyName("total_experts")] public int Total_Experts { get; set; }
    [JsonPropertyName("total_topics")] public int Total_Topics { get; set; }
    [JsonPropertyName("total_cards")] public int Total_Cards { get; set; }
    [JsonPropertyName("total_assignments")] public int Total_Assignments { get; set; }
    [JsonPropertyName("total_submissions")] public int Total_Submissions { get; set; }
    [JsonPropertyName("total_api_calls")] public int Total_Api_Calls { get; set; }
}
public class UsersResponse { [JsonPropertyName("users")] public List<AuthUser> Users { get; set; } = new(); }
public class AiConfigResponse { [JsonPropertyName("config")] public ApiAiConfig Config { get; set; } = new(); }
public class ApiAiConfig
{
    [JsonPropertyName("model_name")] public string Model_Name { get; set; } = "gemini-3.8-flash";
    [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.4;
    [JsonPropertyName("max_output_tokens")] public int Max_Output_Tokens { get; set; } = 4096;
    [JsonPropertyName("anti_repetition")] public bool Anti_Repetition { get; set; } = true;
    [JsonPropertyName("system_instruction")] public string System_Instruction { get; set; } = "";
}
public class ApiLogItem
{
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    [JsonPropertyName("method")] public string Method { get; set; } = "";
    [JsonPropertyName("endpoint")] public string Endpoint { get; set; } = "";
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("duration_ms")] public int Duration_Ms { get; set; }
}
public class LogsResponse { [JsonPropertyName("logs")] public List<ApiLogItem> Logs { get; set; } = new(); }
