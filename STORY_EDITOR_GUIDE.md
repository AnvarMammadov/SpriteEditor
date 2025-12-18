# 📖 Story Editor - Tam İstifadə Təlimatı

## 🎉 YENİ XÜSUSİYYƏTLƏR (v2.0)

### ✨ Character Management System
Story Editor artıq **professional Visual Novel engine** kimi işləyir!

---

## 📚 **İSTİFADƏ TƏLİMATI**

### 1. **Character Yaratmaq**

#### Sol Panel → "CHARACTERS" bölməsi:
1. **"👤 Add Character"** düyməsinə basın
2. Personajın **adını** yazın (məs: "Alice", "Bob")
3. **Display Color** - personaj üçün rəng seçin (hex format: #3B82F6)

#### Portrait (Portret) əlavə etmək:
- Hər personaj üçün **müxtəlif emosiyalar** əlavə edə bilərsiniz:
  - `neutral` (neytral)
  - `happy` (xoşbəxt)
  - `sad` (kədərli)
  - `angry` (qəzəbli)
  - `surprised` (təəccüblü)
  - və s.

**Necə əlavə olunur?**
- Layihəni save etdikdən sonra JSON faylında əl ilə və ya kod vasitəsilə portrait-lər əlavə olunur
- Gələcək versiyada UI vasitəsilə də əlavə edə biləcəksiniz

---

### 2. **Node Yaratmaq və Konfiqurasiya**

#### Node tipləri:
- **Dialogue** - Standart dialoq (personaj danışır)
- **Event** - Hadisə (dəyişənləri dəyişir, portret göstərir)
- **Condition** - Şərt (dəyişənə görə qərar verir)
- **Start** - Başlanğıc node-u
- **End** - Son node

---

### 3. **🎭 Portrait System (ƏN ÖNƏMLİ!)**

#### Portreti göstərmək:
1. Bir node seçin
2. Sağ paneldə **COMMANDS** bölməsindən:
   - **"👤 Portrait"** düyməsinə basın
3. Konfiqurasiya edin:
   - **Character**: Hansı personaj? (Alice, Bob...)
   - **Portrait**: Hansı emosiya? (happy, sad, neutral...)
   - **Position**: Harada göstərilsin?
     - `Left` - Sol tərəf
     - `Center` - Mərkəz (böyük)
     - `Right` - Sağ tərəf
   - **Animation**: Necə görünsün?
     - `FadeIn` - Yavaş-yavaş görünür
     - `SlideFromLeft` - Soldan sürüşür
     - `SlideFromRight` - Sağdan sürüşür
     - `Bounce` - Sıçrayaraq gəlir

#### Portreti gizlətmək:
- **"❌ Hide"** düyməsi ilə portreti ekrandan sil

---

### 4. **Digər Əmrlər (Commands)**

#### 📊 **Set Variable** (Dəyişən dəyişmək)
- Oyun dəyişənlərini idarə edin
- Məs: `health = 100`, `hasKey = True`

#### ⏱️ **Wait** (Gözləmək)
- Oyunçunu X saniyə dayandır
- Dramatik effektlər üçün

#### 🔊 **Sound** (Səs)
- Səs effektləri oynat
- Musiqi də əlavə edə bilərsiniz

#### 📝 **Show Text** (Narrator mətn)
- Dialoq olmadan mətn göstər
- Məs: "3 years later..." kimi

---

## 🎮 **OYUNU TEST ETMƏK**

1. Sol paneldə **"▶ Play Preview"** düyməsinə basın
2. Hekayə yeni pəncərədə açılacaq
3. **Click** edərək irəliləyin
4. **Seçimlər** (Choices) görünəndə birini seçin

---

## 💾 **YADDA SAXLAMA**

### Save (Saxla):
- **"💾 Save"** düyməsi
- `.story.json` formatında saxlanır
- **Nə saxlanır?**
  - Bütün node-lar
  - Dəyişənlər (Variables)
  - **Personajlar və portretlər** ✨
  - Əlaqələr (Connections)

### Load (Yüklə):
- **"📂 Load"** düyməsi
- Əvvəlki işiniz davam edin

---

## 🔥 **MİSAL: SADƏ HEKAYƏ**

### Scenario: Alice ilə görüşmək

#### Node 1 (Start):
- **Type**: Start
- **Text**: "You meet Alice in the park."

#### Node 2 (Show Alice Portrait):
- **Type**: Dialogue
- **Commands**:
  - `👤 Show Portrait`:
    - Character: Alice
    - Portrait: happy
    - Position: Center
- **Speaker**: Alice
- **Text**: "Hi! It's a beautiful day, isn't it?"

#### Node 3 (Choice):
- **Choices**:
  - "Yes, it is!" → Node 4
  - "I prefer rainy days." → Node 5

#### Node 4 (Alice happy):
- **Commands**:
  - `👤 Show Portrait`:
    - Character: Alice
    - Portrait: happy
- **Text**: "Me too! Want to go for a walk?"

#### Node 5 (Alice confused):
- **Commands**:
  - `👤 Show Portrait`:
    - Character: Alice
    - Portrait: surprised
- **Text**: "Oh... that's unusual!"

---

## 🎨 **GÖRSƏLLƏŞDİRMƏ**

### Portrait mövqeləri (Player-də):
```
┌─────────────────────────────────┐
│  [Alice]     [Bob]     [Eve]    │  ← Personajlar
│   Left      Center     Right    │
│                                  │
│  ┌───────────────────────────┐  │
│  │  Alice: "Hello there!"    │  │  ← Dialog box
│  │  ▼ Click to continue      │  │
│  └───────────────────────────┘  │
└─────────────────────────────────┘
```

---

## 📋 **NODE ÜZƏRİNDƏ COMMANDS SIRALAMASı**

Commands **yuxarıdan aşağıya** icra olunur:

```
Node: "Alice Entrance"
├─ 👤 Show Portrait (Alice, happy, Center)
├─ ⏱️ Wait (0.5 sec)
├─ 🔊 Play Sound (door_open.mp3)
└─ 📊 Set Variable (metAlice = True)
```

---

## ⚠️ **ÖNƏMLİ QEYDLƏR**

### Yaşlı istifadəçilər üçün:
1. **Böyük düymələr** - Asanlıqla basmaq olur
2. **Aydın yazılar** - 17-18px font ölçüsü
3. **Rəngli node-lar** - Tip üzrə fərqlənir (Start=yaşıl, End=qırmızı)
4. **Hint mətnlər** - "Click to continue" yazısı

### Performance:
- **Böyük portretlər** (>2000px) yavaşlıq yarada bilər
- Tövsiyə: 1000x1500px və ya daha kiçik
- PNG format (şəffaf arxa plan üçün)

---

## 🚀 **GƏLƏCƏKDƏ GƏLƏN XÜSUSİYYƏTLƏR**

### Faza 3 (2 həftə):
- [ ] **Narrative Script Mode** - Kod yazmadan, mətn faylı kimi hekayə yazmaq (Ink formatı)
- [ ] **Character emotion shortcuts** - UI-da 1 kliklə emosiya dəyişmək
- [ ] **Preview window** - Editor-da dərhal görüntü
- [ ] **Auto-save** - Avtomatik yadda saxlama
- [ ] **Undo/Redo** - Geri al / İrəli al

### Faza 4 (3-4 həftə):
- [ ] **Camera effects** - Shake, zoom, fade
- [ ] **Transitions** - Fade to black, dissolve
- [ ] **Animated sprites** - Portret animasiyaları (göz qırpma və s.)
- [ ] **Voice acting support** - Hər dialoq üçün səs faylı
- [ ] **Localization** - Çoxdilli hekayələr

### Faza 5 (Engine səviyyəsi):
- [ ] **History/Rollback** - Əvvəlki dialoqulara qayıtmaq
- [ ] **Auto-play mode** - Oxumadan avtomatik keçiş
- [ ] **Save/Load system** (runtime) - Oyunçu qeydiyyat sistemi
- [ ] **Achievement system** - Nailiyyətlər
- [ ] **Analytics** - Hansı seçimlərin daha populyar olduğunu görmək

---

## 🛠️ **TROUBLESHOOTİNG**

### Problem: Portret görünmür
**Həll:**
1. Character-in ID-si düzgündürmü?
2. Portrait name düzgün yazılıbmı? (case-sensitive!)
3. Şəkil yolu doğrudurmu?

### Problem: Node hərəkət etmir (drag olmur)
**Həll:**
- Node-un başlığına (header) basıb sürükləyin
- Port düyməsinə deyil!

### Problem: Seçimlər görünmür
**Həll:**
1. Choice-lar əlavə olunubmu?
2. Condition-lar düzgündürmü? (əgər varsa)
3. TargetNodeId boş deyilmi?

---

## 📞 **DƏSTƏK**

Suallarınız varsa:
- Email: support@spriteeditorpro.com
- Discord: [Bizdə qoşul](#)
- GitHub Issues: Bug reportları üçün

---

**Uğurlar! Əla hekayələr yaradın! 🎉**

_Last updated: December 2024_
