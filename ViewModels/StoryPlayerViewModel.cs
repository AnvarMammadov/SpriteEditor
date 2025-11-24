using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        [ObservableProperty] private string _displayText;
        [ObservableProperty] private string _speakerName;
        [ObservableProperty] private ImageSource _backgroundImage;
        [ObservableProperty] private ImageSource _characterImage;

        public ObservableCollection<StoryChoice> CurrentChoices { get; } = new ObservableCollection<StoryChoice>();

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

            // 2. Mətni və Şəkilləri yenilə
            DisplayText = node.Text;
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
    }
}
