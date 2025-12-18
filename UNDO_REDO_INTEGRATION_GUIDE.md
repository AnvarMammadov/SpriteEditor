# Undo/Redo Integration Guide for RiggingViewModel

## 🎯 Məqsəd

RiggingViewModel-də mövcud əməliyyatlara Undo/Redo dəstəyi əlavə etmək.

---

## 📋 Dəyişdirilməli Metodlar

### 1. **OnCanvasLeftClicked** - Joint və Vertex əlavə edən hissələr

#### ƏVVƏLKİ KOD:
```csharp
// CreateJoint mode
var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
// ... bone length/rotation hesablamaları ...
AddJoint(newJoint); // ← BU SƏTRİ DƏYİŞDİRƏK
```

#### YENİ KOD:
```csharp
using SpriteEditor.Helpers.UndoRedo;

// CreateJoint mode
var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
// ... bone length/rotation hesablamaları ...
this.AddJointWithUndo(newJoint); // ← UNDO İLƏ
```

---

### 2. **OnCanvasLeftClicked** - Vertex əlavə edən hissə

#### ƏVVƏLKİ KOD:
```csharp
// EditMesh mode - new vertex
var newVertex = new VertexModel(_vertexIdCounter++, worldPos);
AddVertex(newVertex); // ← BU SƏTRİ DƏYİŞDİRƏK
```

#### YENİ KOD:
```csharp
var newVertex = new VertexModel(_vertexIdCounter++, worldPos);
this.AddVertexWithUndo(newVertex); // ← UNDO İLƏ
```

---

### 3. **DeleteSelectedJoint**

#### ƏVVƏLKİ KOD:
```csharp
public void DeleteSelectedJoint()
{
    if (SelectedJoint == null) return;
    var jointToRemove = SelectedJoint;
    SelectedJoint = null;
    _isDraggingJoint = false;
    RemoveJoint(jointToRemove); // ← BU SƏTRİ DƏYİŞDİRƏK
    // ... parent reference cleanup ...
}
```

#### YENİ KOD:
```csharp
public void DeleteSelectedJoint()
{
    if (SelectedJoint == null) return;
    var jointToRemove = SelectedJoint;
    SelectedJoint = null;
    _isDraggingJoint = false;
    this.DeleteJointWithUndo(jointToRemove); // ← UNDO İLƏ
    // ... parent reference cleanup ...
}
```

---

### 4. **DeleteSelectedVertex**

#### ƏVVƏLKİ KOD:
```csharp
public void DeleteSelectedVertex()
{
    if (SelectedVertex == null) return;
    var vertexToRemove = SelectedVertex;
    SelectedVertex = null;
    _isDraggingVertex = false;
    RemoveVertex(vertexToRemove); // ← BU SƏTRİ DƏYİŞDİRƏK
    // ... triangle cleanup ...
}
```

#### YENİ KOD:
```csharp
public void DeleteSelectedVertex()
{
    if (SelectedVertex == null) return;
    var vertexToRemove = SelectedVertex;
    SelectedVertex = null;
    _isDraggingVertex = false;
    this.DeleteVertexWithUndo(vertexToRemove); // ← UNDO İLƏ
    // ... triangle cleanup ...
}
```

---

### 5. **OnCanvasLeftReleased** - Drag bitdikdə position save

Bu daha mürəkkəbdir - drag başlayanda köhnə pozisiyanı yadda saxlamalıyıq.

#### ViewModel-ə yeni sahələr əlavə edin:
```csharp
// Drag üçün undo support
private SKPoint _jointDragStartPosition;
private SKPoint _vertexDragStartPosition;
```

#### **OnCanvasMouseMoved**-də drag başlayanda:
```csharp
if (_isDraggingJoint && SelectedJoint != null)
{
    // !! İLK DƏFƏ BAŞLAYANDA YADDA SAXLA !!
    if (_jointDragStartPosition == SKPoint.Empty)
    {
        _jointDragStartPosition = SelectedJoint.Position;
    }
    
    // ... normal drag logic ...
}
```

#### **OnCanvasLeftReleased**-də:
```csharp
public void OnCanvasLeftReleased()
{
    // Joint drag bitdi - UNDO qeyd et
    if (_isDraggingJoint && SelectedJoint != null && _jointDragStartPosition != SKPoint.Empty)
    {
        if (_jointDragStartPosition != SelectedJoint.Position)
        {
            this.MoveJointWithUndo(SelectedJoint, _jointDragStartPosition, SelectedJoint.Position);
        }
        _jointDragStartPosition = SKPoint.Empty;
    }
    
    // Vertex drag bitdi - UNDO qeyd et
    if (_isDraggingVertex && SelectedVertex != null && _vertexDragStartPosition != SKPoint.Empty)
    {
        if (_vertexDragStartPosition != SelectedVertex.BindPosition)
        {
            this.MoveVertexWithUndo(SelectedVertex, _vertexDragStartPosition, SelectedVertex.BindPosition);
        }
        _vertexDragStartPosition = SKPoint.Empty;
    }

    _isDraggingJoint = false;
    _isDraggingVertex = false;
}
```

---

### 6. **Triangle əlavə edən hissə** (OnCanvasLeftClicked-də)

#### ƏVVƏLKİ KOD:
```csharp
if (!TriangleExists(v1, v2, v3))
{
    Triangles.Add(new TriangleModel(v1, v2, v3)); // ← BU SƏTRİ DƏYİŞDİRƏK
}
```

#### YENİ KOD:
```csharp
if (!TriangleExists(v1, v2, v3))
{
    var triangle = new TriangleModel(v1, v2, v3);
    this.AddTriangleWithUndo(triangle); // ← UNDO İLƏ
}
```

---

## 🛠️ DƏYİŞİKLİKLƏRİ TƏTBIQ ETMƏK

### Addım 1: Using statement əlavə edin
```csharp
using SpriteEditor.Helpers.UndoRedo;
```

### Addım 2: Sahələr əlavə edin
```csharp
private SKPoint _jointDragStartPosition = SKPoint.Empty;
private SKPoint _vertexDragStartPosition = SKPoint.Empty;
```

### Addım 3: Yuxarıdakı dəyişiklikləri tətbiq edin

### Addım 4: Test edin!
1. Joint yaradın → **Ctrl+Z** basın → Silinməli
2. Vertex əlavə edin → **Ctrl+Z** basın → Silinməli
3. Joint sürüşdürün → **Ctrl+Z** basın → Əvvəlki yerə qayıtmalı
4. Joint silin → **Ctrl+Z** basın → Geri gəlməli

---

## ✅ ÜSTÜNLÜKLƏR

1. **Automatic merging** - Sürüşdürmə zamanı hər piksel üçün ayrı undo command yaranmır
2. **Stack limit** - 100 command limitli (memory leak yoxdur)
3. **Clean API** - Extension methodlar kodun oxunaqlığını saxlayır
4. **Thread-safe** - Singleton pattern
5. **Error handling** - Try/catch wraplənmiş

---

## 🚨 DİQQƏT

- **AutoTriangle və AutoWeight** kimi batch əməliyyatlar üçün **BatchCommand** istifadə edin
- **Load Rig** əməliyyatından sonra undo stack-i **Clear()** edin
- **New Project** başladanda da Clear() edin

---

## 📝 MİSAL FULL CODE

```csharp
// RiggingViewModel.cs - partial snippet

using SpriteEditor.Helpers.UndoRedo;

public partial class RiggingViewModel : ObservableObject
{
    // Drag undo support
    private SKPoint _jointDragStartPosition = SKPoint.Empty;
    private SKPoint _vertexDragStartPosition = SKPoint.Empty;

    public void OnCanvasLeftClicked(SKPoint screenPos, bool isCtrlPressed)
    {
        SKPoint worldPos = ScreenToWorld(screenPos);
        // ...

        if (CurrentTool == RiggingToolMode.CreateJoint)
        {
            var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
            // ... bone length/rotation ...
            this.AddJointWithUndo(newJoint); // ← UNDO
            SelectedJoint = newJoint;
        }
        else if (CurrentTool == RiggingToolMode.EditMesh)
        {
            // ... logic ...
            if (closestVertex == null)
            {
                var newVertex = new VertexModel(_vertexIdCounter++, worldPos);
                this.AddVertexWithUndo(newVertex); // ← UNDO
                SelectedVertex = newVertex;
            }
        }
    }

    public void OnCanvasMouseMoved(SKPoint screenPos, bool isCtrlPressed)
    {
        // ... existing code ...

        if (_isDraggingJoint && SelectedJoint != null)
        {
            // İlk drag başlayanda save et
            if (_jointDragStartPosition == SKPoint.Empty)
            {
                _jointDragStartPosition = SelectedJoint.Position;
            }

            // ... normal drag code ...
        }
    }

    public void OnCanvasLeftReleased()
    {
        // Joint moved - save to undo
        if (_isDraggingJoint && SelectedJoint != null && _jointDragStartPosition != SKPoint.Empty)
        {
            if (_jointDragStartPosition != SelectedJoint.Position)
            {
                this.MoveJointWithUndo(SelectedJoint, _jointDragStartPosition, SelectedJoint.Position);
            }
            _jointDragStartPosition = SKPoint.Empty;
        }

        // Vertex moved - save to undo
        if (_isDraggingVertex && SelectedVertex != null && _vertexDragStartPosition != SKPoint.Empty)
        {
            if (_vertexDragStartPosition != SelectedVertex.BindPosition)
            {
                this.MoveVertexWithUndo(SelectedVertex, _vertexDragStartPosition, SelectedVertex.BindPosition);
            }
            _vertexDragStartPosition = SKPoint.Empty;
        }

        _isDraggingJoint = false;
        _isDraggingVertex = false;
    }

    public void DeleteSelectedJoint()
    {
        if (SelectedJoint == null) return;
        var jointToRemove = SelectedJoint;
        SelectedJoint = null;
        
        this.DeleteJointWithUndo(jointToRemove); // ← UNDO
        
        // ... parent cleanup ...
    }
}
```

---

**Test etmək üçün**: Proqramı run edin, Rigging moduluna gedin və **Ctrl+Z / Ctrl+Y** test edin! 🎉

