using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SpriteEditor.Data.Story
{


    // === 1. ƏSAS COMMAND SİNİFİ (Polimorfik) ===
    // JSON-da "Type" sahəsinə görə hansı əmr olduğunu biləcəyik
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(SetVariableCommand), typeDiscriminator: "SetVar")]
    [JsonDerivedType(typeof(WaitCommand), typeDiscriminator: "Wait")]
    [JsonDerivedType(typeof(PlaySoundCommand), typeDiscriminator: "PlaySound")]
    [JsonDerivedType(typeof(ShowPortraitCommand), typeDiscriminator: "ShowPortrait")]
    [JsonDerivedType(typeof(HidePortraitCommand), typeDiscriminator: "HidePortrait")]
    [JsonDerivedType(typeof(ShowTextCommand), typeDiscriminator: "ShowText")]
    public abstract partial class StoryCommand : ObservableObject
    {
        [ObservableProperty] private bool _isBlocking = true; // Oyunçu bunu gözləməlidirmi?
    }

    // === 2. KONKRET ƏMRLƏR ===

    // Köhnə "Action"un yeni versiyası (Dəyişənlər üçün)
    public partial class SetVariableCommand : StoryCommand
    {
        [ObservableProperty] private string _targetVariableName;
        [ObservableProperty] private ActionOperation _operation = ActionOperation.Set;
        [ObservableProperty] private string _value;
    }

    // Gözləmə Əmri (Məs: 2 saniyə pauza)
    public partial class WaitCommand : StoryCommand
    {
        [ObservableProperty] private double _durationSeconds = 1.0;
    }

    // Səs Əmri (SFX)
    public partial class PlaySoundCommand : StoryCommand
    {
        [ObservableProperty] private string _audioPath;
        [ObservableProperty] private float _volume = 1.0f;
    }
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

    // YENİ: Node Tipləri
    public enum StoryNodeType
    {
        Dialogue,   // Standart: Personaj danışır
        Event,      // Hadisə: Dəyişənləri dəyişir (Gold += 10)
        Condition,  // Şərt: Seçim edir (Açar varsa -> A, yoxsa -> B)
        Start,      // Başlanğıc
        End         // Son
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

    // 2. Dəyişən Tipləri
    public enum VariableType
    {
        Boolean, // True/False
        Integer, // Rəqəm
        String   // Mətn
    }

    // 3. Seçim (Button)
    public partial class StoryChoice : ObservableObject
    {
        [ObservableProperty] private string _text = "Next";
        [ObservableProperty] private string _targetNodeId; // Hansı qutuya gedəcək?

        // === Şərt Sistemi ===
        [ObservableProperty] private string _conditionVariableName;
        [ObservableProperty] private ConditionOperator _operator = ConditionOperator.None;
        [ObservableProperty] private string _conditionValue;
    }

    // 4. Düyün (Hekayənin bir parçası)
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

        [ObservableProperty]
        private StoryNodeType _type = StoryNodeType.Dialogue;

        // Tip dəyişəndə təmizlik işləri
        partial void OnTypeChanged(StoryNodeType value)
        {
            // Əgər Event, Condition və ya End seçilibsə, dialoq məlumatlarını təmizləmək olar
            // Amma istifadəçi səhvən dəyişərsə deyə, hələlik saxlayırıq.

            if (value == StoryNodeType.Start)
            {
                Title = "Start";
                SpeakerName = "System";
            }
            else if (value == StoryNodeType.End)
            {
                Title = "The End";
                SpeakerName = "System";
            }
        }

        // Seçimlər
        public ObservableCollection<StoryChoice> Choices { get; set; } = new ObservableCollection<StoryChoice>();

        // Giriş Hadisələri (Actions)
        public ObservableCollection<StoryCommand> Commands { get; set; } = new ObservableCollection<StoryCommand>();
    }

    // 5. Bütün Hekayə Qrafı
    public class StoryGraph
    {
        public string Name { get; set; } = "My Story";
        public List<StoryNode> Nodes { get; set; } = new List<StoryNode>();
        public List<StoryVariable> Variables { get; set; } = new List<StoryVariable>();
        public List<StoryCharacter> Characters { get; set; } = new List<StoryCharacter>();
        public string StartNodeId { get; set; }
    }

    // 6. Dəyişən Modeli
    public partial class StoryVariable : ObservableObject
    {
        [ObservableProperty] private string _name = "NewVar";
        [ObservableProperty] private VariableType _type = VariableType.Boolean;
        [ObservableProperty] private string _value = "False";

        partial void OnTypeChanged(VariableType value)
        {
            switch (value)
            {
                case VariableType.Boolean: Value = "False"; break;
                case VariableType.Integer: Value = "0"; break;
                case VariableType.String: Value = "Text.."; break;
            }
        }
    }

    // === 7. CHARACTER MANAGEMENT SYSTEM ===
    
    /// <summary>
    /// Bir portreti təmsil edir (məs: happy, sad, angry)
    /// </summary>
    public partial class CharacterPortrait : ObservableObject
    {
        [ObservableProperty] private string _name = "neutral";
        [ObservableProperty] private string _imagePath = "";
    }

    /// <summary>
    /// Hekayə personajı
    /// </summary>
    public partial class StoryCharacter : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [ObservableProperty] private string _name = "Character Name";
        [ObservableProperty] private string _displayColor = "#3B82F6"; // Mavi (default)
        [ObservableProperty] private string _description = "";
        
        // Portretlər kolleksiyası (happy, sad, angry, və s.)
        public ObservableCollection<CharacterPortrait> Portraits { get; set; } = new ObservableCollection<CharacterPortrait>();
    }

    /// <summary>
    /// Portret mövqeyi
    /// </summary>
    public enum PortraitPosition
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Portret animasiya növləri
    /// </summary>
    public enum PortraitAnimation
    {
        None,
        FadeIn,
        SlideFromLeft,
        SlideFromRight,
        Bounce
    }

    // === 8. YENİ ƏMRLƏR: SHOW/HIDE PORTRAIT ===
    
    /// <summary>
    /// Portreti göstər
    /// </summary>
    public partial class ShowPortraitCommand : StoryCommand
    {
        [ObservableProperty] private string _characterId;
        [ObservableProperty] private string _portraitName = "neutral";
        [ObservableProperty] private PortraitPosition _position = PortraitPosition.Center;
        [ObservableProperty] private PortraitAnimation _animation = PortraitAnimation.FadeIn;
        [ObservableProperty] private double _duration = 0.3; // saniyə
    }

    /// <summary>
    /// Portreti gizlət
    /// </summary>
    public partial class HidePortraitCommand : StoryCommand
    {
        [ObservableProperty] private string _characterId;
        [ObservableProperty] private PortraitAnimation _animation = PortraitAnimation.FadeIn; // Fade out
        [ObservableProperty] private double _duration = 0.3;
    }

    /// <summary>
    /// Narrator mətn göstər (dialog olmadan, məs: "3 years later...")
    /// </summary>
    public partial class ShowTextCommand : StoryCommand
    {
        [ObservableProperty] private string _text = "";
        [ObservableProperty] private double _displayDuration = 2.0; // saniyə
    }
}