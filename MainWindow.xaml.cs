using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ApexLauncherSystem.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApexLauncherSystem;

public partial class MainWindow : Window
{
    private readonly string _basePath = AppContext.BaseDirectory;
    private readonly JsonSerializerSettings _jsonSettings = new() { Formatting = Formatting.Indented };

    private List<GameModel> _games = new();
    private List<CategoryModel> _categories = new();
    private LauncherSettings _settings = new();
    private bool _isLocked;

    private string UiPath => Path.Combine(_basePath, "UI", "index.html");
    private string AdminPath => Path.Combine(_basePath, "UI", "admin.html");
    private string GamesPath => Path.Combine(_basePath, "Data", "games.json");
    private string CategoriesPath => Path.Combine(_basePath, "Data", "categories.json");
    private string SettingsPath => Path.Combine(_basePath, "Data", "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            EnsureDirectories();
            LoadData();

            await LauncherWebView.EnsureCoreWebView2Async();
            LauncherWebView.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;
            LauncherWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            _isLocked = _settings.PinEnabled;
            NavigateToUi(UiPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup error: {ex.Message}", "Apex Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_basePath, "UI"));
        Directory.CreateDirectory(Path.Combine(_basePath, "Data"));
        Directory.CreateDirectory(Path.Combine(_basePath, "Cache", "images"));
        Directory.CreateDirectory(Path.Combine(_basePath, "Cache", "backgrounds"));
    }

    private void LoadData()
    {
        _games = ReadJsonFile<List<GameModel>>(GamesPath) ?? new List<GameModel>();
        _categories = ReadJsonFile<List<CategoryModel>>(CategoriesPath) ?? new List<CategoryModel>();
        _settings = ReadJsonFile<LauncherSettings>(SettingsPath) ?? new LauncherSettings();
    }

    private T? ReadJsonFile<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
        catch
        {
            return default;
        }
    }

    private void NavigateToUi(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException($"UI page missing: {targetPath}");
        }

        LauncherWebView.Source = new Uri(targetPath);
    }

    private void CoreWebView2OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            JObject payload = JObject.Parse(e.WebMessageAsJson);
            string type = payload["type"]?.Value<string>() ?? string.Empty;
            JObject data = payload["data"] as JObject ?? new JObject();

            switch (type)
            {
                case "init":
                    PostJson(new
                    {
                        type = "state",
                        data = new
                        {
                            games = _games,
                            categories = _categories,
                            settings = _settings,
                            locked = _isLocked
                        }
                    });
                    break;
                case "play":
                    LaunchGame(data["id"]?.Value<string>());
                    break;
                case "unlockPin":
                    HandleUnlock(data["pin"]?.Value<string>());
                    break;
                case "setPin":
                    HandleSetPin(data["pin"]?.Value<string>());
                    break;
                case "openAdmin":
                    NavigateToUi(AdminPath);
                    break;
                case "openHome":
                    NavigateToUi(UiPath);
                    break;
                case "adminLogin":
                    HandleAdminLogin(data["password"]?.Value<string>());
                    break;
                case "createGame":
                    CreateOrUpdateGame(data, isCreate: true);
                    break;
                case "updateGame":
                    CreateOrUpdateGame(data, isCreate: false);
                    break;
                case "deleteGame":
                    DeleteGame(data["id"]?.Value<string>());
                    break;
                case "createCategory":
                    CreateCategory(data);
                    break;
                case "deleteCategory":
                    DeleteCategory(data["id"]?.Value<string>());
                    break;
                case "chooseExe":
                    ChooseExePath();
                    break;
                case "chooseImage":
                    ChooseImagePath();
                    break;
                case "setTheme":
                    SetTheme(data["theme"]?.Value<string>());
                    break;
            }
        }
        catch (Exception ex)
        {
            PostToast($"Request failed: {ex.Message}", "error");
        }
    }

    private void LaunchGame(string? gameId)
    {
        if (_isLocked)
        {
            PostToast("System locked. Enter PIN first.", "error");
            return;
        }

        GameModel? game = _games.FirstOrDefault(x => x.Id == gameId);
        if (game == null)
        {
            PostToast("Game not found.", "error");
            return;
        }

        if (string.IsNullOrWhiteSpace(game.ExePath) || !File.Exists(game.ExePath))
        {
            PostToast("Executable path is invalid.", "error");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = game.ExePath,
                WorkingDirectory = Directory.Exists(game.WorkingDirectory) ? game.WorkingDirectory : Path.GetDirectoryName(game.ExePath) ?? _basePath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            game.LaunchCount += 1;
            game.LastPlayed = DateTime.UtcNow;
            SaveGames();
            PostToast($"Deploying {game.Name}...", "success");
            PushState();
        }
        catch (Exception ex)
        {
            PostToast($"Launch failed: {ex.Message}", "error");
        }
    }

    private void HandleUnlock(string? pin)
    {
        if (!_settings.PinEnabled)
        {
            _isLocked = false;
            PushState();
            return;
        }

        if (SecurityHelper.VerifySecret(pin ?? string.Empty, _settings.PinHash, _settings.PinSalt))
        {
            _isLocked = false;
            PostToast("Unlocked successfully.", "success");
            PushState();
            return;
        }

        PostToast("Invalid PIN.", "error");
    }

    private void HandleSetPin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
        {
            PostToast("PIN must be at least 4 digits.", "error");
            return;
        }

        var (hash, salt) = SecurityHelper.HashSecret(pin);
        _settings.PinEnabled = true;
        _settings.PinHash = hash;
        _settings.PinSalt = salt;
        _isLocked = true;
        SaveSettings();
        PushState();
        PostToast("PIN configured.", "success");
    }

    private void HandleAdminLogin(string? password)
    {
        if (SecurityHelper.VerifySecret(password ?? string.Empty, _settings.AdminHash, _settings.AdminSalt))
        {
            PostJson(new { type = "adminLoginResult", data = new { success = true } });
            PostToast("Admin login successful.", "success");
        }
        else
        {
            PostJson(new { type = "adminLoginResult", data = new { success = false } });
            PostToast("Invalid admin credentials.", "error");
        }
    }

    private void CreateOrUpdateGame(JObject data, bool isCreate)
    {
        string name = data["name"]?.Value<string>()?.Trim() ?? string.Empty;
        string exePath = data["exePath"]?.Value<string>()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            PostToast("Game name is required.", "error");
            return;
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            PostToast("EXE path is required.", "error");
            return;
        }

        string fullExePath = Path.GetFullPath(exePath);
        if (!fullExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            PostToast("Path must point to an EXE file.", "error");
            return;
        }

        GameModel? game = null;
        if (!isCreate)
        {
            string editId = data["id"]?.Value<string>() ?? string.Empty;
            game = _games.FirstOrDefault(x => x.Id == editId);
        }

        game ??= new GameModel();
        game.Name = name;
        game.Description = data["description"]?.Value<string>() ?? string.Empty;
        game.ExePath = fullExePath;
        game.WorkingDirectory = data["workingDirectory"]?.Value<string>() ?? Path.GetDirectoryName(fullExePath) ?? _basePath;
        game.ImagePath = data["imagePath"]?.Value<string>() ?? string.Empty;
        game.CategoryId = data["categoryId"]?.Value<string>() ?? string.Empty;

        if (isCreate)
        {
            _games.Add(game);
        }

        SaveGames();
        PushState();
        PostToast(isCreate ? "Game added." : "Game updated.", "success");
    }

    private void DeleteGame(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _games.RemoveAll(x => x.Id == id);
        SaveGames();
        PushState();
        PostToast("Game deleted.", "info");
    }

    private void CreateCategory(JObject data)
    {
        string name = data["name"]?.Value<string>()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            PostToast("Category name is required.", "error");
            return;
        }

        if (_categories.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            PostToast("Category already exists.", "error");
            return;
        }

        _categories.Add(new CategoryModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Description = data["description"]?.Value<string>() ?? string.Empty,
            ColorHex = data["colorHex"]?.Value<string>() ?? "#ff3333"
        });

        SaveCategories();
        PushState();
        PostToast("Category added.", "success");
    }

    private void DeleteCategory(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _categories.RemoveAll(x => x.Id == id);
        foreach (var game in _games.Where(g => g.CategoryId == id))
        {
            game.CategoryId = string.Empty;
        }

        SaveCategories();
        SaveGames();
        PushState();
        PostToast("Category deleted and games reassigned.", "info");
    }

    private void ChooseExePath()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            PostJson(new { type = "fileSelected", data = new { target = "exePath", value = dialog.FileName } });
        }
    }

    private void ChooseImagePath()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            PostJson(new { type = "fileSelected", data = new { target = "imagePath", value = dialog.FileName } });
        }
    }

    private void SetTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return;
        }

        _settings.Theme = theme;
        SaveSettings();
        PushState();
    }

    private void SaveGames() => File.WriteAllText(GamesPath, JsonConvert.SerializeObject(_games, _jsonSettings));

    private void SaveCategories() => File.WriteAllText(CategoriesPath, JsonConvert.SerializeObject(_categories, _jsonSettings));

    private void SaveSettings() => File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(_settings, _jsonSettings));

    private void PushState()
    {
        PostJson(new
        {
            type = "state",
            data = new
            {
                games = _games,
                categories = _categories,
                settings = _settings,
                locked = _isLocked
            }
        });
    }

    private void PostToast(string message, string level)
    {
        PostJson(new { type = "toast", data = new { message, level } });
    }

    private void PostJson(object payload)
    {
        LauncherWebView.CoreWebView2?.PostWebMessageAsJson(JsonConvert.SerializeObject(payload));
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isLocked)
        {
            e.Cancel = true;
            PostToast("Unlock PIN before exiting.", "error");
            return;
        }

        base.OnClosing(e);
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_isLocked && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            PostToast("Alt+F4 disabled while locked.", "error");
        }
    }

    private sealed class LauncherSettings
    {
        [JsonProperty("theme")]
        public string Theme { get; set; } = "dark";

        [JsonProperty("backgroundType")]
        public string BackgroundType { get; set; } = "image";

        [JsonProperty("backgroundPath")]
        public string BackgroundPath { get; set; } = string.Empty;

        [JsonProperty("pinEnabled")]
        public bool PinEnabled { get; set; } = true;

        [JsonProperty("pinHash")]
        public string PinHash { get; set; } = string.Empty;

        [JsonProperty("pinSalt")]
        public string PinSalt { get; set; } = string.Empty;

        [JsonProperty("adminHash")]
        public string AdminHash { get; set; } = string.Empty;

        [JsonProperty("adminSalt")]
        public string AdminSalt { get; set; } = string.Empty;
    }
}
