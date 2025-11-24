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
        private string _currentAudioPath; // Hazırda nə oxunur?

        [ObservableProperty] private string _displayText;
        [ObservableProperty] private string _speakerName;
        [ObservableProperty] private ImageSource _backgroundImage;
        [ObservableProperty] private ImageSource _characterImage;

        // === YENİ: Typewriter Effekti üçün ===
        private readonly DispatcherTimer _textTimer;
        private string _targetText; // Hədəf mətn (tam cümlə)
        private int _charIndex;     // Hazırda neçənci hərfdəyik?
                                    // ====================================

        public ObservableCollection<StoryChoice> CurrentChoices { get; } = new ObservableCollection<StoryChoice>();


        public StoryPlayerViewModel()
        {
            // Audio Loop (Köhnə kod)
            _mediaPlayer.MediaEnded += (s, e) =>
            {
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            };

            // === YENİ: Timer Tənzimləmələri ===
            _textTimer = new DispatcherTimer();
            _textTimer.Interval = TimeSpan.FromMilliseconds(30); // Sürət (30ms idealdır)
            _textTimer.Tick += OnTypewriterTick;
        }

        public void LoadStory(StoryGraph story)
        {
            _currentStory = story;
            if (!string.IsNullOrEmpty(story.StartNodeId))
            {
                GoToNode(story.StartNodeId);
            }
        }

        private void GoToNode(string nodeId)
        {
            var node = _currentStory.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) return;

            // 1. YENİ: Düyünə girən kimi Hadisələri İcra Et (Actions)
            // Bu, mətni göstərməzdən əvvəl baş verməlidir ki, şərtlər dərhal düzgün işləsin.
            ExecuteActions(node);

            PlayNodeAudio(node.AudioPath);

            // 2. Mətni və Şəkilləri yenilə
            StartTypewriter(node.Text);
            SpeakerName = node.SpeakerName;

            if (!string.IsNullOrEmpty(node.BackgroundImagePath))
                try { BackgroundImage = new BitmapImage(new Uri(node.BackgroundImagePath)); } catch { BackgroundImage = null; }
            else BackgroundImage = null;

            if (!string.IsNullOrEmpty(node.CharacterImagePath))
                try { CharacterImage = new BitmapImage(new Uri(node.CharacterImagePath)); } catch { CharacterImage = null; }
            else CharacterImage = null;

            // 3. Seçimləri yenilə (Şərtləri yoxlayaraq)
            CurrentChoices.Clear();
            foreach (var choice in node.Choices)
            {
                if (EvaluateCondition(choice))
                {
                    CurrentChoices.Add(choice);
                }
            }
        }

        // === YENİ: Hadisələri İcra Edən Metod ===
        private void ExecuteActions(StoryNode node)
        {
            if (node.OnEnterActions == null) return;

            foreach (var action in node.OnEnterActions)
            {
                // Hədəf dəyişəni tap
                var variable = _currentStory.Variables.FirstOrDefault(v => v.Name == action.TargetVariableName);
                if (variable == null) continue; // Dəyişən tapılmadısa ötür

                try
                {
                    switch (action.Operation)
                    {
                        case ActionOperation.Set:
                            // Dəyəri birbaşa mənimsət
                            variable.Value = action.Value;
                            break;

                        case ActionOperation.Toggle:
                            // Yalnız Boolean üçün: True -> False, False -> True
                            if (bool.TryParse(variable.Value, out bool currentBool))
                            {
                                variable.Value = (!currentBool).ToString();
                            }
                            break;

                        case ActionOperation.Add:
                            // Rəqəmlər üçün toplama (Integer)
                            if (int.TryParse(variable.Value, out int currentIntAdd) && int.TryParse(action.Value, out int valAdd))
                            {
                                variable.Value = (currentIntAdd + valAdd).ToString();
                            }
                            break;

                        case ActionOperation.Subtract:
                            // Rəqəmlər üçün çıxma (Integer)
                            if (int.TryParse(variable.Value, out int currentIntSub) && int.TryParse(action.Value, out int valSub))
                            {
                                variable.Value = (currentIntSub - valSub).ToString();
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Səhv olsa belə oyun dayanmasın, sadəcə log yaza bilərik
                    System.Diagnostics.Debug.WriteLine($"Action Error: {ex.Message}");
                }
            }
        }

        // === Şərtləri Yoxlayan Metod (Olduğu kimi qalır) ===
        private bool EvaluateCondition(StoryChoice choice)
        {
            if (string.IsNullOrEmpty(choice.ConditionVariableName) || choice.Operator == ConditionOperator.None)
                return true;

            var variable = _currentStory.Variables.FirstOrDefault(v => v.Name == choice.ConditionVariableName);
            if (variable == null) return false;

            string varValue = variable.Value?.ToString().ToLower() ?? "";
            string targetValue = choice.ConditionValue?.ToString().ToLower() ?? "";

            switch (choice.Operator)
            {
                case ConditionOperator.Equals: return varValue == targetValue;
                case ConditionOperator.NotEquals: return varValue != targetValue;
                case ConditionOperator.GreaterThan:
                    if (double.TryParse(varValue, out double vG) && double.TryParse(targetValue, out double tG)) return vG > tG;
                    return false;
                case ConditionOperator.LessThan:
                    if (double.TryParse(varValue, out double vL) && double.TryParse(targetValue, out double tL)) return vL < tL;
                    return false;
                default: return true;
            }
        }

        [RelayCommand]
        public void SelectChoice(StoryChoice choice)
        {
            if (choice != null && !string.IsNullOrEmpty(choice.TargetNodeId))
            {
                GoToNode(choice.TargetNodeId);
            }
        }

        // === YENİ: Audio Məntiqi ===
        private void PlayNodeAudio(string newPath)
        {
            // Əgər Node-da musiqi yoxdursa, heç nə etmə (və ya köhnəni davam etdir)
            // Strategiya: "Boşdursa susdur" yoxsa "Boşdursa köhnəni saxla"?
            // Visual Novel-lərdə adətən köhnə musiqi davam edir. 
            // Əgər susdurmaq istəsəniz, xüsusi bir "Stop" əmri və ya boş fayl təyin edə bilərsiniz.

            if (string.IsNullOrEmpty(newPath)) return;

            // Əgər eyni musiqi artıq çalınırsa, dəyişmə (Kəsilmə olmasın)
            if (_currentAudioPath == newPath) return;

            try
            {
                _mediaPlayer.Open(new Uri(newPath));
                _mediaPlayer.Play();
                _currentAudioPath = newPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio Error: {ex.Message}");
            }
        }


        private void OnTypewriterTick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_targetText))
            {
                _textTimer.Stop();
                return;
            }

            // Əgər hələ yazılacaq hərf qalıbsa
            if (_charIndex < _targetText.Length)
            {
                // Bir hərf əlavə et
                DisplayText += _targetText[_charIndex];
                _charIndex++;
            }
            else
            {
                // Bitdisə dayandır
                _textTimer.Stop();
            }
        }

        // Mətni dərhal tamamlamaq üçün (Məsələn, oyunçu klikləyəndə)
        [RelayCommand]
        public void CompleteText()
        {
            if (_textTimer.IsEnabled)
            {
                _textTimer.Stop();
                DisplayText = _targetText; // Hepsini göstər
                _charIndex = _targetText.Length;
            }
        }

        private void StartTypewriter(string text)
        {
            // 1. Hazırlıq
            _targetText = text ?? "";
            DisplayText = ""; // Ekrani təmizlə
            _charIndex = 0;
            _textTimer.Stop(); // Köhnə timer işləyirsə dayandır

            // 2. Başla
            if (!string.IsNullOrEmpty(_targetText))
            {
                _textTimer.Start();
            }
        }


        public void Cleanup()
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Close();
        }
    }
}
