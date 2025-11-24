using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace SpriteEditor.Data.Story
{

    // === YENİ: Əməliyyat Növləri ===
    public enum ActionOperation
    {
        Set,      // = (Mənimsət, Məs: Açar = True)
        Add,      // + (Topla, Məs: Gold += 10)
        Subtract, // - (Çıx, Məs: HP -= 20)
        Toggle    // ! (Tərsinə çevir, yalnız Boolean üçün. True->False)
    }

    // === YENİ: Düyün Hadisəsi (Action) ===
    public partial class StoryNodeAction : ObservableObject
    {
        // Hansı dəyişən dəyişəcək?
        [ObservableProperty] private string _targetVariableName;

        // Nə baş verəcək? (=, +, -)
        [ObservableProperty] private ActionOperation _operation = ActionOperation.Set;

        // Yeni dəyər nədir? (Məs: "True", "50")
        // Toggle əməliyyatı üçün bu boş qala bilər.
        [ObservableProperty] private string _value;
    }

    // 1. Müqayisə Operatorları
    public enum ConditionOperator
    {
        None,           // Şərt yoxdur
        Equals,         // Bərabərdir (==)
        NotEquals,      // Bərabər deyil (!=)
        GreaterThan,    // Böyükdür (>) - Yalnız Integer üçün
        LessThan        // Kiçikdir (<) - Yalnız Integer üçün
    }

    // 2. Dəyişən Tipləri (Sadəlik üçün hələlik 3 əsas tip)
    public enum VariableType
    {
        Boolean, // True/False (Məs: HasKey)
        Integer, // Rəqəm (Məs: Gold, Health)
        String   // Mətn (Məs: PlayerName)
    }

    // 3. Seçim (Button)
    public partial class StoryChoice : ObservableObject
    {
        [ObservableProperty] private string _text = "Next";
        [ObservableProperty] private string _targetNodeId; // Hansı qutuya gedəcək?

        // === YENİ: Şərt Sistemi ===

        // Hansı dəyişəni yoxlayırıq? (Dəyişənin Adı)
        // Qeyd: Birbaşa StoryVariable obyektini saxlamırıq, çünki Save/Load zamanı ad (string) daha etibarlıdır.
        [ObservableProperty] private string _conditionVariableName;

        // Necə yoxlayırıq? (==, !=, >, <)
        [ObservableProperty] private ConditionOperator _operator = ConditionOperator.None;

        // Hansı dəyərlə müqayisə edirik? (Məsələn: "True", "5", "RedKey")
        [ObservableProperty] private string _conditionValue;
    }

    // 4. Düyün (Hekayənin bir parçası)
    // ObservableObject edirik ki, Editor-da yazanda UI dərhal yenilənsin
    public partial class StoryNode : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty] private double _x;
        [ObservableProperty] private double _y;
        [ObservableProperty] private bool _isStartNode;
        [ObservableProperty] private string _title = "New Node";
        [ObservableProperty] private string _text = "Dialogue text goes here...";
        [ObservableProperty] private string _speakerName = "Character";
        [ObservableProperty] private string _backgroundImagePath;
        [ObservableProperty] private string _characterImagePath;
        [ObservableProperty] private string _audioPath;

        // Seçimlər
        public ObservableCollection<StoryChoice> Choices { get; set; } = new ObservableCollection<StoryChoice>();

        // === YENİ ƏLAVƏ: Giriş Hadisələri (Actions) ===
        // Oyunçu bu düyünə girən kimi bu siyahıdakı əməliyyatlar icra olunacaq.
        public ObservableCollection<StoryNodeAction> OnEnterActions { get; set; } = new ObservableCollection<StoryNodeAction>();
    }

    // 5. Bütün Hekayə Qrafı
    public class StoryGraph
    {
        public string Name { get; set; } = "My Story";
        public List<StoryNode> Nodes { get; set; } = new List<StoryNode>();

        // YENİ ƏLAVƏ: Dəyişənlər Siyahısı
        public List<StoryVariable> Variables { get; set; } = new List<StoryVariable>();

        public string StartNodeId { get; set; }
    }


    // 6. Dəyişən Modeli
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
