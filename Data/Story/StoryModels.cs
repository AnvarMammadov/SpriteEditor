using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpriteEditor.Data.Story
{
    // 1. Seçim (Button)
    public class StoryChoice
    {
        public string Text { get; set; } = "Next";
        public string TargetNodeId { get; set; } // Hansı qutuya gedəcək?
    }

    // 2. Düyün (Hekayənin bir parçası)
    // ObservableObject edirik ki, Editor-da yazanda UI dərhal yenilənsin
    public partial class StoryNode : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Editor üçün Koordinatlar (JSON-da saxlanacaq)
        [ObservableProperty] private double _x;
        [ObservableProperty] private double _y;

        // Məzmun
        [ObservableProperty] private string _title = "New Node";
        [ObservableProperty] private string _text = "Dialogue text goes here...";
        [ObservableProperty] private string _speakerName = "Character";

        // Resurslar (Fayl yolları)
        [ObservableProperty] private string _backgroundImagePath;
        [ObservableProperty] private string _characterImagePath;

        // Seçimlər
        public List<StoryChoice> Choices { get; set; } = new List<StoryChoice>();
    }

    // 3. Bütün Hekayə Qrafı
    public class StoryGraph
    {
        public string Name { get; set; } = "My Story";
        public List<StoryNode> Nodes { get; set; } = new List<StoryNode>();
        public string StartNodeId { get; set; } // Başlanğıc nöqtəsi
    }
}
