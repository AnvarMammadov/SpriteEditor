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

        [ObservableProperty] private bool _isStartNode;

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

        // YENİ ƏLAVƏ: Dəyişənlər Siyahısı
        public List<StoryVariable> Variables { get; set; } = new List<StoryVariable>();

        public string StartNodeId { get; set; }
    }



    // 1. Dəyişən Tipləri (Sadəlik üçün hələlik 3 əsas tip)
    public enum VariableType
    {
        Boolean, // True/False (Məs: HasKey)
        Integer, // Rəqəm (Məs: Gold, Health)
        String   // Mətn (Məs: PlayerName)
    }

    // 2. Dəyişən Modeli
    public partial class StoryVariable : ObservableObject
    {
        [ObservableProperty]
        private string _name = "NewVar";

        [ObservableProperty]
        private VariableType _type = VariableType.Boolean;

        [ObservableProperty]
        private string _value = "False";

        // BU HİSSƏ YENİDİR: Tip dəyişdikdə avtomatik işə düşür
        partial void OnTypeChanged(VariableType value)
        {
            switch (value)
            {
                case VariableType.Boolean:
                    Value = "False";
                    break;
                case VariableType.Integer:
                    Value = "0";
                    break;
                case VariableType.String:
                    Value = "Text..";
                    break;
            }
        }
    }


}
