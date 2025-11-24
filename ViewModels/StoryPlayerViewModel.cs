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

        // Ekranda görünən elementlər
        [ObservableProperty] private string _displayText;
        [ObservableProperty] private string _speakerName;
        [ObservableProperty] private ImageSource _backgroundImage;
        [ObservableProperty] private ImageSource _characterImage;

        // Seçimlər (Düymələr)
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
            if (node == null) return; // Oyun bitdi və ya xəta

            // 1. Mətni yenilə
            DisplayText = node.Text;
            SpeakerName = node.SpeakerName;

            // 2. Şəkilləri yenilə (Əgər yol varsa)
            if (!string.IsNullOrEmpty(node.BackgroundImagePath))
            {
                try { BackgroundImage = new BitmapImage(new Uri(node.BackgroundImagePath)); } catch { }
            }
            else
            {
                BackgroundImage = null; // Yoxdursa təmizlə
            }

            if (!string.IsNullOrEmpty(node.CharacterImagePath))
            {
                try { CharacterImage = new BitmapImage(new Uri(node.CharacterImagePath)); } catch { }
            }
            else
            {
                CharacterImage = null;
            }

            // 3. Seçimləri yenilə (ŞƏRTLİ MƏNTİQ BURADADIR)
            CurrentChoices.Clear();
            foreach (var choice in node.Choices)
            {
                // Yalnız şərti ödəyən düymələri siyahıya əlavə et
                if (EvaluateCondition(choice))
                {
                    CurrentChoices.Add(choice);
                }
            }
        }

        // === YENİ: Şərtləri Yoxlayan "Beyin" ===
        private bool EvaluateCondition(StoryChoice choice)
        {
            // 1. Əgər heç bir şərt qoyulmayıbsa və ya Operator "None" seçilibsə -> Həmişə Göstər
            if (string.IsNullOrEmpty(choice.ConditionVariableName) || choice.Operator == ConditionOperator.None)
            {
                return true;
            }

            // 2. Qlobal dəyişənlər siyahısından lazım olanı tap
            var variable = _currentStory.Variables.FirstOrDefault(v => v.Name == choice.ConditionVariableName);

            // Əgər dəyişən tapılmadısa (məsələn silinib), təhlükəsizlik üçün düyməni gizlət (və ya göstər, strategiyadan asılıdır)
            if (variable == null) return false;

            // 3. Dəyərləri müqayisə et
            // String müqayisəsi üçün hər ikisini kiçik hərfə çeviririk (Case-insensitive)
            string varValue = variable.Value?.ToString().ToLower() ?? "";
            string targetValue = choice.ConditionValue?.ToString().ToLower() ?? "";

            switch (choice.Operator)
            {
                case ConditionOperator.Equals:
                    return varValue == targetValue;

                case ConditionOperator.NotEquals:
                    return varValue != targetValue;

                case ConditionOperator.GreaterThan:
                    // Yalnız rəqəmlər üçün
                    if (double.TryParse(varValue, out double vNumGt) && double.TryParse(targetValue, out double tNumGt))
                        return vNumGt > tNumGt;
                    return false;

                case ConditionOperator.LessThan:
                    // Yalnız rəqəmlər üçün
                    if (double.TryParse(varValue, out double vNumLt) && double.TryParse(targetValue, out double tNumLt))
                        return vNumLt < tNumLt;
                    return false;

                default:
                    return true;
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
