# 🚀 Story Editor → Visual Novel Engine Roadmap

## 📊 **HAZıRKı VƏZİYYƏT (v2.0 - Character System)**

### ✅ **Tamamlanan (Bu sessiya)**

#### 1. Data Models
- `StoryCharacter` - Personaj modeli (ad, rəng, təsvir)
- `CharacterPortrait` - Portret modeli (emosiya adı, şəkil yolu)
- `PortraitPosition` enum - Left/Center/Right
- `PortraitAnimation` enum - FadeIn, Slide, Bounce
- `ShowPortraitCommand` - Portret göstər əmri
- `HidePortraitCommand` - Portret gizlət əmri
- `ShowTextCommand` - Narrator mətn əmri

#### 2. ViewModel Updates
- `StoryEditorViewModel`:
  - `Characters` collection əlavə edildi
  - `AddCharacter/DeleteCharacter` commands
  - `AddShowPortraitCommand/AddHidePortraitCommand`
  - Save/Load metodlarında Characters dəstəyi
  
- `StoryPlayerViewModel`:
  - `PortraitLeft/Center/Right` image sources
  - `IsPortraitLeftVisible` və s. visibility properties
  - `ShowPortrait()` / `HidePortrait()` metodları
  - Command execution sistemində portrait dəstəyi

#### 3. UI Improvements
- **StoryEditorView.xaml**:
  - Character Library panel (sol paneldə)
  - Portrait/Hide/Text command buttons
  - DataTemplate-lər hər command tipi üçün
  - Icon-based command display (👤, ❌, 📊, ⏱️, 🔊)
  
- **StoryPlayerWindow.xaml**:
  - 3 portrait position (Left/Center/Right)
  - Improved choice buttons (60px height, 18px font)
  - "Click to continue" hint
  - Better hover effects

---

## 🎯 **FAZA 3: UX & EASE OF USE** (Prioritet: Yüksək)

### Məqsəd: Yaşlı və texniki bilməyən istifadəçilər asanlıqla istifadə etsin

#### 1. Node Templates System
```csharp
public class NodeTemplate
{
    string Name { get; set; } // "Character Introduction"
    string Description { get; set; }
    StoryNode CreateNode(); // Hazır şablon yaradır
}
```

**Şablonlar:**
- "Character Introduction" - Personaj təqdimatı
- "Choice Scene" - Seçim sahəsi (2-3 variant)
- "Flashback" - Flashback sahəsi (fade effect)
- "Ending" - Son sahə

**UI:**
- Right-click → "Insert Template" menu

#### 2. Portrait Manager UI
**Problem:** İndiki sistemdə portrait-lər JSON-da əl ilə əlavə olunur

**Həll:** UI vasitəsilə portrait əlavə etmək

```xaml
<!-- Character properties panel -->
<Expander Header="PORTRAITS">
    <ItemsControl ItemsSource="{Binding SelectedCharacter.Portraits}">
        <!-- Portret adı + şəkil seçimi + delete button -->
    </ItemsControl>
    <Button Command="AddPortraitCommand" Content="+ Add Emotion"/>
</Expander>
```

**Quick emotions dropdown:**
- "Add Standard Set" button → Avtomatik 6 emosiya əlavə edir:
  - neutral, happy, sad, angry, surprised, thinking

#### 3. Minimap Navigator
**Problem:** Böyük hekayələrdə itib gedirsən

**Həll:** Kiçik minimap (sağ yuxarı küncdə)

```csharp
// New UI control
public class MinimapControl : UserControl
{
    - Canvas-dakı bütün node-ları kiçik ölçüdə göstər
    - Click edəndə həmin node-a pan et
    - Start node yaşıl, End node qırmızı
    - Current selected node sarı border
}
```

#### 4. Search & Filter
**Funksionallıq:**
- `Ctrl+F` → Axtarış paneli açılır
- Node adına görə axtar
- Speaker adına görə axtar (məs: "Alice" danışan bütün node-lar)
- Text content-ə görə (məs: "key" sözü olan node-lar)

```csharp
[RelayCommand]
public void SearchNodes(string query)
{
    var results = Nodes.Where(n => 
        n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        n.SpeakerName.Contains(query) ||
        n.Text.Contains(query)
    ).ToList();
    
    // Highlight results
    foreach (var node in results)
        node.IsHighlighted = true;
}
```

#### 5. Comment Nodes
**Məqsəd:** Node-lar arasında qeydlər yazmaq (execute olunmur)

```csharp
public enum StoryNodeType
{
    ...
    Comment  // Yeni tip
}
```

**Xüsusiyyətləri:**
- Sarı rəng
- Execute zamanı skip olunur
- Böyük text area (development notes üçün)

#### 6. Interactive Tutorial
**İlk dəfə açanda:**
1. Welcome screen: "First time using Story Editor?"
2. Step-by-step guide (highlight hər element):
   - "This is the node canvas..."
   - "Create your first character here..."
   - "Add a dialogue node..."
   - "Connect nodes by dragging..."
3. Finish → "Create Sample Story" button (demo hekayə yaradır)

---

## 🎭 **FAZA 4: ADVANCed VN FEATURES** (2-3 həftə)

### 1. Narrative Script Mode (Ink/Yarn kimi)
**Məqsəd:** Node yaratmadan, text yazaraq hekayə qurmaq

**Format:**
```ink
=== start ===
You meet Alice in the park.
Alice (happy): Hi! It's a beautiful day!
    * Yes, it is!
        Alice (happy): Let's go for a walk!
        -> park_walk
    * I prefer rainy days.
        Alice (surprised): Oh... unusual!
        -> end

=== park_walk ===
...
```

**Implementation:**
- `NarrativeScriptParser` class
- Parse text → Auto-generate nodes
- Bi-directional: Node-lardan script generate et

### 2. Camera Effects
```csharp
public class CameraEffectCommand : StoryCommand
{
    CameraEffect Effect { get; set; } // Shake, Zoom, Pan
    double Duration { get; set; }
    double Intensity { get; set; }
}

public enum CameraEffect
{
    Shake,      // Titrəmə (zəlzələ, partlayış)
    ZoomIn,     // Yaxınlaşma (drama)
    ZoomOut,    // Uzaqlaşma
    PanLeft,    // Sola hərəkət
    PanRight    // Sağa hərəkət
}
```

**Storyboard animations ilə:**
```csharp
private async Task ExecuteShake()
{
    var storyboard = new Storyboard();
    var animation = new DoubleAnimation
    {
        From = 0, To = 10, Duration = 0.05, AutoReverse = true, RepeatBehavior = new RepeatBehavior(5)
    };
    Storyboard.SetTarget(animation, MainCanvas);
    Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
    storyboard.Begin();
}
```

### 3. Transition Effects
```csharp
public class TransitionCommand : StoryCommand
{
    TransitionType Type { get; set; }
    double Duration { get; set; }
}

public enum TransitionType
{
    FadeToBlack,
    FadeFromBlack,
    Dissolve,       // Cross-fade
    Wipe,           // Ekran süpürülür
    Flash           // Ağ flash (yaddaş və s.)
}
```

### 4. Portrait Animations (Advanced)
**İndi:** Static şəkillər  
**Gələcək:** Animated sprites

```csharp
public class AnimatedPortrait
{
    List<BitmapImage> Frames { get; set; }  // Frame-by-frame animation
    double FrameRate { get; set; } = 12;     // FPS
    
    // Idle animations:
    BlinkAnimation { get; set; }  // Göz qırpma
    BreathingAnimation { get; set; }  // Nəfəs alma (yüngül scale)
}
```

**Lip-sync (çox advanced):**
- Audio dalğa formasını analiz et
- Ağız hərəkəti ilə sync et

### 5. Multiple Character Dialogue
**Problem:** İndi bir node = bir speaker

**Həll:** Node içində multiple speakers

```csharp
public class DialogueLine : ObservableObject
{
    string SpeakerName { get; set; }
    string Text { get; set; }
    string Portrait { get; set; }  // happy, sad, etc.
}

public partial class StoryNode : ObservableObject
{
    ...
    ObservableCollection<DialogueLine> DialogueLines { get; set; }  // Yeni!
}
```

**Player-də:**
```
Alice (happy): Hey Bob, how are you?
Bob (tired): Ugh, not great...
Alice (worried): What happened?
```

---

## 🎮 **FAZA 5: PLAYER & RUNTIME** (3-4 həftə)

### 1. History / Rollback System
**Visual Novel-ın must-have xüsusiyyəti!**

```csharp
public class StoryHistory
{
    Stack<StoryState> PreviousStates { get; set; }
    
    public void SaveState(StoryState state);
    public StoryState Rollback();  // Mouse wheel up
}

public class StoryState
{
    string CurrentNodeId { get; set; }
    Dictionary<string, string> Variables { get; set; }
    Dictionary<PortraitPosition, PortraitInfo> Portraits { get; set; }
}
```

**UI:**
- Mouse wheel up → Əvvəlki dialoqa qayıt
- Right panel: History log (son 20 dialoq)

### 2. Auto-play Mode
```csharp
public partial class StoryPlayerViewModel : ObservableObject
{
    [ObservableProperty] bool _isAutoPlayEnabled;
    [ObservableProperty] double _autoPlaySpeed = 2.0;  // saniyə
    
    private async Task AutoAdvance()
    {
        while (IsAutoPlayEnabled)
        {
            await Task.Delay(TimeSpan.FromSeconds(AutoPlaySpeed));
            if (CurrentChoices.Count == 0)
                GoToNextNode();
            else
                break;  // Seçim varsa dayansın
        }
    }
}
```

**UI:**
- ▶️ / ⏸️ button (bottom-right)
- Speed slider: 1x, 1.5x, 2x, 3x

### 3. Save/Load System (Runtime)
**Oyunçu oyunu yadda saxlaya və davam etdirə bilsin**

```csharp
public class GameSave
{
    DateTime SaveTime { get; set; }
    string CurrentNodeId { get; set; }
    Dictionary<string, string> Variables { get; set; }
    string Screenshot { get; set; }  // Base64 thumbnail
}

[RelayCommand]
public async Task SaveGame(int slotNumber)
{
    var save = new GameSave
    {
        SaveTime = DateTime.Now,
        CurrentNodeId = CurrentNode.Id,
        Variables = _currentStory.Variables.ToDictionary(v => v.Name, v => v.Value),
        Screenshot = CaptureScreenshot()
    };
    
    await File.WriteAllTextAsync($"save_{slotNumber}.json", JsonSerializer.Serialize(save));
}
```

**UI:**
- ESC açır → Save/Load menu
- 3 save slot (thumbnail + tarix)

### 4. Settings Menu
- Text speed slider
- Auto-play speed
- Master volume
- Fullscreen toggle
- Language (gələcək)

### 5. Skip Mode
- Əvvəl oxunan dialoquları avtomatik keç
- **Ctrl** basıb saxla → Skip
- Yeni dialoqa çatanda dayansın

---

## 🌍 **FAZA 6: LOCALİZATION & EXPORT** (2 həftə)

### 1. Multi-language Support
```csharp
public class LocalizedText
{
    string DefaultText { get; set; }
    Dictionary<string, string> Translations { get; set; }
    
    // "en" -> "Hello"
    // "az" -> "Salam"
    // "ru" -> "Привет"
}

public partial class StoryNode : ObservableObject
{
    LocalizedText Text { get; set; }  // String əvəzinə
    LocalizedText Title { get; set; }
}
```

**Editor-da:**
- Language dropdown: EN / AZ / RU
- Translation panel (side-by-side)

### 2. Export Options
**Standalone executable:**
- .NET 8 Self-contained deployment
- Story JSON embedded as resource
- Custom player window (branded)

**Unity Plugin:**
- Export story as Unity ScriptableObject
- Custom Unity inspector
- Integration with Unity UI

**Web (Blazor):**
- Export to WebAssembly
- Play in browser
- Host on Itch.io

### 3. Asset Bundler
**Problem:** Story + 100 şəkil faylı = çətin paylaşmaq

**Həll:** Zip archive
```
MyStory.vnpack  (ZIP formatı)
├── story.json
├── assets/
│   ├── characters/
│   │   ├── alice_happy.png
│   │   ├── alice_sad.png
│   ├── backgrounds/
│   │   ├── park.jpg
│   │   ├── cafe.jpg
│   └── audio/
│       ├── music_theme.mp3
│       └── sfx_door.wav
```

---

## 🔬 **FAZA 7: ANALYTICS & DEBUG** (1 həftə)

### 1. Playtest Analytics
```csharp
public class PlaytestData
{
    Dictionary<string, int> NodeVisitCount { get; set; }
    Dictionary<string, int> ChoiceSelectionCount { get; set; }
    
    // Hansı seçim daha populyardır?
    // Hansı node heç vaxt görünməyib? (unreachable code)
}
```

**UI:**
- Heatmap: Node-ları ziyarət sayına görə rəngləndir
- Choice statistics: "80% users chose Option A"

### 2. Debug Mode
**Editor-da:**
- "Debug Run" button
  - Hər node-da dayanır
  - Variable values göstərir
  - "Step Over" / "Continue" buttons

**Breakpoints:**
- Node-a sağ klik → "Set Breakpoint"
- Qırmızı icon göstərir

### 3. Validation & Error Checking
```csharp
[RelayCommand]
public List<ValidationError> ValidateStory()
{
    var errors = new List<ValidationError>();
    
    // Orphan nodes (heç bir node-a bağlı deyil)
    foreach (var node in Nodes)
    {
        if (!node.IsStartNode && !Connections.Any(c => c.Target == node))
            errors.Add(new ValidationError($"Node '{node.Title}' is unreachable"));
    }
    
    // Missing portraits
    foreach (var cmd in AllCommands.OfType<ShowPortraitCommand>())
    {
        var character = Characters.FirstOrDefault(c => c.Id == cmd.CharacterId);
        if (character == null)
            errors.Add(new ValidationError($"Character not found: {cmd.CharacterId}"));
    }
    
    return errors;
}
```

**UI:**
- ⚠️ icon (top bar) → Errors list
- Click error → Jump to problem node

---

## 🏆 **FAZA 8: ADVANCED FEATURES** (Bonus)

### 1. Branching Visualizer
**Məqsəd:** Storyline-ların axını görmək

**UI:**
- "Flowchart View" button
- Bütün yolları göstər (Start-dan End-a qədər)
- Dead ends highlight et

### 2. Character Relationship System
```csharp
public class RelationshipManager
{
    Dictionary<(string, string), int> Relationships { get; set; }
    // ("Alice", "Bob") -> 75 (friendship level)
    
    public void ChangeRelationship(string char1, string char2, int delta);
}
```

**Commands:**
- `ChangeRelationshipCommand`
- UI-da graph göstərilir

### 3. Inventory System
```csharp
public class InventoryItem
{
    string Id { get; set; }
    string Name { get; set; }
    string IconPath { get; set; }
}

// Condition: "if (inventory.Contains("Key")) ..."
```

### 4. Achievement System
```csharp
public class Achievement
{
    string Id { get; set; }
    string Title { get; set; }
    string Description { get; set; }
    Condition UnlockCondition { get; set; }
}
```

### 5. Mini-games Integration
- Quick-time events
- Simple puzzles
- Choice timers ("Cavab ver: 10... 9... 8...")

---

## 📈 **PRIORITET SIRALAMASı**

### ⚡ Yüksək Prioritet (1-2 ay):
1. Portrait Manager UI
2. Node Templates
3. Minimap Navigator
4. Search & Filter
5. History/Rollback
6. Auto-play

### 🔶 Orta Prioritet (3-4 ay):
1. Narrative Script Mode
2. Camera Effects
3. Transition Effects
4. Save/Load System
5. Settings Menu
6. Validation System

### 🔹 Aşağı Prioritet (5-6+ ay):
1. Localization
2. Export options
3. Analytics
4. Relationship System
5. Inventory
6. Achievements

---

## 📊 **RESOURce ESTİMATEs**

| Faza | Xüsusiyyətlər | Təxmini Vaxt | Çətinlik |
|------|---------------|--------------|----------|
| 3 | UX Improvements | 1-2 həftə | Orta |
| 4 | Advanced VN | 2-3 həftə | Yüksək |
| 5 | Player/Runtime | 3-4 həftə | Yüksək |
| 6 | Localization | 2 həftə | Orta |
| 7 | Analytics | 1 həftə | Aşağı |
| 8 | Advanced | 4+ həftə | Çox Yüksək |

**TOTAL:** ~3-4 ay full development

---

## 🎯 **SUCCESS METRİCS**

### Fungus-la müqayisə:
| Feature | Fungus | Bizim Engine | Status |
|---------|--------|--------------|--------|
| Node-based editor | ✅ | ✅ | ✅ Par |
| Character system | ✅ | ✅ | ✅ Par |
| Portrait system | ✅ | ✅ | ✅ Par |
| Variables | ✅ | ✅ | ✅ Par |
| Conditionals | ✅ | ✅ | ✅ Par |
| Commands | ✅ | ✅ | ✅ Par |
| Camera effects | ✅ | ❌ | 🔶 Faza 4 |
| Transitions | ✅ | ❌ | 🔶 Faza 4 |
| Narrative script | ✅ | ❌ | 🔶 Faza 4 |
| Localization | ✅ | ❌ | 🔶 Faza 6 |
| History/Rollback | ✅ | ❌ | 🔶 Faza 5 |
| Save/Load | ✅ | ❌ | 🔶 Faza 5 |

**Current Match:** ~50%  
**After Faza 4-6:** ~85%  
**After Faza 7-8:** 100%+ (bəzi xüsusiyyətlərdə üstün)

---

## 💡 **UNİQue SELLING POİNTS**

### Fungus-dan fərqlərimiz:
1. **Standalone app** (Unity-dən asılı deyil)
2. **7-in-1 toolkit** (Rigging, Packer və s. alətlər də var)
3. **Modern UI** (Dark theme, WPF)
4. **Multi-language by default** (7 dil dəstəyi)
5. **Export flexibility** (Standalone, Unity, Web)
6. **Open data format** (JSON, asanlıqla edit olunur)

---

**Nəticə:** Bu roadmap ilə gedərsək, 4-6 ay ərzində **Fungus səviyyəsində** və bəzi sahələrdə **daha yaxşı** bir Visual Novel Engine-imiz olacaq! 🚀

_Last updated: December 2024_
