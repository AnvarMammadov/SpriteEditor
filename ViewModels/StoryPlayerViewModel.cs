using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpriteEditor.Data.Story;

namespace SpriteEditor.ViewModels
{
    public partial class StoryPlayerViewModel : ObservableObject
    {
        private StoryGraph _currentStory;
        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private string _currentAudioPath;

        [ObservableProperty] private string _displayText;
        [ObservableProperty] private string _speakerName;
        [ObservableProperty] private ImageSource _backgroundImage;
        [ObservableProperty] private ImageSource _characterImage;

        private readonly DispatcherTimer _textTimer;
        private string _targetText;
        private int _charIndex;

        public ObservableCollection<StoryChoice> CurrentChoices { get; } = new ObservableCollection<StoryChoice>();

        public StoryPlayerViewModel()
        {
            _mediaPlayer.MediaEnded += (s, e) => { _mediaPlayer.Position = TimeSpan.Zero; _mediaPlayer.Play(); };
            _textTimer = new DispatcherTimer();
            _textTimer.Interval = TimeSpan.FromMilliseconds(30);
            _textTimer.Tick += OnTypewriterTick;
        }

        public void LoadStory(StoryGraph story)
        {
            _currentStory = story;
            if (!string.IsNullOrEmpty(story.StartNodeId)) GoToNode(story.StartNodeId);
        }

        private async void GoToNode(string nodeId)
        {
            var node = _currentStory.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) return;

            // === YENİ HİSSƏ BAŞLADI ===
            // Düyündəki bütün əmrləri icra et
            if (node.Commands != null)
            {
                foreach (var cmd in node.Commands)
                {
                    await ExecuteCommandAsync(cmd);
                }
            }
            PlayNodeAudio(node.AudioPath);

            // 2. Node Tipinə görə davranış
            if (node.Type == StoryNodeType.Dialogue || node.Type == StoryNodeType.Start)
            {
                // Mətni yazmağa başla
                StartTypewriter(node.Text);
                SpeakerName = node.SpeakerName;

                // Şəkilləri yüklə (Mövcud kodunuz)
                if (!string.IsNullOrEmpty(node.BackgroundImagePath)) /* ... Yükləmə kodu ... */;
                if (!string.IsNullOrEmpty(node.CharacterImagePath)) /* ... Yükləmə kodu ... */;

                RefreshChoices(node);
            }
            else if (node.Type == StoryNodeType.Event || node.Type == StoryNodeType.Condition)
            {
                // Event/Condition: Ekranda dayanma, avtomatik keç (Logic Node)
                RefreshChoices(node);

                // Əgər seçim varsa, birincisini seç (avtomatik)
                if (CurrentChoices.Count > 0)
                {
                    // Rekursiyanı qırmaq üçün kiçik delay verilə bilər, amma hələlik birbaşa keçirik
                    SelectChoice(CurrentChoices[0]);
                }
            }
        }

        // YENİ: Əmrləri tanıyan "Beyin"
        private async Task ExecuteCommandAsync(StoryCommand command)
        {
            switch (command)
            {
                case WaitCommand waitCmd:
                    // Oyunçunu dondurub gözləyirik
                    await Task.Delay(TimeSpan.FromSeconds(waitCmd.DurationSeconds));
                    break;

                case SetVariableCommand varCmd:
                    // Dəyişən məntiqi (Köhnə ExecuteActions kodunuzu bura köçürün)
                    ApplyVariableChange(varCmd);
                    break;

                case PlaySoundCommand soundCmd:
                    // Səs effektini oynat (Musiqini kəsmədən)
                    PlaySoundEffect(soundCmd.AudioPath);
                    break;
            }
        }


        // Dəyişən məntiqini (Köhnə ExecuteActions) bura ayırın
        private void ApplyVariableChange(SetVariableCommand action)
        {
            // Dəyişəni tapırıq
            var variable = _currentStory.Variables.FirstOrDefault(v => v.Name == action.TargetVariableName);
            if (variable == null) return;

            try
            {
                switch (action.Operation)
                {
                    case ActionOperation.Set:
                        variable.Value = action.Value;
                        break;

                    case ActionOperation.Toggle:
                        if (bool.TryParse(variable.Value, out bool b))
                            variable.Value = (!b).ToString();
                        break;

                    case ActionOperation.Add:
                        if (int.TryParse(variable.Value, out int iA) && int.TryParse(action.Value, out int vA))
                            variable.Value = (iA + vA).ToString();
                        break;

                    case ActionOperation.Subtract:
                        if (int.TryParse(variable.Value, out int iS) && int.TryParse(action.Value, out int vS))
                            variable.Value = (iS - vS).ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Action Error: {ex.Message}");
            }
        }

        // Səs effekti üçün sadə metod (MediaPlayer əlavə edilməlidir)
        private void PlaySoundEffect(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            // Yeni bir MediaPlayer yaradıb səsi oyna (Fire and forget)
            var sfxPlayer = new MediaPlayer();
            sfxPlayer.Open(new Uri(path));
            sfxPlayer.Play();
        }

        private void RefreshChoices(StoryNode node)
        {
            CurrentChoices.Clear();
            foreach (var choice in node.Choices)
            {
                if (EvaluateCondition(choice))
                {
                    CurrentChoices.Add(choice);
                }
            }
        }


        private bool EvaluateCondition(StoryChoice choice)
        {
            if (string.IsNullOrEmpty(choice.ConditionVariableName) || choice.Operator == ConditionOperator.None) return true;
            var variable = _currentStory.Variables.FirstOrDefault(v => v.Name == choice.ConditionVariableName);
            if (variable == null) return false;

            string varVal = variable.Value?.ToLower() ?? "";
            string targetVal = choice.ConditionValue?.ToLower() ?? "";

            switch (choice.Operator)
            {
                case ConditionOperator.Equals: return varVal == targetVal;
                case ConditionOperator.NotEquals: return varVal != targetVal;
                case ConditionOperator.GreaterThan: return double.TryParse(varVal, out double vG) && double.TryParse(targetVal, out double tG) && vG > tG;
                case ConditionOperator.LessThan: return double.TryParse(varVal, out double vL) && double.TryParse(targetVal, out double tL) && vL < tL;
                default: return true;
            }
        }

        [RelayCommand]
        public void SelectChoice(StoryChoice choice)
        {
            if (choice != null && !string.IsNullOrEmpty(choice.TargetNodeId)) GoToNode(choice.TargetNodeId);
        }

        private void PlayNodeAudio(string newPath)
        {
            if (string.IsNullOrEmpty(newPath)) return;
            if (_currentAudioPath == newPath) return;
            try { _mediaPlayer.Open(new Uri(newPath)); _mediaPlayer.Play(); _currentAudioPath = newPath; }
            catch { }
        }

        private void OnTypewriterTick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_targetText)) { _textTimer.Stop(); return; }
            if (_charIndex < _targetText.Length) { DisplayText += _targetText[_charIndex]; _charIndex++; }
            else _textTimer.Stop();
        }

        [RelayCommand]
        public void CompleteText()
        {
            if (_textTimer.IsEnabled) { _textTimer.Stop(); DisplayText = _targetText; _charIndex = _targetText.Length; }
        }

        private void StartTypewriter(string text)
        {
            _targetText = text ?? ""; DisplayText = ""; _charIndex = 0; _textTimer.Stop();
            if (!string.IsNullOrEmpty(_targetText)) _textTimer.Start();
        }

        public void Cleanup() { _mediaPlayer.Stop(); _mediaPlayer.Close(); }
    }
}