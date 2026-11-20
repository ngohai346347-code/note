using System.Net.Http;
using System.Text;
using System.Text.Json;
using EnglishFlashcard3D.Models;

namespace EnglishFlashcard3D.Services;

public class ApiClientService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ApiClientService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5046/") };
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var payload = JsonSerializer.Serialize(new { email, password });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/auth/login", content);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions);
    }

    public async Task<LoginResponse?> RegisterAsync(string name, string email, string password)
    {
        var payload = JsonSerializer.Serialize(new { name, email, password });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/auth/register", content);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions);
    }

    public async Task<List<TopicDeck>> GetTopicsAsync(string userId)
    {
        var json = await _httpClient.GetStringAsync($"api/vocab/topics?user_id={Uri.EscapeDataString(userId)}");
        var response = JsonSerializer.Deserialize<TopicsResponse>(json, JsonOptions);
        if (response is null) return new List<TopicDeck>();

        return response.Topics.Select(t => new TopicDeck
        {
            Id = t.Id,
            Name = t.Name,
            Level = t.Level,
            CardCount = t.CardCount
        }).ToList();
    }

    public async Task<TopicDeck?> GenerateTopicAsync(string topicName, string level, string userId)
    {
        var payload = JsonSerializer.Serialize(new { topic = topicName, level, user_id = userId });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/vocab/generate", content);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = $"API từ vựng trả về HTTP {(int)response.StatusCode}.";
            try
            {
                using var error = JsonDocument.Parse(json);
                if (error.RootElement.TryGetProperty("error", out var detail)) message = detail.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new HttpRequestException(message);
        }
        var model = JsonSerializer.Deserialize<GenerateTopicResponse>(json, JsonOptions);
        if (model?.Topic is null) return null;

        return new TopicDeck
        {
            Id = model.Topic.Id,
            Name = model.Topic.Name,
            Level = model.Topic.Level,
            CardCount = model.Topic.CardCount
        };
    }

    /// <summary>F2.3: Sinh đợt thêm 15 từ mới cho topic đã có, chống trùng từ.</summary>
    public async Task<GenerateBatchResponse?> GenerateBatchAsync(string topicId, string userId)
    {
        var payload = JsonSerializer.Serialize(new { topic_id = topicId, user_id = userId });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/vocab/generate-batch", content);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = $"API trả về HTTP {(int)response.StatusCode}.";
            try
            {
                using var error = JsonDocument.Parse(json);
                if (error.RootElement.TryGetProperty("error", out var detail)) message = detail.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new HttpRequestException(message);
        }
        return JsonSerializer.Deserialize<GenerateBatchResponse>(json, JsonOptions);
    }

    /// <summary>F2.5: Xóa toàn bộ chủ đề và flashcard liên quan.</summary>
    public async Task<bool> DeleteTopicAsync(string topicId, string userId)
    {
        var response = await _httpClient.DeleteAsync($"api/vocab/{Uri.EscapeDataString(topicId)}?user_id={Uri.EscapeDataString(userId)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<EnglishFlashcard>> GetTopicCardsAsync(string topicId, string userId)
    {
        var json = await _httpClient.GetStringAsync($"api/vocab/{topicId}?user_id={Uri.EscapeDataString(userId)}");
        var response = JsonSerializer.Deserialize<TopicCardsResponse>(json, JsonOptions);
        if (response is null) return new List<EnglishFlashcard>();

        return response.Cards.Select(c => new EnglishFlashcard
        {
            Id = c.Id,
            Word = c.Word_En,
            Phonetic = c.Ipa_En ?? string.Empty,
            MeaningVi = c.Meaning_Vi,
            ExampleEn = c.Example_En,
            ExampleVi = c.Example_Meaning_Vi,
            PartOfSpeech = c.Difficulty ?? string.Empty,
            IsMastered = c.Learned
        }).ToList();
    }

    public async Task<bool> MarkCardLearnedAsync(string cardId, string userId)
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"api/vocab/cards/{cardId}/toggle-learned?user_id={Uri.EscapeDataString(userId)}", content);
        return response.IsSuccessStatusCode;
    }
    public async Task<List<ApiAssignment>> GetStudentAssignmentsAsync()
    {
        var json = await _httpClient.GetStringAsync($"api/student/assignments");
        var response = JsonSerializer.Deserialize<AssignmentsResponse>(json, JsonOptions);
        return response?.Assignments ?? new List<ApiAssignment>();
    }

    public async Task<List<ApiAssignment>> GetExpertAssignmentsAsync(string expertId)
    {
        var json = await _httpClient.GetStringAsync($"api/expert/assignments?expert_id={Uri.EscapeDataString(expertId)}");
        return JsonSerializer.Deserialize<AssignmentsResponse>(json, JsonOptions)?.Assignments ?? new();
    }

    public async Task<bool> CreateAssignmentAsync(string expertId, string expertName, string title, string skill, string level, string instructions, string dueDate)
    {
        var payload = JsonSerializer.Serialize(new { expert_id = expertId, expert_name = expertName, title, skill, target_level = level, instructions, due_date = dueDate });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return (await _httpClient.PostAsync("api/expert/assignments", content)).IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAssignmentAsync(string id) => (await _httpClient.DeleteAsync($"api/expert/assignments/{Uri.EscapeDataString(id)}")).IsSuccessStatusCode;

    public async Task<bool> SubmitAssignmentAsync(string assignmentId, string studentId, string studentName, string contentText)
    {
        var payload = JsonSerializer.Serialize(new { student_id = studentId, student_name = studentName, content = contentText });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"api/student/assignments/{assignmentId}/submit", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ApiSubmission>> GetStudentSubmissionsAsync(string studentId)
    {
        var json = await _httpClient.GetStringAsync($"api/student/submissions?student_id={Uri.EscapeDataString(studentId)}");
        var response = JsonSerializer.Deserialize<SubmissionsResponse>(json, JsonOptions);
        return response?.Submissions ?? new List<ApiSubmission>();
    }

    public async Task<List<ApiSubmission>> GetExpertSubmissionsAsync()
    {
        var json = await _httpClient.GetStringAsync("api/expert/submissions");
        var response = JsonSerializer.Deserialize<SubmissionsResponse>(json, JsonOptions);
        return response?.Submissions ?? new List<ApiSubmission>();
    }

    public async Task<bool> GradeSubmissionAsync(string submissionId, double score, string feedback, string gradedBy, string assessedLevel)
    {
        var payload = JsonSerializer.Serialize(new { score, feedback, graded_by = gradedBy, assessed_level = assessedLevel });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"api/expert/submissions/{submissionId}/grade", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<ApiStats> GetAdminStatsAsync() => JsonSerializer.Deserialize<ApiStatsResponse>(await _httpClient.GetStringAsync("api/admin/stats"), JsonOptions)?.Stats ?? new();
    public async Task<List<AuthUser>> GetUsersAsync() => JsonSerializer.Deserialize<UsersResponse>(await _httpClient.GetStringAsync("api/auth/users"), JsonOptions)?.Users ?? new();
    public async Task<bool> UpdateUserAsync(string id, string role, string status)
    {
        var payload = JsonSerializer.Serialize(new { role, status });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return (await _httpClient.PutAsync($"api/auth/users/{Uri.EscapeDataString(id)}", content)).IsSuccessStatusCode;
    }
    public async Task<ApiAiConfig> GetAiConfigAsync() => JsonSerializer.Deserialize<AiConfigResponse>(await _httpClient.GetStringAsync("api/admin/ai-config"), JsonOptions)?.Config ?? new();
    public async Task<bool> UpdateAiConfigAsync(ApiAiConfig config)
    {
        using var content = new StringContent(JsonSerializer.Serialize(config), Encoding.UTF8, "application/json");
        return (await _httpClient.PutAsync("api/admin/ai-config", content)).IsSuccessStatusCode;
    }
    public async Task<List<ApiLogItem>> GetApiLogsAsync() => JsonSerializer.Deserialize<LogsResponse>(await _httpClient.GetStringAsync("api/admin/api-logs?limit=100"), JsonOptions)?.Logs ?? new();
    public async Task<List<ApiTopic>> GetCurriculumAsync() => JsonSerializer.Deserialize<TopicsResponse>(await _httpClient.GetStringAsync("api/admin/dau-curriculum"), JsonOptions)?.Topics ?? new();
    public async Task<bool> UpdateCurriculumAsync(List<ApiTopic> topics)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { topics }), Encoding.UTF8, "application/json");
        return (await _httpClient.PutAsync("api/admin/dau-curriculum", content)).IsSuccessStatusCode;
    }
    public async Task<bool> SyncMysqlAsync() => (await _httpClient.PostAsync("api/admin/sync-mysql", new StringContent("{}", Encoding.UTF8, "application/json"))).IsSuccessStatusCode;
}
