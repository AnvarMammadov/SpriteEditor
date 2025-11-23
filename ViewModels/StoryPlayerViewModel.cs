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

            if (!string.IsNullOrEmpty(node.CharacterImagePath))
            {
                try { CharacterImage = new BitmapImage(new Uri(node.CharacterImagePath)); } catch { }
            }

            // 3. Seçimləri yenilə
            CurrentChoices.Clear();
            foreach (var choice in node.Choices)
            {
                CurrentChoices.Add(choice);
            }

            // Əgər seçim yoxdursa, avtomatik "Bitdi" və ya "Davam et" əlavə etmək olar
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
