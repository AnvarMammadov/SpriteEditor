# ⚡ Quick Start Guide - Story Editor

## 🚀 5 Dəqiqədə Başla!

### 1️⃣ **Demo Hekayəni Yüklə (Ən Asan Yol!)**

```
Story Editor açıldıqdan sonra:

1. Sol paneldə 📖 "Load Sample Story" düyməsinə BAS
2. ▶ "Play Preview" düyməsinə BAS
3. Hekayəni oxu, seçimlər et!

🎉 Artıq necə işlədiyini gördün!
```

---

### 2️⃣ **Sıfırdan Hekayə Yarat**

#### Addım 1: Personaj Yarat
```
Sol panel → CHARACTERS
↓
👤 "Add Character" bas
↓
Adı yaz: "Emma"
Color: #3B82F6
```

#### Addım 2: İlk Node-u Yarat
```
"+ Add Node" bas
↓
Sağ panel-də:
- Speaker Name: Emma
- Dialogue Text: "Hello! My name is Emma."
```

#### Addım 3: Portrait Göstər
```
Sağ panel → COMMANDS
↓
👤 "Portrait" bas
↓
- Character: Emma
- Portrait: neutral
- Position: Center
```

#### Addım 4: Test Et
```
▶ "Play Preview" bas
✅ Emma-nın portreti görünməlidir!
```

---

### 3️⃣ **2-ci Node Əlavə Et (Seçimlər)**

#### Node Yarat
```
"+ Add Node" bas yenidən
↓
Speaker: Emma
Text: "Want to be friends?"
```

#### Əlaqə Yarat
```
İlk node-un SAĞ tərəfindəki kiçik dairəni
↓
2-ci node-a SÜRÜKLƏ
↓
Choice text yaz: "Yes!"
```

#### Test Et
```
▶ Play Preview
✅ İlk node-dan 2-ci node-a keçid olmalıdır
```

---

### 4️⃣ **Variable İstifadə Et**

#### Variable Yarat
```
Sol panel → GLOBAL VARIABLES
↓
"+ Create Variable" bas
↓
Name: friendshipLevel
Type: Integer
Value: 0
```

#### Variable-ı Dəyiş
```
2-ci node seç
↓
COMMANDS → 📊 "Variable"
↓
- Variable: friendshipLevel
- Operation: Add
- Value: 10
```

#### Test Et
```
Play Preview edəndə friendshipLevel 10-a çatmalıdır
```

---

### 5️⃣ **Save Et və Paylaş**

```
💾 "Save" bas
↓
Fayl adı: MyFirstStory.story.json
↓
Dostlarına göndər!
```

---

## 🎨 **CHEAT SHEET**

### Node Tipləri:
| Tip | Nə Vaxt İstifadə Olunur? |
|-----|--------------------------|
| **Start** | Hekayənin başlanğıcı |
| **Dialogue** | Personaj danışır |
| **Event** | Dəyişənləri dəyiş, portrait göstər |
| **Condition** | Dəyişənə görə qərar ver |
| **End** | Hekayənin sonu |

### Command-lar:
| Command | İkon | Funksiya |
|---------|------|----------|
| Show Portrait | 👤 | Personaj portretini göstər |
| Hide Portrait | ❌ | Portreti gizlət |
| Variable | 📊 | Dəyişəni dəyiş |
| Wait | ⏱️ | X saniyə gözlə |
| Sound | 🔊 | Səs oynat |
| Text | 📝 | Narrator mətn göstər |

### Portrait Positions:
- **Left**: Sol tərəf
- **Center**: Mərkəz (böyük)
- **Right**: Sağ tərəf

### Portrait Animations:
- **FadeIn**: Yavaş-yavaş görünür
- **SlideFromLeft**: Soldan gəlir
- **SlideFromRight**: Sağdan gəlir
- **Bounce**: Sıçrayaraq gəlir

---

## 🆘 **YARDIM**

### Problem: "Node hərəkət etmir"
**Həll:** Node-un **başlığına** (yuxarı hissə) basıb sürükləyin

### Problem: "Portrait görünmür"
**Həll:** 
1. Portrait şəkil yolu düzgündürmü?
2. Character ID düzgündürmü?
3. Demo story-dən başlayın (şəkilsiz işləyir)

### Problem: "Seçim düyməsi yoxdur"
**Həll:** Node-lar arasında **əlaqə** yaratmalısınız (port-dan sürükləyin)

### Problem: "Play Preview işləmir"
**Həll:**
1. Start node varmı? (★ işarəsi)
2. Node-a sağ klik → "Set as Start Node"

---

## 📖 **DAHA ÇOX ÖYRƏN**

Ətraflı təlimat:
- `STORY_EDITOR_GUIDE.md` - Tam istifadə təlimatı
- `SAMPLE_STORY_INFO.md` - Demo hekayə haqqında
- `STORY_ENGINE_ROADMAP.md` - Gələcək xüsusiyyətlər

---

## 🎯 **5 DƏQİQƏLİK CHALLENGe**

Hazır demo-nu yükləyib test etdinizsə, indi bu challenge-ı cəhd edin:

**Tapşırıq:** 3 personajlı kiçik hekayə yarat
- Personajlar: Alice, Bob, Charlie
- Ən azı 5 node
- 2 fərqli son
- Portrait-lər istifadə et

**Vaxt:** 5 dəqiqə  
**Bonus:** Dəyişən (variable) istifadə et!

---

**Uğurlar! Əla hekayələr yaradacaqsan! 🎉**
