# 📖 Demo Hekayə - "A Sunny Day with Friends"

## 🎉 YENİ XÜSUSİYYƏT: Sample Story!

İndi Story Editor-də **hazır demo hekayə** var! 

### Necə istifadə olunur?

1. **Story Editor**-i açın
2. Sol paneldə **"📖 Load Sample Story"** düyməsinə basın
3. Demo hekayə avtomatik yüklənəcək
4. **"▶ Play Preview"** basaraq test edin!

---

## 📚 Demo Hekayənin Strukturu

### Personajlar (2 ədəd):
- **Alice** (Mavi rəng)
  - Portraits: neutral, happy, sad
  - Xarakter: Sevimli və enerjili qadın

- **Bob** (Qırmızı rəng)
  - Portraits: neutral, tired, surprised
  - Xarakter: Alice-in köhnə dostu

### Dəyişənlər (Variables):
- `metBob` (Boolean) - Bob ilə görüşdünüzmü?
- `friendship` (Integer) - Dostluq səviyyəsi

### Node-lar (10 ədəd):

#### 1️⃣ **Start Node**
- Tip: Start
- Mətn: "You wake up on a beautiful sunny morning..."
- İlk node (yaşıl)

#### 2️⃣ **Meet Alice**
- Tip: Dialogue
- Speaker: Alice
- **Command**: Show Portrait (Alice, happy, Center, FadeIn)
- Mətn: "Good morning! Isn't it a wonderful day?"

#### 3️⃣ **Response Choice**
- Tip: Dialogue
- Alice soruşur: "Want to come to the park?"
- **2 seçim**:
  - ✅ "Sure! I'd love to."
  - ❌ "Sorry, I'm busy."

#### 4️⃣ **Accept** (qəbul edirsinizsə)
- **Command**: Set Variable (friendship += 10)
- Mətn: "Sure! I'd love to join you."
- → At Park node-na keçir

#### 5️⃣ **Decline** (rədd edirsinizsə)
- **Command**: Show Portrait (Alice, sad)
- Mətn: "Sorry, I have things to do..."
- → Sad Ending node-na keçir

#### 6️⃣ **At Park** (accept etdinizsə)
- **Commands:**
  1. Show Portrait (Alice, happy, Left)
  2. Wait (0.5 sec)
  3. Show Portrait (Bob, surprised, Right, SlideFromRight)
- Alice: "Look! There's Bob!"

#### 7️⃣ **Bob Greeting**
- **Command**: Set Variable (metBob = True)
- Bob: "Alice! What a surprise!"

#### 8️⃣ **Introductions**
- Alice sizi Bob-a təqdim edir
- Dostluq başlayır

#### 9️⃣ **Happy Ending** (qəbul etdinizsə)
- Tip: End
- **Commands:**
  - Hide Portrait (Alice)
  - Hide Portrait (Bob)
- Mətn: "You made two wonderful friends today!"
- 🎉 Xoşbəxt son

#### 🔟 **Sad Ending** (rədd etdinizsə)
- Tip: End
- **Command**: Hide Portrait (Alice)
- Mətn: "Sometimes we miss out on great opportunities..."
- 😢 Kədərli son

---

## 🎨 Əlaqələr (Connections):

```
Start (1)
    ↓
Meet Alice (2)
    ↓
Response Choice (3)
    ├─→ Accept (4) ──→ At Park (6) ──→ Bob Greeting (7) ──→ Introductions (8) ──→ Happy Ending (9)
    └─→ Decline (5) ──→ Sad Ending (10)
```

---

## 🎯 Bu Demo-dan Nə Öyrənə Bilərsən?

### 1. **Character System**
- Necə personaj yaradılır
- Portrait-lərin necə işlədiyini
- Rəng sistemini

### 2. **Portrait Commands**
- `ShowPortraitCommand` - Portreti göstərmək
- `HidePortraitCommand` - Portreti gizlətmək
- Position sistemini (Left/Center/Right)
- Animation növlərini (FadeIn, SlideFromRight)

### 3. **Variables**
- Boolean dəyişənlər (True/False)
- Integer dəyişənlər (rəqəmlər)
- Variable-ları necə dəyişmək (Set, Add)

### 4. **Multiple Commands**
- Bir node-da bir neçə command
- Wait command ilə gecikdirmə
- Sequential execution (ardıcıllıq)

### 5. **Branching Story**
- Seçimlərin nəticələrə təsiri
- 2 fərqli son (Happy vs Sad)
- Conditional flow

### 6. **Multi-character Scenes**
- Eyni anda 2 personaj (Alice + Bob)
- Position sistemindən istifadə
- Character giriş animasiyası

---

## 💡 İPUCLARI

### Necə test edək?

1. **Play Preview** basın
2. Hekayəni oxuyun
3. **2 dəfə oynayın:**
   - İlk dəfə: "Sure! I'd love to." seçin → Happy Ending
   - İkinci dəfə: "Sorry, I'm busy." seçin → Sad Ending

### Necə dəyişdirək?

1. **Node-lara sağ klik** edin, "Set as Start Node" seçin (fərqli nöqtədən başlatmaq üçün)
2. **Dialogue Text-ləri dəyişin** (sağ paneldə)
3. **Yeni node-lar əlavə edin** (+ Add Node)
4. **Əlaqələr yaradın** (sağdakı port düyməsindən sürükləyin)

### Portrait şəkilləri əlavə etmək:

**Not:** Bu demo-da portrait şəkilləri boşdur (`ImagePath = ""`)

Şəkil əlavə etmək üçün:
1. Save edin (JSON faylı yaranır)
2. JSON-u text editor-da açın
3. Portrait-lərin `imagePath` sahəsinə şəkil yolunu yazın:

```json
{
  "name": "happy",
  "imagePath": "C:/Users/YourName/Pictures/alice_happy.png"
}
```

4. Load edin yenidən
5. Play Preview - portretlər görünəcək!

**Gələcək versiya:** UI-da birbaşa şəkil yükləmə olacaq.

---

## 🚀 Demo-dan Öyrənənlərlə Nə Edə Bilərsən?

### Öz hekayəni yaz:
1. Bu demo-nu əsas kimi götür
2. Node-ları dəyişdir
3. Yeni personajlar əlavə et
4. Daha çox seçimlər yarat
5. Uzun hekayə qur!

### Template kimi istifadə et:
- Node strukturlarını kopyala
- Command kombinasiyalarını istifadə et
- Variable məntiqini öz layihənə tətbiq et

---

## 📊 STATİSTİKA

| Element | Sayı |
|---------|------|
| Personajlar | 2 (Alice, Bob) |
| Portretlər | 6 (3 hər personaj üçün) |
| Node-lar | 10 |
| Əlaqələr | 9 |
| Dəyişənlər | 2 |
| Sonlar | 2 (Happy, Sad) |
| Commands | 12 |

---

## 🎓 SONRAKı ADDIMLAR

Bu demo-nu başa düşdünüz?

1. ✅ Öz personajlarınızı yaradın
2. ✅ Portrait şəkilləri əlavə edin
3. ✅ Daha uzun hekayə yazın
4. ✅ Camera effects əlavə edin (gələcək)
5. ✅ Audio əlavə edin

---

## ❓ SUALLAR?

### "Portrait şəkilləri görünmür!"
→ ImagePath boşdur. Şəkil yolu əlavə edin (yuxarıda göstərildiyi kimi)

### "Node-ları necə hərəkət etdirim?"
→ Node-un başlığına (header) basıb sürükləyin

### "Yeni node necə əlavə edim?"
→ Sol paneldə "+ Add Node" düyməsi

### "Əlaqələr necə yaradılır?"
→ Node-un sağ tərəfindəki kiçik dairəni sürükləyin

---

**Uğurlar! Bu demo ilə tez öyrənəcəksiz! 🎉**

_Sample Story created: December 2024_
