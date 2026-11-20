using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BilingualVocabApi.Models;

namespace BilingualVocabApi.Services;

public class AiFallbackService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly IConfiguration _configuration;

    public AiFallbackService(IConfiguration configuration) => _configuration = configuration;

    public async Task<List<VocabCard>> GenerateVocabAsync(
        string topic,
        string level,
        string topicId,
        AiConfig config,
        IEnumerable<string>? excludeWords = null,
        int batchIndex = 1,
        int startIndex = 0,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Nếu không có API Key, sử dụng bộ dữ liệu mẫu 15 từ chuẩn hóa để hệ thống luôn hoạt động
            return GenerateVocab(topic, level, topicId, excludeWords, batchIndex, startIndex);
        }

        var model = string.IsNullOrWhiteSpace(config.Model_Name) ? "gemini-3.8-flash" : config.Model_Name.Trim();
        var excludeList = excludeWords?.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        var excludePrompt = excludeList.Count > 0
            ? $" Tuyệt đối KHÔNG sinh lại các từ sau (đã có trong bộ thẻ): [{string.Join(", ", excludeList.Take(80))}]."
            : string.Empty;
        var batchPrompt = batchIndex > 1
            ? $" Đây là đợt từ vựng #{batchIndex}, hãy cung cấp 15 từ vựng mới, phong phú và nâng cao hơn về chủ đề này."
            : string.Empty;

        var prompt = $"Tạo đúng 15 flashcard từ vựng tiếng Anh về chủ đề '{topic}', trình độ CEFR {level}.{batchPrompt}{excludePrompt} " +
            "Chỉ trả JSON array, không markdown hay code block. Mỗi object có đúng các trường sau: " +
            "word_en, ipa_en, meaning_vi, example_en, example_meaning_vi, difficulty. " +
            "Nghĩa tiếng Việt và câu ví dụ tiếng Anh phải tự nhiên, chính xác, phù hợp trình độ. Tuyệt đối không lặp lại từ.";

        var requestBody = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = Math.Clamp(config.Temperature, 0, 2),
                maxOutputTokens = Math.Clamp(config.Max_Output_Tokens, 1024, 8192)
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = TryReadApiError(responseText) ?? $"Gemini trả về HTTP {(int)response.StatusCode}.";
            throw new HttpRequestException(detail, null, response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseText);
        var generatedText = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(generatedText)) throw new JsonException("Gemini trả về nội dung rỗng.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var generated = JsonSerializer.Deserialize<List<GeneratedCard>>(generatedText, options);
        if (generated is null || generated.Count == 0) throw new JsonException("Không đọc được danh sách từ vựng Gemini trả về.");

        return generated.Select((card, index) => new VocabCard
        {
            Id = $"card-{topicId}-{startIndex + index + 1}",
            Topic_Id = topicId,
            Word_En = card.Word_En?.Trim() ?? string.Empty,
            Ipa_En = card.Ipa_En,
            Meaning_Vi = card.Meaning_Vi?.Trim() ?? string.Empty,
            Example_En = card.Example_En?.Trim() ?? string.Empty,
            Example_Meaning_Vi = card.Example_Meaning_Vi?.Trim() ?? string.Empty,
            Difficulty = string.IsNullOrWhiteSpace(card.Difficulty) ? level : card.Difficulty,
            Learned = false
        }).Where(card => !string.IsNullOrWhiteSpace(card.Word_En) && !string.IsNullOrWhiteSpace(card.Meaning_Vi)).ToList();
    }

    private static string? TryReadApiError(string body)
    {
        try { using var doc = JsonDocument.Parse(body); return doc.RootElement.GetProperty("error").GetProperty("message").GetString(); }
        catch { return null; }
    }

    private sealed class GeneratedCard
    {
        [JsonPropertyName("word_en")]
        public string? Word_En { get; set; }
        [JsonPropertyName("ipa_en")]
        public string? Ipa_En { get; set; }
        [JsonPropertyName("meaning_vi")]
        public string? Meaning_Vi { get; set; }
        [JsonPropertyName("example_en")]
        public string? Example_En { get; set; }
        [JsonPropertyName("example_meaning_vi")]
        public string? Example_Meaning_Vi { get; set; }
        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }
    }

    // Fallback: 15 từ chuẩn khi không có GEMINI_API_KEY
    private static readonly (string Word, string Ipa, string MeaningVi, string ExampleEn, string ExampleVi, string Difficulty)[] FallbackPool = new[]
    {
        ("resilience",    "/rɪˈzɪl.jəns/",        "Sự kiên cường",  "She showed great resilience after the setback.",       "Cô ấy thể hiện sự kiên cường sau thất bại.",        "B2"),
        ("meticulous",    "/məˈtɪk.jə.ləs/",       "Tỉ mỉ, cẩn thận","He is meticulous about every detail of his work.",     "Anh ấy rất tỉ mỉ trong từng chi tiết công việc.",  "C1"),
        ("enhance",       "/ɪnˈhɑːns/",            "Nâng cao, tăng cường","This tool enhances productivity significantly.",  "Công cụ này nâng cao năng suất đáng kể.",          "B2"),
        ("sustainable",   "/səˈsteɪ.nə.bəl/",      "Bền vững",       "We need sustainable development for the future.",     "Chúng ta cần phát triển bền vững cho tương lai.",  "B2"),
        ("innovative",    "/ˈɪn.ə.veɪ.tɪv/",       "Đổi mới, sáng tạo","Their innovative approach solved the problem.",     "Cách tiếp cận đổi mới của họ đã giải quyết vấn đề.","B2"),
        ("collaborate",   "/kəˈlæb.ə.reɪt/",       "Hợp tác",        "Teams must collaborate to succeed.",                  "Các nhóm phải hợp tác để thành công.",              "B2"),
        ("comprehensive", "/ˌkɒm.prɪˈhen.sɪv/",    "Toàn diện, đầy đủ","She gave a comprehensive report on the findings.",  "Cô ấy đưa ra báo cáo toàn diện về các phát hiện.", "C1"),
        ("facilitate",    "/fəˈsɪl.ɪ.teɪt/",       "Tạo điều kiện, giúp đỡ","Good tools facilitate learning.",             "Công cụ tốt tạo điều kiện cho việc học.",           "C1"),
        ("accumulate",    "/əˈkjuː.mjʊ.leɪt/",     "Tích lũy",       "Students accumulate knowledge over time.",            "Sinh viên tích lũy kiến thức theo thời gian.",      "B2"),
        ("prioritize",    "/praɪˈɒr.ɪ.taɪz/",      "Ưu tiên",        "Learn to prioritize your daily tasks.",               "Học cách ưu tiên các nhiệm vụ hàng ngày của bạn.", "B2"),
        ("elaborate",     "/ɪˈlæb.ər.ət/",          "Tỉ mỉ; giải thích thêm","Could you elaborate on that point?",           "Bạn có thể giải thích thêm về điểm đó không?",    "C1"),
        ("substantial",   "/səbˈstæn.ʃəl/",         "Đáng kể, lớn",   "The project requires substantial effort.",            "Dự án đòi hỏi nỗ lực đáng kể.",                    "C1"),
        ("interpret",     "/ɪnˈtɜː.prɪt/",          "Giải thích, phiên dịch","She interpreted the data carefully.",          "Cô ấy giải thích dữ liệu một cách cẩn thận.",      "B2"),
        ("evaluate",      "/ɪˈvæl.juˌeɪt/",         "Đánh giá",       "Teachers evaluate students regularly.",               "Giáo viên đánh giá học sinh thường xuyên.",         "B2"),
        ("consequence",   "/ˈkɒn.sɪ.kwəns/",        "Hậu quả, kết quả","Think about the consequences of your actions.",     "Hãy nghĩ về hậu quả của hành động của bạn.",       "B2"),
        ("phenomenon",    "/fɪˈnɒm.ɪ.nən/",         "Hiện tượng",     "This is a remarkable natural phenomenon.",            "Đây là một hiện tượng tự nhiên đáng chú ý.",       "C1"),
        ("perception",    "/pəˈsep.ʃən/",            "Nhận thức, cảm nhận","Perception of reality differs for everyone.",    "Nhận thức về thực tại khác nhau với mỗi người.",  "C1"),
        ("fluctuate",     "/ˈflʌk.tʃuˌeɪt/",        "Dao động, thay đổi","Prices fluctuate depending on demand.",            "Giá cả dao động tùy theo nhu cầu.",                 "C1"),
        ("paradigm",      "/ˈpær.ə.daɪm/",           "Mô hình, khuôn mẫu","This discovery shifted the paradigm.",            "Khám phá này đã thay đổi mô hình tư duy.",         "C2"),
        ("discourse",     "/ˈdɪs.kɔːs/",             "Diễn ngôn, thảo luận","Academic discourse shapes our thinking.",       "Diễn ngôn học thuật định hình tư duy của chúng ta.","C2"),
        ("ambiguous",     "/æmˈbɪɡ.juˌəs/",          "Mơ hồ, không rõ ràng","The instructions were ambiguous.",             "Các hướng dẫn không rõ ràng.",                     "C1"),
        ("plausible",     "/ˈplɔː.zɪ.bəl/",           "Có vẻ hợp lý",   "His explanation sounded plausible.",                "Giải thích của anh ta có vẻ hợp lý.",              "C1"),
        ("undermine",     "/ˌʌn.dəˈmaɪn/",            "Phá hoại, làm suy yếu","Stress can undermine your health.",           "Căng thẳng có thể làm suy yếu sức khỏe của bạn.", "C1"),
        ("profound",      "/prəˈfaʊnd/",              "Sâu sắc",        "He had a profound understanding of the subject.",    "Anh ấy có sự hiểu biết sâu sắc về chủ đề.",       "C1"),
        ("intrinsic",     "/ɪnˈtrɪn.zɪk/",            "Vốn có, bản chất","Intrinsic motivation leads to deeper learning.",   "Động lực bản thân dẫn đến việc học sâu hơn.",     "C2"),
        ("alleviate",     "/əˈliː.viˌeɪt/",           "Giảm bớt",       "Rest can alleviate fatigue.",                        "Nghỉ ngơi có thể giảm bớt mệt mỏi.",              "C1"),
        ("empirical",     "/ɪmˈpɪr.ɪ.kəl/",           "Thực nghiệm",    "The theory is based on empirical evidence.",         "Lý thuyết dựa trên bằng chứng thực nghiệm.",      "C2"),
        ("hierarchy",     "/ˈhaɪ.ər.ɑː.ki/",          "Hệ thống phân cấp","Every company has its own hierarchy.",             "Mỗi công ty đều có hệ thống phân cấp riêng.",     "C1"),
        ("indigenous",    "/ɪnˈdɪdʒ.ɪ.nəs/",          "Bản địa",        "We must protect indigenous cultures.",               "Chúng ta phải bảo vệ các nền văn hóa bản địa.",   "C1"),
        ("rhetoric",      "/ˈret.ər.ɪk/",              "Tu từ, hùng biện","Political rhetoric can be misleading.",             "Tu từ chính trị có thể gây hiểu lầm.",             "C2"),
    };

    public List<VocabCard> GenerateVocab(
        string topic,
        string level,
        string topicId,
        IEnumerable<string>? excludeWords = null,
        int batchIndex = 1,
        int startIndex = 0)
    {
        var exclude = excludeWords?
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim().ToLowerInvariant())
            .ToHashSet() ?? new HashSet<string>();

        var available = FallbackPool
            .Where(w => !exclude.Contains(w.Word.ToLowerInvariant()))
            .ToList();

        // Nếu hết pool, quay vòng từ đầu pool (tránh trả rỗng)
        if (available.Count == 0) available = FallbackPool.ToList();

        // Lấy 15 từ, lệch offset theo batchIndex để tránh trùng đợt
        var offset = ((batchIndex - 1) * 15) % available.Count;
        var selected = available.Skip(offset).Concat(available.Take(offset)).Take(15).ToList();

        return selected.Select((w, i) => new VocabCard
        {
            Id = $"card-{topicId}-{startIndex + i + 1}",
            Topic_Id = topicId,
            Word_En = w.Word,
            Ipa_En = w.Ipa,
            Meaning_Vi = w.MeaningVi,
            Example_En = w.ExampleEn,
            Example_Meaning_Vi = w.ExampleVi,
            Difficulty = string.IsNullOrWhiteSpace(level) ? w.Difficulty : level,
            Learned = false
        }).ToList();
    }

    public VocabCard AutoFill(string input)
    {
        return new VocabCard
        {
            Word_En = input,
            Ipa_En = "/.../",
            Word_Zh = input,
            Pinyin = string.Empty,
            Meaning_Vi = $"Nghĩa tiếng Việt của {input}",
            Example_En = $"I use the word {input} in a sentence.",
            Example_Zh = $"我在句子中使用{input}这个词。",
            Example_Meaning_Vi = $"Tôi dùng từ {input} trong một câu.",
            Difficulty = "B1"
        };
    }

    public object Lookup(string text, string sourceLang)
    {
        return new
        {
            word_en = text,
            word_zh = text,
            pinyin = sourceLang == "zh" ? "" : "",
            meaning_vi = $"Giải nghĩa tham khảo cho '{text}'",
            example_sentence = $"This is an example sentence with {text}."
        };
    }

    public string? ExtractYouTubeId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (Regex.IsMatch(input, "^[a-zA-Z0-9_-]{11}$")) return input;
        var match = Regex.Match(input, "(?:youtube\\.com\\/(?:.*[?&]v=|shorts\\/)|youtu\\.be\\/)([a-zA-Z0-9_-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }
}
