using System.Collections.ObjectModel;
using System.Windows.Input;
using EnglishFlashcard3D.Models;
using EnglishFlashcard3D.Services;

namespace EnglishFlashcard3D.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ApiClientService _apiClient;
    private readonly DataStorageService _storageService;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _registerName = string.Empty;
    private bool _isRegistering;
    private int _activeTabIndex;
    private string _statusMessage = "Sẵn sàng";
    private bool _isLoggedIn;
    private TopicDeck? _selectedTopic;
    private string _newTopicName = string.Empty;
    private string _selectedCefr = "B1";
    private AuthUser? _currentUser;
    private ApiAssignment? _selectedAssignment;
    private ApiSubmission? _selectedSubmission;
    private AuthUser? _selectedUser;
    private ApiTopic? _selectedCurriculum;
    private string _assignmentTitle = "";
    private string _assignmentInstructions = "";
    private string _assignmentSkill = "writing";
    private string _assignmentDueDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
    private string _submissionContent = "";
    private string _gradeScore = "";
    private string _gradeFeedback = "";
    private string _gradeLevel = "B1";
    private string _curriculumName = "";
    private string _curriculumLevel = "B1";
    private string _adminSummary = "Chưa tải dữ liệu quản trị";
    private ApiAiConfig _aiConfig = new();

    public MainViewModel()
    {
        _apiClient = new ApiClientService();
        _storageService = new DataStorageService();

        var localCards = _storageService.LoadDefaultCards();
        FlashcardSession = new Flashcard3DViewModel(localCards);
        FlashcardSession.CardMarkedMastered += OnCardMarkedMastered;

        Topics = new ObservableCollection<TopicDeck>();
        Assignments = new ObservableCollection<ApiAssignment>(); Submissions = new ObservableCollection<ApiSubmission>();
        Users = new ObservableCollection<AuthUser>(); ApiLogs = new ObservableCollection<ApiLogItem>(); Curriculum = new ObservableCollection<ApiTopic>();
        CefrLevels = new ObservableCollection<string> { "A1", "A2", "B1", "B2", "C1", "C2" };
        Skills = new ObservableCollection<string> { "writing", "speaking", "listening", "reading" };
        Roles = new ObservableCollection<string> { "student", "expert" };
        UserStatuses = new ObservableCollection<string> { "active", "suspended" };

        LoginCommand = new RelayCommand(_ => _ = LoginAsync());
        LogoutCommand = new RelayCommand(_ => { IsLoggedIn = false; CurrentUser = null; Email = string.Empty; Password = string.Empty; StatusMessage = "Bạn đã đăng xuất."; });
        ToggleRegisterCommand = new RelayCommand(_ => { IsRegistering = !IsRegistering; StatusMessage = IsRegistering ? "Tạo tài khoản mới (role mặc định: student)." : "Đăng nhập bằng tài khoản của bạn."; });
        LoadTopicsCommand = new RelayCommand(_ => _ = LoadTopicsAsync(), _ => IsStudent);
        GenerateTopicCommand = new RelayCommand(_ => _ = GenerateTopicAsync(), _ => IsStudent && !string.IsNullOrWhiteSpace(NewTopicName));
        OpenTopicCommand = new RelayCommand(_ => _ = OpenSelectedTopicAsync(), _ => IsStudent && SelectedTopic is not null);
        GenerateBatchCommand = new RelayCommand(_ => _ = GenerateBatchAsync(), _ => IsStudent && SelectedTopic is not null);
        DeleteTopicCommand = new RelayCommand(_ => _ = DeleteTopicAsync(), _ => IsStudent && SelectedTopic is not null);
        RefreshF4Command = new RelayCommand(_ => _ = LoadF4Async(), _ => IsLoggedIn);
        CreateAssignmentCommand = new RelayCommand(_ => _ = CreateAssignmentAsync(), _ => IsRole("expert") && !string.IsNullOrWhiteSpace(AssignmentTitle) && !string.IsNullOrWhiteSpace(AssignmentInstructions));
        DeleteAssignmentCommand = new RelayCommand(_ => _ = DeleteAssignmentAsync(), _ => IsRole("expert") && SelectedAssignment is not null);
        SubmitWorkCommand = new RelayCommand(_ => _ = SubmitWorkAsync(), _ => IsRole("student") && SelectedAssignment is not null && !string.IsNullOrWhiteSpace(SubmissionContent));
        GradeWorkCommand = new RelayCommand(_ => _ = GradeWorkAsync(), _ => IsRole("expert") && SelectedSubmission is not null);
        RefreshAdminCommand = new RelayCommand(_ => _ = LoadAdminAsync(), _ => IsRole("admin"));
        SyncMysqlCommand = new RelayCommand(_ => _ = SyncMysqlAsync(), _ => IsRole("admin"));
        SaveUserCommand = new RelayCommand(_ => _ = SaveUserAsync(), _ => IsRole("admin") && SelectedUser is not null);
        SaveAiConfigCommand = new RelayCommand(_ => _ = SaveAiConfigAsync(), _ => IsRole("admin"));
        AddCurriculumCommand = new RelayCommand(_ => _ = AddCurriculumAsync(), _ => IsRole("admin") && !string.IsNullOrWhiteSpace(CurriculumName));
        DeleteCurriculumCommand = new RelayCommand(_ => _ = DeleteCurriculumAsync(), _ => IsRole("admin") && SelectedCurriculum is not null);
    }

    public Flashcard3DViewModel FlashcardSession { get; }

    public ObservableCollection<TopicDeck> Topics { get; }
    public ObservableCollection<ApiAssignment> Assignments { get; }
    public ObservableCollection<ApiSubmission> Submissions { get; }
    public ObservableCollection<AuthUser> Users { get; }
    public ObservableCollection<ApiLogItem> ApiLogs { get; }
    public ObservableCollection<ApiTopic> Curriculum { get; }
    public ObservableCollection<string> Skills { get; }
    public ObservableCollection<string> Roles { get; }
    public ObservableCollection<string> UserStatuses { get; }
    public ApiStats AdminStats { get; private set; } = new();
    public ApiAiConfig AiConfig { get => _aiConfig; set => SetField(ref _aiConfig, value); }
    public AuthUser? CurrentUser { get => _currentUser; set { if (SetField(ref _currentUser, value)) { RaiseCommandStates(); OnPropertyChanged(nameof(IsStudent)); OnPropertyChanged(nameof(IsExpert)); OnPropertyChanged(nameof(IsAdmin)); OnPropertyChanged(nameof(IsAssessmentUser)); } } }
    public string RegisterName { get => _registerName; set => SetField(ref _registerName, value); }
    public bool IsRegistering { get => _isRegistering; set => SetField(ref _isRegistering, value); }
    public int ActiveTabIndex { get => _activeTabIndex; set => SetField(ref _activeTabIndex, value); }
    public bool IsLoggedOut => !IsLoggedIn;
    public bool IsStudent => IsRole("student");
    public bool IsExpert => IsRole("expert");
    public bool IsAdmin => IsRole("admin");
    public bool IsAssessmentUser => IsStudent || IsExpert;
    public ApiAssignment? SelectedAssignment { get => _selectedAssignment; set { if (SetField(ref _selectedAssignment, value)) RaiseCommandStates(); } }
    public ApiSubmission? SelectedSubmission { get => _selectedSubmission; set { if (SetField(ref _selectedSubmission, value)) RaiseCommandStates(); } }
    public AuthUser? SelectedUser { get => _selectedUser; set { if (SetField(ref _selectedUser, value)) RaiseCommandStates(); } }
    public ApiTopic? SelectedCurriculum { get => _selectedCurriculum; set { if (SetField(ref _selectedCurriculum, value) && DeleteCurriculumCommand is RelayCommand cmd) cmd.RaiseCanExecuteChanged(); } }
    public string AdminSummary { get => _adminSummary; set => SetField(ref _adminSummary, value); }
    public string AssignmentTitle { get => _assignmentTitle; set { if (SetField(ref _assignmentTitle, value)) RaiseCommandStates(); } }
    public string AssignmentInstructions { get => _assignmentInstructions; set { if (SetField(ref _assignmentInstructions, value)) RaiseCommandStates(); } }
    public string AssignmentSkill { get => _assignmentSkill; set => SetField(ref _assignmentSkill, value); }
    public string AssignmentDueDate { get => _assignmentDueDate; set => SetField(ref _assignmentDueDate, value); }
    public string SubmissionContent { get => _submissionContent; set { if (SetField(ref _submissionContent, value)) RaiseCommandStates(); } }
    public string GradeScore { get => _gradeScore; set => SetField(ref _gradeScore, value); }
    public string GradeFeedback { get => _gradeFeedback; set => SetField(ref _gradeFeedback, value); }
    public string GradeLevel { get => _gradeLevel; set => SetField(ref _gradeLevel, value); }
    public string CurriculumName { get => _curriculumName; set { if (SetField(ref _curriculumName, value)) RaiseCommandStates(); } }
    public string CurriculumLevel { get => _curriculumLevel; set => SetField(ref _curriculumLevel, value); }

    public ObservableCollection<string> CefrLevels { get; }

    public string Email
    {
        get => _email;
        set => SetField(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set
        {
            if (!SetField(ref _isLoggedIn, value)) return;
            OnPropertyChanged(nameof(IsLoggedOut));
            OnPropertyChanged(nameof(IsStudent)); OnPropertyChanged(nameof(IsExpert)); OnPropertyChanged(nameof(IsAdmin)); OnPropertyChanged(nameof(IsAssessmentUser));
            RaiseCommandStates();
        }
    }

    public TopicDeck? SelectedTopic
    {
        get => _selectedTopic;
        set
        {
            if (!SetField(ref _selectedTopic, value)) return;
            RaiseCommandStates();
        }
    }

    public string NewTopicName
    {
        get => _newTopicName;
        set
        {
            if (!SetField(ref _newTopicName, value)) return;
            RaiseCommandStates();
        }
    }

    public string SelectedCefr
    {
        get => _selectedCefr;
        set => SetField(ref _selectedCefr, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ToggleRegisterCommand { get; }
    public ICommand LoadTopicsCommand { get; }
    public ICommand GenerateTopicCommand { get; }
    public ICommand OpenTopicCommand { get; }
    public ICommand GenerateBatchCommand { get; }
    public ICommand DeleteTopicCommand { get; }
    public ICommand RefreshF4Command { get; }
    public ICommand CreateAssignmentCommand { get; }
    public ICommand DeleteAssignmentCommand { get; }
    public ICommand SubmitWorkCommand { get; }
    public ICommand GradeWorkCommand { get; }
    public ICommand RefreshAdminCommand { get; }
    public ICommand SyncMysqlCommand { get; }
    public ICommand SaveUserCommand { get; }
    public ICommand SaveAiConfigCommand { get; }
    public ICommand AddCurriculumCommand { get; }
    public ICommand DeleteCurriculumCommand { get; }

    private async Task LoginAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Vui lòng nhập email và mật khẩu.";
                return;
            }
            if (IsRegistering && string.IsNullOrWhiteSpace(RegisterName))
            {
                StatusMessage = "Vui lòng nhập họ tên để đăng ký.";
                return;
            }

            StatusMessage = IsRegistering ? "Đang tạo tài khoản..." : "Đang đăng nhập...";
            var result = IsRegistering
                ? await _apiClient.RegisterAsync(RegisterName.Trim(), Email.Trim(), Password)
                : await _apiClient.LoginAsync(Email.Trim(), Password);
            if (result?.Success != true || result.User is null)
            {
                IsLoggedIn = false;
                StatusMessage = IsRegistering ? "Đăng ký thất bại. Email có thể đã được sử dụng." : "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.";
                return;
            }

            CurrentUser = result.User;
            IsLoggedIn = true;
            ActiveTabIndex = IsAdmin ? 2 : IsExpert ? 1 : 0;
            IsRegistering = false;
            RegisterName = string.Empty;
            StatusMessage = $"Xin chào {result.User.Name}";
            if (IsStudent) await LoadTopicsAsync();
            if (IsAssessmentUser) await LoadF4Async();
            if (IsAdmin) await LoadAdminAsync();
        }
        catch
        {
            IsLoggedIn = false;
            StatusMessage = "Không kết nối được API. Hãy chạy BilingualVocabApi trước.";
        }
    }
    private async Task LoadTopicsAsync()
    {
        try
        {
            if (CurrentUser is null) return;
            var items = await _apiClient.GetTopicsAsync(CurrentUser.Id);
            Topics.Clear();
            foreach (var item in items)
            {
                Topics.Add(item);
            }

            StatusMessage = $"Đã tải {Topics.Count} chủ đề";
        }
        catch
        {
            StatusMessage = "Tải danh sách chủ đề thất bại.";
        }
    }

    private async Task GenerateTopicAsync()
    {
        try
        {
            StatusMessage = "Đang gọi Gemini tạo bộ từ vựng...";
            if (CurrentUser is null) return;
            var topic = await _apiClient.GenerateTopicAsync(NewTopicName.Trim(), SelectedCefr, CurrentUser.Id);
            if (topic is null)
            {
                StatusMessage = "Gemini không trả về chủ đề.";
                return;
            }

            Topics.Insert(0, topic);
            SelectedTopic = topic;
            NewTopicName = string.Empty;
            StatusMessage = "Đã tạo bộ từ vựng bằng Gemini.";
            await OpenSelectedTopicAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể tạo từ vựng: {ex.Message}";
        }
    }
    private async Task OpenSelectedTopicAsync()
    {
        if (SelectedTopic is null) return;

        try
        {
            if (CurrentUser is null) return;
            var cards = await _apiClient.GetTopicCardsAsync(SelectedTopic.Id, CurrentUser.Id);
            FlashcardSession.SetCards(cards);
            StatusMessage = $"Đã mở chủ đề: {SelectedTopic.Name} ({cards.Count} thẻ)";
        }
        catch
        {
            StatusMessage = "Không tải được thẻ của chủ đề.";
        }
    }

    private async void OnCardMarkedMastered(EnglishFlashcard card)
    {
        if (string.IsNullOrWhiteSpace(card.Id) || !IsLoggedIn) return;
        await _apiClient.MarkCardLearnedAsync(card.Id, CurrentUser!.Id);
    }

    // F2.3: Thêm đợt từ vựng mới vào chủ đề đã có
    private async Task GenerateBatchAsync()
    {
        if (SelectedTopic is null || CurrentUser is null) return;
        try
        {
            StatusMessage = $"Đang tạo thêm 15 từ vựng cho chủ đề «{SelectedTopic.Name}»...";
            var result = await _apiClient.GenerateBatchAsync(SelectedTopic.Id, CurrentUser.Id);
            if (result?.Success != true)
            {
                StatusMessage = "Không sinh được từ mới. Thử lại hoặc chủ đề đã đầy từ.";
                return;
            }
            SelectedTopic.CardCount = result.TotalCards;
            OnPropertyChanged(nameof(SelectedTopic));
            await OpenSelectedTopicAsync();
            StatusMessage = $"Đã thêm {result.NewCards.Count} từ mới. Tổng: {result.TotalCards} thẻ.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể thêm từ: {ex.Message}";
        }
    }

    // F2.5: Xóa chủ đề và toàn bộ flashcard liên quan
    private async Task DeleteTopicAsync()
    {
        if (SelectedTopic is null || CurrentUser is null) return;
        var topicToDelete = SelectedTopic;
        try
        {
            StatusMessage = $"Đang xóa chủ đề «{topicToDelete.Name}»...";
            var ok = await _apiClient.DeleteTopicAsync(topicToDelete.Id, CurrentUser.Id);
            if (!ok) { StatusMessage = "Xóa chủ đề thất bại."; return; }
            Topics.Remove(topicToDelete);
            SelectedTopic = Topics.FirstOrDefault();
            if (SelectedTopic is not null)
                await OpenSelectedTopicAsync();
            else
                FlashcardSession.SetCards(new System.Collections.Generic.List<EnglishFlashcard>());
            StatusMessage = $"Đã xóa chủ đề «{topicToDelete.Name}».";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi xóa chủ đề: {ex.Message}";
        }
    }

    private void RaiseCommandStates()
    {
        if (LoadTopicsCommand is RelayCommand load) load.RaiseCanExecuteChanged();
        if (GenerateTopicCommand is RelayCommand gen) gen.RaiseCanExecuteChanged();
        if (OpenTopicCommand is RelayCommand open) open.RaiseCanExecuteChanged();
        if (GenerateBatchCommand is RelayCommand batch) batch.RaiseCanExecuteChanged();
        if (DeleteTopicCommand is RelayCommand delTopic) delTopic.RaiseCanExecuteChanged();
        foreach (var command in new[] { RefreshF4Command, CreateAssignmentCommand, DeleteAssignmentCommand, SubmitWorkCommand, GradeWorkCommand, RefreshAdminCommand, SyncMysqlCommand, SaveUserCommand, SaveAiConfigCommand, AddCurriculumCommand, DeleteCurriculumCommand })
            if (command is RelayCommand relay) relay.RaiseCanExecuteChanged();
    }

    private bool IsRole(string role) => IsLoggedIn && CurrentUser?.Role.Equals(role, StringComparison.OrdinalIgnoreCase) == true;

    private async Task LoadF4Async()
    {
        try
        {
            Assignments.Clear(); Submissions.Clear();
            var assignments = IsRole("expert") ? await _apiClient.GetExpertAssignmentsAsync(CurrentUser!.Id) : await _apiClient.GetStudentAssignmentsAsync();
            foreach (var item in assignments) Assignments.Add(item);
            var submissions = IsRole("expert") ? await _apiClient.GetExpertSubmissionsAsync() : await _apiClient.GetStudentSubmissionsAsync(CurrentUser!.Id);
            foreach (var item in submissions) Submissions.Add(item);
        }
        catch { StatusMessage = "Không tải được dữ liệu khảo thí."; }
    }

    private async Task CreateAssignmentAsync()
    {
        if (CurrentUser is null) return;
        try
        {
            var ok = await _apiClient.CreateAssignmentAsync(CurrentUser.Id, CurrentUser.Name, AssignmentTitle.Trim(), AssignmentSkill, SelectedCefr, AssignmentInstructions.Trim(), AssignmentDueDate);
            if (!ok) { StatusMessage = "Tạo bài tập thất bại."; return; }
            AssignmentTitle = AssignmentInstructions = ""; await LoadF4Async(); StatusMessage = "Đã tạo bài tập.";
        }
        catch { StatusMessage = "Không kết nối được API để tạo bài."; }
    }

    private async Task DeleteAssignmentAsync()
    {
        if (SelectedAssignment is null) return;
        try { if (await _apiClient.DeleteAssignmentAsync(SelectedAssignment.Id)) { SelectedAssignment = null; await LoadF4Async(); StatusMessage = "Đã xóa bài tập."; } }
        catch { StatusMessage = "Xóa bài tập thất bại."; }
    }

    private async Task SubmitWorkAsync()
    {
        if (CurrentUser is null || SelectedAssignment is null) return;
        try { if (await _apiClient.SubmitAssignmentAsync(SelectedAssignment.Id, CurrentUser.Id, CurrentUser.Name, SubmissionContent.Trim())) { SubmissionContent = ""; await LoadF4Async(); StatusMessage = "Đã nộp bài."; } else StatusMessage = "Nộp bài thất bại."; }
        catch { StatusMessage = "Không kết nối được API để nộp bài."; }
    }

    private async Task GradeWorkAsync()
    {
        if (SelectedSubmission is null || !double.TryParse(GradeScore, out var score) || score < 0 || score > 100 || string.IsNullOrWhiteSpace(GradeFeedback)) { StatusMessage = "Nhập điểm từ 0–100 và nhận xét trước khi chấm."; return; }
        try { if (await _apiClient.GradeSubmissionAsync(SelectedSubmission.Id, score, GradeFeedback.Trim(), CurrentUser?.Name ?? "Chuyên gia", GradeLevel)) { GradeScore = GradeFeedback = ""; await LoadF4Async(); StatusMessage = "Đã lưu kết quả chấm."; } else StatusMessage = "Chấm bài thất bại."; }
        catch { StatusMessage = "Không kết nối được API để chấm bài."; }
    }

    private async Task LoadAdminAsync()
    {
        try
        {
            AdminStats = await _apiClient.GetAdminStatsAsync(); OnPropertyChanged(nameof(AdminStats));
            AdminSummary = $"Sinh viên: {AdminStats.Total_Students} | Chuyên gia: {AdminStats.Total_Experts} | Chủ đề: {AdminStats.Total_Topics} | Thẻ: {AdminStats.Total_Cards} | Bài tập: {AdminStats.Total_Assignments} | Bài nộp: {AdminStats.Total_Submissions} | API calls: {AdminStats.Total_Api_Calls}";
            Users.Clear(); foreach (var u in await _apiClient.GetUsersAsync()) Users.Add(u);
            ApiLogs.Clear(); foreach (var log in await _apiClient.GetApiLogsAsync()) ApiLogs.Add(log);
            Curriculum.Clear(); foreach (var topic in await _apiClient.GetCurriculumAsync()) Curriculum.Add(topic);
            AiConfig = await _apiClient.GetAiConfigAsync();
        }
        catch { StatusMessage = "Không tải được dữ liệu quản trị."; }
    }

    private async Task SaveUserAsync()
    {
        if (SelectedUser is null) return;
        try { if (await _apiClient.UpdateUserAsync(SelectedUser.Id, SelectedUser.Role, SelectedUser.Status)) { await LoadAdminAsync(); StatusMessage = "Đã cập nhật người dùng."; } }
        catch { StatusMessage = "Cập nhật người dùng thất bại."; }
    }

    private async Task SaveAiConfigAsync()
    {
        try { StatusMessage = await _apiClient.UpdateAiConfigAsync(AiConfig) ? "Đã lưu cấu hình AI." : "Lưu cấu hình AI thất bại."; }
        catch { StatusMessage = "Không kết nối được API để lưu cấu hình."; }
    }

    private async Task SyncMysqlAsync()
    {
        try { await _apiClient.SyncMysqlAsync(); StatusMessage = "Backend C# hiện lưu JSON cục bộ; thao tác sync MySQL chưa được cấu hình."; }
        catch { StatusMessage = "Không thể gọi chức năng đồng bộ."; }
    }

    private async Task AddCurriculumAsync()
    {
        Curriculum.Add(new ApiTopic { Id = $"topic-dau-{Guid.NewGuid():N}", Name = CurriculumName.Trim(), Level = CurriculumLevel });
        await SaveCurriculumAsync(); CurriculumName = "";
    }
    private async Task DeleteCurriculumAsync()
    {
        if (SelectedCurriculum is not null) Curriculum.Remove(SelectedCurriculum);
        await SaveCurriculumAsync();
    }
    private async Task SaveCurriculumAsync()
    {
        try { StatusMessage = await _apiClient.UpdateCurriculumAsync(Curriculum.ToList()) ? "Đã lưu chương trình DAU." : "Lưu chương trình thất bại."; }
        catch { StatusMessage = "Không thể cập nhật chương trình DAU."; }
    }
}
