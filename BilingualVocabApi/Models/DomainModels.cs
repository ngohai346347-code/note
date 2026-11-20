namespace BilingualVocabApi.Models;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "student";
    public string? Student_Id { get; set; }
    public string? Department { get; set; }
    public string? Expertise { get; set; }
    public string? Target_Cefr { get; set; }
    public string Created_At { get; set; } = DateTime.UtcNow.ToString("O");
    public string Status { get; set; } = "active";
    public string? Last_Active { get; set; }
}

public class Topic
{
    public string Id { get; set; } = string.Empty;
    public string? Owner_User_Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Created_At { get; set; } = DateTime.UtcNow.ToString("O");
    public int Card_Count { get; set; }
    public string? Level { get; set; }
    public bool Is_Dau_Curriculum { get; set; }
    public string? Department { get; set; }
}

public class VocabCard
{
    public string Id { get; set; } = string.Empty;
    public string Topic_Id { get; set; } = string.Empty;
    public string Word_En { get; set; } = string.Empty;
    public string? Ipa_En { get; set; }
    public string Word_Zh { get; set; } = string.Empty;
    public string Pinyin { get; set; } = string.Empty;
    public string? Word_Ja { get; set; }
    public string? Reading_Ja { get; set; }
    public string Meaning_Vi { get; set; } = string.Empty;
    public string Example_En { get; set; } = string.Empty;
    public string Example_Zh { get; set; } = string.Empty;
    public string? Example_Ja { get; set; }
    public string Example_Meaning_Vi { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public bool Learned { get; set; }
}

public class Assignment
{
    public string Id { get; set; } = string.Empty;
    public string Expert_Id { get; set; } = string.Empty;
    public string Expert_Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Skill { get; set; } = "writing";
    public string Target_Level { get; set; } = "B2";
    public string Department_Category { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string? Reference_Media_Url { get; set; }
    public string? Due_Date { get; set; }
    public string Created_At { get; set; } = DateTime.UtcNow.ToString("O");
    public int Max_Score { get; set; } = 100;
    public int Submission_Count { get; set; }
}

public class Submission
{
    public string Id { get; set; } = string.Empty;
    public string Assignment_Id { get; set; } = string.Empty;
    public string Assignment_Title { get; set; } = string.Empty;
    public string Skill { get; set; } = "writing";
    public string Student_Id { get; set; } = string.Empty;
    public string Student_Name { get; set; } = string.Empty;
    public string? Student_Code { get; set; }
    public string? Student_Department { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Audio_Url { get; set; }
    public string Submitted_At { get; set; } = DateTime.UtcNow.ToString("O");
    public string Status { get; set; } = "pending";
    public double? Score { get; set; }
    public string? Feedback { get; set; }
    public string? Graded_By { get; set; }
    public string? Graded_At { get; set; }
    public string? Assessed_Level { get; set; }
}

public class Consultation
{
    public string Id { get; set; } = string.Empty;
    public string Expert_Id { get; set; } = string.Empty;
    public string Expert_Name { get; set; } = string.Empty;
    public string Student_Id { get; set; } = string.Empty;
    public string Student_Name { get; set; } = string.Empty;
    public string Current_Level { get; set; } = "B1";
    public string Recommended_Level { get; set; } = "B2";
    public string Advice { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public string Study_Plan { get; set; } = string.Empty;
    public string Created_At { get; set; } = DateTime.UtcNow.ToString("O");
}

public class SubtitleItem
{
    public string Id { get; set; } = string.Empty;
    public double Start { get; set; }
    public double Duration { get; set; } = 5;
    public string Text_Original { get; set; } = string.Empty;
    public string Text_Vi { get; set; } = string.Empty;
    public string? Pinyin { get; set; }
}

public class VideoWord
{
    public string Id { get; set; } = string.Empty;
    public string Session_Id { get; set; } = string.Empty;
    public string Word { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string? Ipa_En { get; set; }
    public string? Pinyin { get; set; }
    public double Timestamp_Sec { get; set; }
    public string Meaning_Vi { get; set; } = string.Empty;
    public string? Context_Sentence { get; set; }
    public string? Difficulty { get; set; }
}

public class VideoSession
{
    public string Id { get; set; } = string.Empty;
    public string Youtube_Url { get; set; } = string.Empty;
    public string Video_Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Channel_Title { get; set; }
    public int Duration_Sec { get; set; }
    public string Language { get; set; } = "en";
    public string Created_At { get; set; } = DateTime.UtcNow.ToString("O");
    public int Words_Count { get; set; }
    public List<SubtitleItem> Subtitles { get; set; } = new();
}

public class AiConfig
{
    public string Model_Name { get; set; } = "gemini-3.8-flash";
    public double Temperature { get; set; } = 0.4;
    public int Max_Output_Tokens { get; set; } = 4096;
    public bool Anti_Repetition { get; set; } = true;
    public string System_Instruction { get; set; } = "You are an expert bilingual lexicographer.";
}

public class ApiLog
{
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Duration_Ms { get; set; }
    public string? User_Email { get; set; }
    public int Tokens_Used { get; set; }
}
