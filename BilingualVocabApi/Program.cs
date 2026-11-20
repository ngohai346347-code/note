using System.Text.Json;
using BilingualVocabApi.Models;
using BilingualVocabApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddSingleton<JsonDatabase>();
builder.Services.AddSingleton<AiFallbackService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/", (JsonDatabase db) => Results.Json(new
{
    name = "Bilingual Vocab AI - C# Backend API",
    status = "online",
    version = "1.0.0",
    time = DateTime.UtcNow,
    endpoints = new { health = "/api/health", vocab = "/api/vocab/*", video = "/api/video/*", translate = "/api/translate/*" }
}));

app.MapGet("/api/health", (JsonDatabase db) => Results.Json(new { status = "ok", service = "bilingual-vocab-api-csharp", database = db.StorageMode, time = DateTime.UtcNow }));

app.MapPost("/api/auth/login", (JsonDatabase db, JsonElement body) =>
{
    var email = body.TryGetProperty("email", out var e) ? e.GetString() : null;
    var password = body.TryGetProperty("password", out var p) ? p.GetString() : null;
    if (string.IsNullOrWhiteSpace(email)) return Results.BadRequest(new { error = "Email không được để trống" });
    var storedUser = db.GetUserByEmail(email);
    if (storedUser is null) return Results.Unauthorized();
    if (storedUser.Status == "suspended") return Results.StatusCode(403);
    if (storedUser.Password != password) return Results.Unauthorized();
    db.UpdateUser(storedUser.Id, new Dictionary<string, object?> { ["last_active"] = "Vừa mới" });
    var user = db.GetUser(storedUser.Id);
    return Results.Json(new { success = true, user, token = $"token_{user!.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" });
});
app.MapPost("/api/auth/register", (JsonDatabase db, JsonElement body) =>
{
    var email = body.TryGetProperty("email", out var e) ? e.GetString() : null;
    var name = body.TryGetProperty("name", out var n) ? n.GetString() : null;
    var password = body.TryGetProperty("password", out var p) ? p.GetString() : null;
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
        return Results.BadRequest(new { error = "Vui lòng điền đầy đủ email, họ tên và mật khẩu" });
    if (db.GetUserByEmail(email) is not null) return Results.BadRequest(new { error = "Email này đã được đăng ký" });

    var user = new User
    {
        Id = $"user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        Email = email.Trim().ToLowerInvariant(),
        Password = password,
        Name = name.Trim(),
        Role = "student",
        Student_Id = body.TryGetProperty("student_id", out var sid) ? sid.GetString() : null,
        Department = body.TryGetProperty("department", out var d) ? d.GetString() : "Khoa Kiến trúc",
        Expertise = body.TryGetProperty("expertise", out var ex) ? ex.GetString() : null,
        Target_Cefr = body.TryGetProperty("target_cefr", out var tc) ? tc.GetString() : "B2",
        Status = "active",
        Created_At = DateTime.UtcNow.ToString("O"),
        Last_Active = "Vừa mới"
    };

    var saved = db.AddUser(user);
    return Results.Json(new { success = true, user = saved, token = $"token_{saved.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" }, statusCode: 201);
});

app.MapGet("/api/auth/me", (HttpRequest request, JsonDatabase db) =>
{
    var userId = request.Headers["x-user-id"].ToString();
    var email = request.Headers["x-user-email"].ToString();
    if (!string.IsNullOrWhiteSpace(userId))
    {
        var byId = db.GetUser(userId);
        if (byId is not null) return Results.Json(new { success = true, user = byId });
    }
    if (!string.IsNullOrWhiteSpace(email))
    {
        var byEmail = db.GetUserByEmail(email);
        if (byEmail is not null)
        {
            byEmail.Password = null;
            return Results.Json(new { success = true, user = byEmail });
        }
    }
    return Results.Json(new { success = true, user = db.GetUsers().FirstOrDefault() });
});

app.MapGet("/api/auth/users", (JsonDatabase db) => Results.Json(new { success = true, users = db.GetUsers() }));
app.MapPut("/api/auth/users/{id}", (string id, JsonDatabase db, Dictionary<string, object?> updates) =>
{
    if (id == "user-admin-root" || db.GetUser(id)?.Email.Equals("admin@123", StringComparison.OrdinalIgnoreCase) == true)
        return Results.BadRequest(new { error = "Không thể thay đổi tài khoản quản trị cố định." });
    if (updates.TryGetValue("role", out var roleValue) && roleValue?.ToString() is not ("student" or "expert"))
        return Results.BadRequest(new { error = "Vai trò chỉ có thể là student hoặc expert." });
    var updated = db.UpdateUser(id, updates);
    return updated is null ? Results.NotFound(new { error = "Không tìm thấy người dùng" }) : Results.Json(new { success = true, user = updated });
});
app.MapDelete("/api/auth/users/{id}", (string id, JsonDatabase db) => db.DeleteUser(id) ? Results.Json(new { success = true }) : Results.NotFound(new { error = "Không tìm thấy người dùng" }));

app.MapGet("/api/expert/assignments", (string? expert_id, JsonDatabase db) => Results.Json(new { success = true, assignments = db.GetAssignments(expert_id) }));
app.MapPost("/api/expert/assignments", (JsonDatabase db, JsonElement body) =>
{
    var title = body.TryGetProperty("title", out var t) ? t.GetString() : null;
    var skill = body.TryGetProperty("skill", out var s) ? s.GetString() : null;
    var instructions = body.TryGetProperty("instructions", out var i) ? i.GetString() : null;
    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(skill) || string.IsNullOrWhiteSpace(instructions))
        return Results.BadRequest(new { error = "Vui lòng cung cấp tiêu đề, kỹ năng và hướng dẫn" });

    var a = new Assignment
    {
        Id = $"assign-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        Expert_Id = body.TryGetProperty("expert_id", out var eid) ? eid.GetString() ?? "user-expert-1" : "user-expert-1",
        Expert_Name = body.TryGetProperty("expert_name", out var en) ? en.GetString() ?? "Chuyên gia" : "Chuyên gia",
        Title = title,
        Skill = skill,
        Target_Level = body.TryGetProperty("target_level", out var tl) ? tl.GetString() ?? "B2" : "B2",
        Department_Category = body.TryGetProperty("department_category", out var dc) ? dc.GetString() ?? "DAU" : "DAU",
        Instructions = instructions,
        Reference_Media_Url = body.TryGetProperty("reference_media_url", out var ru) ? ru.GetString() : null,
        Due_Date = body.TryGetProperty("due_date", out var due) ? due.GetString() : DateTime.UtcNow.AddDays(7).ToString("O"),
        Created_At = DateTime.UtcNow.ToString("O"),
        Max_Score = body.TryGetProperty("max_score", out var ms) && ms.TryGetInt32(out var score) ? score : 100
    };

    return Results.Json(new { success = true, assignment = db.AddAssignment(a) }, statusCode: 201);
});
app.MapDelete("/api/expert/assignments/{id}", (string id, JsonDatabase db) => Results.Json(new { success = db.DeleteAssignment(id) }));
app.MapGet("/api/expert/submissions", (string? status, string? assignment_id, JsonDatabase db) => Results.Json(new { success = true, submissions = db.GetSubmissions(null, assignment_id, status) }));
app.MapPost("/api/expert/submissions/{id}/grade", (string id, JsonDatabase db, JsonElement body) =>
{
    var feedback = body.TryGetProperty("feedback", out var f) ? f.GetString() : null;
    var score = body.TryGetProperty("score", out var s) && s.TryGetDouble(out var sv) ? sv : (double?)null;
    if (!score.HasValue || string.IsNullOrWhiteSpace(feedback)) return Results.BadRequest(new { error = "Thiếu điểm hoặc nhận xét" });
    var graded = db.GradeSubmission(id, score.Value, feedback, body.TryGetProperty("graded_by", out var gb) ? gb.GetString() ?? "Chuyên gia" : "Chuyên gia", body.TryGetProperty("assessed_level", out var al) ? al.GetString() : null);
    return graded is null ? Results.NotFound(new { error = "Không tìm thấy bài nộp" }) : Results.Json(new { success = true, submission = graded });
});
app.MapGet("/api/expert/consultations", (string? student_id, JsonDatabase db) => Results.Json(new { success = true, consultations = db.GetConsultations(student_id) }));
app.MapPost("/api/expert/consultations", (JsonDatabase db, JsonElement body) =>
{
    var studentId = body.TryGetProperty("student_id", out var sid) ? sid.GetString() : null;
    var advice = body.TryGetProperty("advice", out var adv) ? adv.GetString() : null;
    var level = body.TryGetProperty("recommended_level", out var rl) ? rl.GetString() : null;
    if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(advice) || string.IsNullOrWhiteSpace(level))
        return Results.BadRequest(new { error = "Thiếu thông tin tư vấn" });

    var c = new Consultation
    {
        Id = $"cons-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        Expert_Id = body.TryGetProperty("expert_id", out var e) ? e.GetString() ?? "user-expert-1" : "user-expert-1",
        Expert_Name = body.TryGetProperty("expert_name", out var en) ? en.GetString() ?? "Chuyên gia" : "Chuyên gia",
        Student_Id = studentId,
        Student_Name = body.TryGetProperty("student_name", out var sn) ? sn.GetString() ?? "Sinh viên" : "Sinh viên",
        Current_Level = body.TryGetProperty("current_level", out var cl) ? cl.GetString() ?? "B1" : "B1",
        Recommended_Level = level,
        Advice = advice,
        Strengths = new List<string> { "Nền tảng tốt" },
        Weaknesses = new List<string> { "Cần luyện thêm" },
        Study_Plan = body.TryGetProperty("study_plan", out var sp) ? sp.GetString() ?? "30 phút mỗi ngày" : "30 phút mỗi ngày"
    };

    return Results.Json(new { success = true, consultation = db.AddConsultation(c) }, statusCode: 201);
});
app.MapGet("/api/expert/students-progress", (JsonDatabase db) => Results.Json(new { success = true, students = db.GetStudentProgress() }));

app.MapGet("/api/student/assignments", (JsonDatabase db) => Results.Json(new { success = true, assignments = db.GetAssignments() }));
app.MapPost("/api/student/assignments/{id}/submit", (string id, JsonDatabase db, JsonElement body) =>
{
    var content = body.TryGetProperty("content", out var c) ? c.GetString() : null;
    var audio = body.TryGetProperty("audio_url", out var au) ? au.GetString() : null;
    if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(audio)) return Results.BadRequest(new { error = "Thiếu nội dung hoặc file audio" });
    var assignment = db.GetAssignment(id);
    var s = new Submission
    {
        Id = $"sub-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        Assignment_Id = id,
        Assignment_Title = assignment?.Title ?? "Bài tập",
        Skill = assignment?.Skill ?? "writing",
        Student_Id = body.TryGetProperty("student_id", out var sid) ? sid.GetString() ?? "user-student-root" : "user-student-root",
        Student_Name = body.TryGetProperty("student_name", out var sn) ? sn.GetString() ?? "Sinh viên" : "Sinh viên",
        Student_Code = body.TryGetProperty("student_code", out var sc) ? sc.GetString() : null,
        Student_Department = body.TryGetProperty("student_department", out var sd) ? sd.GetString() : null,
        Content = content ?? string.Empty,
        Audio_Url = audio,
        Submitted_At = DateTime.UtcNow.ToString("O"),
        Status = "pending"
    };
    return Results.Json(new { success = true, submission = db.AddSubmission(s) }, statusCode: 201);
});
app.MapGet("/api/student/submissions", (string? student_id, JsonDatabase db) => Results.Json(new { success = true, submissions = db.GetSubmissions(student_id) }));
app.MapGet("/api/student/consultations", (string? student_id, JsonDatabase db) => Results.Json(new { success = true, consultations = db.GetConsultations(student_id) }));

app.MapGet("/api/admin/stats", (JsonDatabase db) => Results.Json(new { success = true, stats = db.GetAdminStats() }));
app.MapGet("/api/admin/ai-config", (JsonDatabase db) => Results.Json(new { success = true, config = db.GetAiConfig() }));
app.MapPut("/api/admin/ai-config", (JsonDatabase db, AiConfig cfg) => Results.Json(new { success = true, config = db.UpdateAiConfig(cfg) }));
app.MapGet("/api/admin/api-logs", (int? limit, JsonDatabase db) => Results.Json(new { success = true, logs = db.GetApiLogs(limit ?? 50) }));
app.MapGet("/api/admin/dau-curriculum", (JsonDatabase db) =>
{
    var topics = db.GetTopics().Where(t => t.Is_Dau_Curriculum || t.Name.StartsWith("DAU:", StringComparison.OrdinalIgnoreCase)).ToList();
    return Results.Json(new { success = true, topics });
});
app.MapPut("/api/admin/dau-curriculum", (JsonDatabase db, JsonElement body) =>
{
    if (!body.TryGetProperty("topics", out var topicsElement) || topicsElement.ValueKind != JsonValueKind.Array)
        return Results.BadRequest(new { error = "Danh sách chương trình không hợp lệ." });
    var topics = JsonSerializer.Deserialize<List<Topic>>(topicsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) ?? new();
    if (topics.Any(t => string.IsNullOrWhiteSpace(t.Name))) return Results.BadRequest(new { error = "Tên chủ đề không được để trống." });
    return Results.Json(new { success = true, topics = db.ReplaceDauCurriculum(topics) });
});
app.MapPost("/api/admin/sync-mysql", () => Results.Json(new { success = true, message = "Bản C# đang dùng JSON local, không yêu cầu MySQL sync." }));

app.MapPost("/api/vocab/generate", async (JsonDatabase db, AiFallbackService ai, JsonElement body) =>
{
    var topicName = body.TryGetProperty("topic", out var t) ? t.GetString() : null;
    var ownerId = body.TryGetProperty("user_id", out var uid) ? uid.GetString() : null;
    if (string.IsNullOrWhiteSpace(ownerId) || db.GetUser(ownerId)?.Role != "student") return Results.BadRequest(new { error = "Student account is required." });
    if (string.IsNullOrWhiteSpace(topicName)) return Results.BadRequest(new { error = "Vui lòng cung cấp chủ đề hợp lệ." });
    var level = body.TryGetProperty("level", out var l) ? l.GetString() ?? "ALL" : "ALL";
    level = level.Trim().ToUpperInvariant();
    var allowedLevels = new HashSet<string> { "A1", "A2", "B1", "B2", "C1", "C2" };
    if (!allowedLevels.Contains(level)) return Results.BadRequest(new { error = "Cấp CEFR không hợp lệ. Chọn A1, A2, B1, B2, C1 hoặc C2." });
    var duplicate = db.GetTopicsForUser(ownerId).FirstOrDefault(topic =>
        topic.Name.Equals(topicName.Trim(), StringComparison.OrdinalIgnoreCase));
    if (duplicate is not null) return Results.BadRequest(new { error = "Tên chủ đề đã tồn tại. Hãy chọn tên khác." });
    var topicId = $"topic-{Guid.NewGuid():N}";
    var topic = new Topic { Id = topicId, Owner_User_Id = ownerId, Name = topicName.Trim(), Created_At = DateTime.UtcNow.ToString("O"), Level = level };
    try
    {
        var cards = await ai.GenerateVocabAsync(topic.Name, level, topicId, db.GetAiConfig());
        if (cards.Count == 0) throw new JsonException("Gemini không trả về thẻ từ vựng hợp lệ.");
        topic.Card_Count = cards.Count;
        db.AddTopic(topic);
        db.AddCards(cards);
        return Results.Json(new { success = true, topic, cards });
    }
    catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 503); }
    catch (HttpRequestException ex) { return Results.Json(new { error = $"Gemini API: {ex.Message}" }, statusCode: 502); }
    catch (JsonException ex) { return Results.Json(new { error = $"Không đọc được phản hồi Gemini: {ex.Message}" }, statusCode: 502); }
});

// F2.3: Sinh thêm đợt từ vựng (batch #2, #3, ...) cho topic đã có – chống trùng từ
app.MapPost("/api/vocab/generate-batch", async (JsonDatabase db, AiFallbackService ai, JsonElement body) =>
{
    var topicId = body.TryGetProperty("topic_id", out var tid) ? tid.GetString() : null;
    var ownerId = body.TryGetProperty("user_id", out var uid) ? uid.GetString() : null;
    if (string.IsNullOrWhiteSpace(ownerId) || db.GetUser(ownerId)?.Role != "student")
        return Results.BadRequest(new { error = "Student account is required." });
    if (string.IsNullOrWhiteSpace(topicId))
        return Results.BadRequest(new { error = "topic_id là bắt buộc." });

    var topic = db.GetTopicForUser(topicId, ownerId);
    if (topic is null) return Results.NotFound(new { error = "Không tìm thấy chủ đề hoặc bạn không có quyền." });

    // Lấy danh sách từ đã có để loại trừ
    var existingCards = db.GetCardsByTopic(topicId);
    var excludeWords = existingCards.Select(c => c.Word_En).Where(w => !string.IsNullOrWhiteSpace(w)).ToList();

    // batchIndex = số đợt đã có + 1 (mỗi 15 thẻ = 1 đợt)
    var batchIndex = (existingCards.Count / 15) + 1;
    var startIndex = existingCards.Count;
    var level = topic.Level ?? "B1";

    try
    {
        var newCards = await ai.GenerateVocabAsync(
            topic.Name, level, topicId, db.GetAiConfig(),
            excludeWords, batchIndex, startIndex);

        if (newCards.Count == 0)
            return Results.Json(new { error = "Không có từ mới được sinh ra. Hãy thử lại hoặc chủ đề đã đầy từ vựng." }, statusCode: 422);

        db.AddCards(newCards);
        db.UpdateTopicCardCount(topicId);
        var updatedTopic = db.GetTopic(topicId);
        return Results.Json(new { success = true, topic = updatedTopic, new_cards = newCards, total_cards = existingCards.Count + newCards.Count });
    }
    catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 503); }
    catch (HttpRequestException ex) { return Results.Json(new { error = $"Gemini API: {ex.Message}" }, statusCode: 502); }
    catch (JsonException ex) { return Results.Json(new { error = $"Không đọc được phản hồi Gemini: {ex.Message}" }, statusCode: 502); }
});
app.MapPost("/api/vocab/autofill", (AiFallbackService ai, JsonElement body) =>
{
    var input = body.TryGetProperty("input_word", out var iw) ? iw.GetString() : body.TryGetProperty("inputWord", out var iw2) ? iw2.GetString() : null;
    if (string.IsNullOrWhiteSpace(input)) return Results.BadRequest(new { error = "Vui lòng nhập từ hoặc cụm từ." });
    return Results.Json(new { success = true, data = ai.AutoFill(input) });
});

app.MapGet("/api/vocab/topics", (string? user_id, JsonDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(user_id) || db.GetUser(user_id)?.Role != "student") return Results.Json(new { success = true, topics = new List<Topic>() });
    return Results.Json(new { success = true, topics = db.GetTopicsForUser(user_id) });
});
app.MapPost("/api/vocab/topics", (JsonDatabase db, JsonElement body) =>
{
    var name = body.TryGetProperty("name", out var n) ? n.GetString() : null;
    var ownerId = body.TryGetProperty("user_id", out var uid) ? uid.GetString() : null;
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Topic name is required." });
    if (string.IsNullOrWhiteSpace(ownerId) || db.GetUser(ownerId)?.Role != "student") return Results.BadRequest(new { error = "Student account is required." });
    var topic = new Topic { Id = $"topic-{Guid.NewGuid():N}", Owner_User_Id = ownerId, Name = name.Trim(), Level = body.TryGetProperty("level", out var l) ? l.GetString() ?? "ALL" : "ALL", Created_At = DateTime.UtcNow.ToString("O") };
    db.AddTopic(topic);
    return Results.Json(new { success = true, topic }, statusCode: 201);
});
app.MapGet("/api/vocab/notebook/all", (string? user_id, JsonDatabase db) =>
{
    var topics = string.IsNullOrWhiteSpace(user_id) || db.GetUser(user_id)?.Role != "student" ? new List<Topic>() : db.GetTopicsForUser(user_id);
    var topicIds = topics.Select(topic => topic.Id).ToHashSet();
    return Results.Json(new { success = true, cards = db.GetAllCards().Where(card => topicIds.Contains(card.Topic_Id)).ToList(), topics });
});
app.MapPost("/api/vocab/cards", (JsonDatabase db, JsonElement body) =>
{
    var ownerId = body.TryGetProperty("user_id", out var uid) ? uid.GetString() : null;
    if (string.IsNullOrWhiteSpace(ownerId) || db.GetUser(ownerId)?.Role != "student") return Results.BadRequest(new { error = "Student account is required." });
    var topicId = body.TryGetProperty("topic_id", out var tid) ? tid.GetString() : null;
    var topicName = body.TryGetProperty("topicName", out var tn) ? tn.GetString() : "Sổ tay từ vựng của tôi";
    if (string.IsNullOrWhiteSpace(topicId))
    {
        var existing = db.GetTopicsForUser(ownerId).FirstOrDefault(t => t.Name.Equals(topicName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new Topic { Id = $"topic-{Guid.NewGuid():N}", Owner_User_Id = ownerId, Name = topicName ?? "Sổ tay từ vựng của tôi", Level = "ALL", Created_At = DateTime.UtcNow.ToString("O") };
            db.AddTopic(existing);
        }
        topicId = existing.Id;
    }
    else if (db.GetTopicForUser(topicId, ownerId) is null) return Results.NotFound(new { error = "Topic not found for this account." });

    var card = new VocabCard
    {
        Id = $"card-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        Topic_Id = topicId!,
        Word_En = body.TryGetProperty("word_en", out var we) ? we.GetString() ?? string.Empty : string.Empty,
        Ipa_En = body.TryGetProperty("ipa_en", out var ipa) ? ipa.GetString() : null,
        Word_Zh = body.TryGetProperty("word_zh", out var wz) ? wz.GetString() ?? string.Empty : string.Empty,
        Pinyin = body.TryGetProperty("pinyin", out var py) ? py.GetString() ?? string.Empty : string.Empty,
        Word_Ja = body.TryGetProperty("word_ja", out var wj) ? wj.GetString() : null,
        Reading_Ja = body.TryGetProperty("reading_ja", out var rj) ? rj.GetString() : null,
        Meaning_Vi = body.TryGetProperty("meaning_vi", out var mv) ? mv.GetString() ?? string.Empty : string.Empty,
        Example_En = body.TryGetProperty("example_en", out var ee) ? ee.GetString() ?? string.Empty : string.Empty,
        Example_Zh = body.TryGetProperty("example_zh", out var ez) ? ez.GetString() ?? string.Empty : string.Empty,
        Example_Ja = body.TryGetProperty("example_ja", out var eja) ? eja.GetString() : null,
        Example_Meaning_Vi = body.TryGetProperty("example_meaning_vi", out var emv) ? emv.GetString() ?? string.Empty : string.Empty,
        Difficulty = body.TryGetProperty("difficulty", out var d) ? d.GetString() : "B1",
        Learned = false
    };

    return Results.Json(new { success = true, card = db.AddCard(card), topic = db.GetTopic(topicId!) }, statusCode: 201);
});

app.MapPut("/api/vocab/cards/{cardId}", (string cardId, string? user_id, JsonDatabase db, VocabCard patch) =>
{
    if (string.IsNullOrWhiteSpace(user_id) || !db.IsCardOwnedByUser(cardId, user_id)) return Results.NotFound(new { error = "Card not found for this account." });
    var updated = db.UpdateCard(cardId, patch);
    return updated is null ? Results.NotFound(new { error = "Card not found." }) : Results.Json(new { success = true, card = updated });
});
app.MapDelete("/api/vocab/cards/{cardId}", (string cardId, string? user_id, JsonDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(user_id) || !db.IsCardOwnedByUser(cardId, user_id)) return Results.NotFound(new { error = "Card not found for this account." });
    return db.DeleteCard(cardId) ? Results.Json(new { success = true }) : Results.NotFound(new { error = "Card not found." });
});
app.MapPost("/api/vocab/cards/{cardId}/toggle-learned", (string cardId, string? user_id, JsonDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(user_id) || !db.IsCardOwnedByUser(cardId, user_id)) return Results.NotFound(new { error = "Card not found for this account." });
    var card = db.ToggleCardLearned(cardId);
    return card is null ? Results.NotFound(new { error = "Card not found." }) : Results.Json(new { success = true, card });
});app.MapGet("/api/vocab/{topicId}", (string topicId, string? user_id, JsonDatabase db) =>
{
    var topic = string.IsNullOrWhiteSpace(user_id) ? null : db.GetTopicForUser(topicId, user_id);
    return topic is null ? Results.NotFound(new { error = "Topic not found for this account." }) : Results.Json(new { success = true, topic, cards = db.GetCardsByTopic(topicId) });
});
app.MapDelete("/api/vocab/{topicId}", (string topicId, string? user_id, JsonDatabase db) =>
{
    if (string.IsNullOrWhiteSpace(user_id) || db.GetTopicForUser(topicId, user_id) is null) return Results.NotFound(new { error = "Topic not found for this account." });
    db.DeleteTopic(topicId);
    return Results.Json(new { success = true });
});
app.MapPost("/api/video/extract", (JsonDatabase db, AiFallbackService ai, JsonElement body) =>
{
    var url = body.TryGetProperty("youtube_url", out var y) ? y.GetString() : null;
    if (string.IsNullOrWhiteSpace(url)) return Results.BadRequest(new { error = "Vui lòng cung cấp link video YouTube." });
    var videoId = ai.ExtractYouTubeId(url);
    if (string.IsNullOrWhiteSpace(videoId)) return Results.BadRequest(new { error = "Link YouTube không hợp lệ." });

    var sessionId = $"session-{videoId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    var session = new VideoSession
    {
        Id = sessionId,
        Youtube_Url = url,
        Video_Id = videoId,
        Title = body.TryGetProperty("title", out var t) ? t.GetString() ?? $"YouTube Video ({videoId})" : $"YouTube Video ({videoId})",
        Duration_Sec = 300,
        Language = "en",
        Created_At = DateTime.UtcNow.ToString("O")
    };

    var words = new List<VideoWord>
    {
        new() { Id = $"vw-{sessionId}-1", Session_Id = sessionId, Word = "sustainable", Language = "en", Ipa_En = "/səˈsteɪ.nə.bəl/", Timestamp_Sec = 12, Meaning_Vi = "bền vững", Context_Sentence = "This is a sustainable model.", Difficulty = "B1" },
        new() { Id = $"vw-{sessionId}-2", Session_Id = sessionId, Word = "innovation", Language = "en", Ipa_En = "/ˌɪn.əˈveɪ.ʃən/", Timestamp_Sec = 45, Meaning_Vi = "đổi mới", Context_Sentence = "Innovation drives growth.", Difficulty = "B2" }
    };

    session.Words_Count = words.Count;
    session.Subtitles = new List<SubtitleItem>
    {
        new() { Id = "sub-1", Start = 10, Duration = 5, Text_Original = "This is a sustainable model.", Text_Vi = "Đây là một mô hình bền vững." },
        new() { Id = "sub-2", Start = 42, Duration = 5, Text_Original = "Innovation drives growth.", Text_Vi = "Đổi mới thúc đẩy tăng trưởng." }
    };

    db.AddVideoSession(session);
    db.AddVideoWords(words);

    return Results.Json(new { success = true, session, words, transcript_count = session.Subtitles.Count });
});

app.MapGet("/api/video/sessions", (JsonDatabase db) => Results.Json(new { success = true, sessions = db.GetVideoSessions() }));
app.MapGet("/api/video/{sessionId}", (string sessionId, JsonDatabase db) =>
{
    var session = db.GetVideoSession(sessionId);
    if (session is null) return Results.NotFound(new { error = "Không tìm thấy phiên học video." });
    return Results.Json(new { success = true, session, words = db.GetVideoWords(session.Id) });
});
app.MapPut("/api/video/sessions/{sessionId}/subtitles", (string sessionId, JsonDatabase db, JsonElement body) =>
{
    if (!body.TryGetProperty("subtitles", out var subsEl) || subsEl.ValueKind != JsonValueKind.Array) return Results.BadRequest(new { error = "Danh sách phụ đề không hợp lệ." });
    var subtitles = JsonSerializer.Deserialize<List<SubtitleItem>>(subsEl.GetRawText(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) ?? new List<SubtitleItem>();
    var updated = db.UpdateVideoSubtitles(sessionId, subtitles);
    return updated is null ? Results.NotFound(new { error = "Không tìm thấy video session." }) : Results.Json(new { success = true, session = updated });
});

app.MapPost("/api/translate/lookup", (AiFallbackService ai, JsonElement body) =>
{
    var text = body.TryGetProperty("text", out var t) ? t.GetString() : null;
    var sourceLang = body.TryGetProperty("sourceLang", out var sl) ? sl.GetString() ?? "auto" : "auto";
    if (string.IsNullOrWhiteSpace(text)) return Results.BadRequest(new { error = "Text required" });
    return Results.Json(new { success = true, data = ai.Lookup(text, sourceLang) });
});

app.Run();
