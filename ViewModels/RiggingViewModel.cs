using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using SpriteEditor.Data; // Yaratdığımız Data modelləri üçün
using System.Text.Json; // JSON Serializasiyası üçün
using TriangleNet.Geometry; // Triangle.NET üçün
using TriangleNet.Meshing;




namespace SpriteEditor.ViewModels
{
    public enum RiggingToolMode
    {
        Edit,
        CreateJoint,
        Pose,
        EditMesh,
    }

    public partial class RiggingViewModel : ObservableObject
    {
        public event EventHandler RequestRedraw;
        public event EventHandler RequestCenterCamera;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadImageCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveRigCommand))]
        [NotifyCanExecuteChangedFor(nameof(LoadRigCommand))]
        private bool _isImageLoaded = false;

        [ObservableProperty]
        private SKBitmap _loadedBitmap;

        [ObservableProperty]
        private RiggingToolMode _currentTool = RiggingToolMode.Edit;

        // === SKELET MƏLUMATLARI ===
        public ObservableCollection<JointModel> Joints { get; } = new ObservableCollection<JointModel>();

        [ObservableProperty]
        private JointModel _selectedJoint;

        // === MESH MƏLUMATLARI ===
        public ObservableCollection<VertexModel> Vertices { get; } = new ObservableCollection<VertexModel>();
        public ObservableCollection<TriangleModel> Triangles { get; } = new ObservableCollection<TriangleModel>();

        [ObservableProperty]
        private VertexModel _selectedVertex;

        public ObservableCollection<VertexModel> VertexSelectionForTriangle { get; } = new ObservableCollection<VertexModel>();

        [ObservableProperty]
        private SKPoint _currentMousePosition;

        private int _jointIdCounter = 0;
        private int _vertexIdCounter = 0;
        private string _loadedImagePath;

        // === KAMERA VƏZİYYƏTİ ===
        public SKPoint CameraOffset { get; private set; } = SKPoint.Empty;
        public float CameraScale { get; private set; } = 1.0f;

        private bool _isPanning = false;
        private SKPoint _lastPanPosition;

        // === SÜRÜKLƏMƏ VƏZİYYƏTLƏRİ ===
        private bool _isDraggingJoint = false;
        private SKPoint _dragOffset;
        private bool _isDraggingVertex = false;
        private SKPoint _vertexDragOffset;

        // === YENİ (PLAN 3 - FINAL): "SAKİT VƏZİYYƏT" (BIND POSE) YADDAŞI ===
        private Dictionary<int, SKPoint> _jointBindPositions = new Dictionary<int, SKPoint>();
        private Dictionary<int, float> _jointBindRotations = new Dictionary<int, float>();
        // ==============================================================

        private const float EPS = 1e-4f;


        // === YENİ (PLAN 3): AVTO AĞIRLIQLANDIRMA ƏMRİ ===
        [RelayCommand(CanExecute = nameof(CanAutoWeight))]
        private void AutoWeight()
        {
            if (!CanAutoWeight()) return;

            MessageBox.Show("Avtomatik ağırlıqlandırma başlayır...");

            // Hər bir nöqtə (vertex) üçün...
            foreach (var vertex in Vertices)
            {
                vertex.Weights.Clear();
                var weights = new Dictionary<int, float>();
                float totalInverseDistanceSquared = 0;

                // Hər bir sümüyə (joint) olan məsafəni yoxla
                foreach (var joint in Joints)
                {
                    // Ağırlıqlandırma nöqtənin BindPosition-u (sakit) ilə
                    // sümüyün Position-u (sakit) arasında hesablanmalıdır.
                    float dx = vertex.BindPosition.X - joint.Position.X;
                    float dy = vertex.BindPosition.Y - joint.Position.Y;
                    float distanceSq = dx * dx + dy * dy;

                    // Əgər nöqtə sümüyün tam üstündədirsə
                    if (distanceSq < EPS)
                    {
                        weights.Clear(); // Bütün digər təsirləri ləğv et
                        weights.Add(joint.Id, 1.0f);
                        totalInverseDistanceSquared = 1.0f;
                        break; // Bu nöqtə üçün başqa sümük axtarma
                    }

                    // Məsafənin tərs kvadratı (daha kəskin təsir)
                    float inverseDistSq = 1.0f / distanceSq;
                    weights.Add(joint.Id, inverseDistSq);
                    totalInverseDistanceSquared += inverseDistSq;
                }

                // Nəticələri normallaşdır (cəmi 1.0 olsun)
                if (totalInverseDistanceSquared > 0 && weights.Count > 0)
                {
                    foreach (var jointId in weights.Keys.ToList())
                    {
                        float normalizedWeight = weights[jointId] / totalInverseDistanceSquared;

                        // Çox kiçik təsirləri yadda saxlamamaq üçün filtr
                        if (normalizedWeight > 0.01f)
                        {
                            vertex.Weights[jointId] = normalizedWeight;
                        }
                    }
                }
            }

            // Nəticəni yadda saxlaya bilmək üçün
            SaveRigCommand.NotifyCanExecuteChanged();
            MessageBox.Show("Avtomatik ağırlıqlandırma tamamlandı! Nəticəni yadda saxlaya bilərsiniz.");
        }

        private bool CanAutoWeight()
        {
            // Yalnız həm sümük, həm də nöqtə varsa işləsin
            return Joints.Count > 0 && Vertices.Count > 0;
        }

        // =======================================================


        [RelayCommand]
        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Görüntü Faylları (*.png)|*.png|Bütün Fayllar (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    _loadedImagePath = openDialog.FileName;
                    byte[] fileBytes = File.ReadAllBytes(_loadedImagePath);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        LoadedBitmap = SKBitmap.Decode(ms);
                    }
                    if (LoadedBitmap == null)
                    {
                        throw new Exception("Fayl formatı dəstəklənmir və ya fayl zədəlidir.");
                    }
                    IsImageLoaded = true;
                    ClearRiggingData();
                    ResetCamera();
                    RequestCenterCamera?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Şəkli yükləyərkən xəta baş verdi: {ex.Message}", "Xəta");
                    IsImageLoaded = false;
                    LoadedBitmap = null;
                    _loadedImagePath = null;
                    ClearRiggingData();
                    ResetCamera();
                }
            }
        }


        // === Skeleti Yüklə (PLAN 3 - MESH ƏLAVƏ EDİLDİ) ===
        [RelayCommand(CanExecute = nameof(CanLoadRig))]
        private async Task LoadRigAsync()
        {
            if (!CanLoadRig()) return;

            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Rig JSON Faylı (*.rig.json)|*.rig.json|Bütün Fayllar (*.*)|*.*",
                Title = "Skelet Məlumatını Yüklə"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string jsonString = await File.ReadAllTextAsync(openDialog.FileName);
                    var rigData = JsonSerializer.Deserialize<RigData>(jsonString);

                    if (rigData == null)
                    {
                        throw new Exception("JSON faylının strukturu düzgün deyil.");
                    }

                    string jsonImageName = rigData.ImageFileName;
                    string currentImageName = Path.GetFileName(_loadedImagePath);
                    if (jsonImageName != currentImageName)
                    {
                        var result = MessageBox.Show(
                            $"Bu skelet faylı ('{jsonImageName}') yüklənmiş şəkildən ('{currentImageName}') fərqli bir şəkil üçün yaradılıb.\n\nDavam etmək istəyirsinizmi?",
                            "Xəbərdarlıq", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                            return;
                    }

                    ClearRiggingData();

                    var jointMap = new Dictionary<int, JointModel>();
                    if (rigData.Joints != null)
                    {
                        foreach (var jointData in rigData.Joints)
                        {
                            var newJoint = new JointModel(jointData.Id, jointData.Position, null)
                            {
                                BoneLength = jointData.BoneLength,
                                Rotation = jointData.Rotation
                            };

                            if (!string.IsNullOrEmpty(jointData.Name))
                            {
                                newJoint.Name = jointData.Name;
                            }
                            Joints.Add(newJoint);
                            jointMap.Add(newJoint.Id, newJoint);
                        }
                        foreach (var jointData in rigData.Joints)
                        {
                            if (jointData.ParentId != -1)
                            {
                                if (jointMap.TryGetValue(jointData.Id, out JointModel currentJoint) &&
                                    jointMap.TryGetValue(jointData.ParentId, out JointModel parentJoint))
                                {
                                    currentJoint.Parent = parentJoint;
                                }
                            }
                        }
                        if (Joints.Count > 0)
                        {
                            _jointIdCounter = Joints.Max(j => j.Id) + 1;
                        }
                        RecomputeBoneParamsFromPositions();
                    }

                    var vertexMap = new Dictionary<int, VertexModel>();
                    if (rigData.Mesh != null)
                    {
                        foreach (var vertexData in rigData.Mesh.Vertices)
                        {
                            var newVertex = new VertexModel(vertexData.Id, vertexData.Position)
                            {
                                Weights = vertexData.Weights
                            };
                            Vertices.Add(newVertex);
                            vertexMap.Add(newVertex.Id, newVertex);
                        }
                        foreach (var triangleData in rigData.Mesh.Triangles)
                        {
                            if (vertexMap.TryGetValue(triangleData.V1, out var v1) &&
                                vertexMap.TryGetValue(triangleData.V2, out var v2) &&
                                vertexMap.TryGetValue(triangleData.V3, out var v3))
                            {
                                Triangles.Add(new TriangleModel(v1, v2, v3));
                            }
                        }
                        if (Vertices.Count > 0)
                        {
                            _vertexIdCounter = Vertices.Max(v => v.Id) + 1;
                        }
                    }

                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                    SaveRigCommand.NotifyCanExecuteChanged();
                    AutoWeightCommand.NotifyCanExecuteChanged();
                    MessageBox.Show("Skelet və Mesh uğurla yükləndi.", "Uğurlu");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yükləyərkən xəta baş verdi: {ex.Message}", "Xəta");
                    ClearRiggingData();
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
        }



        private bool CanLoadRig()
        {
            return IsImageLoaded;
        }


        [RelayCommand(CanExecute = nameof(CanSaveRig))]
        private async Task SaveRigAsync()
        {
            if (!CanSaveRig()) return;
            var rigData = new RigData { ImageFileName = Path.GetFileName(_loadedImagePath) };
            foreach (var joint in Joints)
            {
                rigData.Joints.Add(new JointData
                {
                    Id = joint.Id,
                    Position = joint.Position, // Həmişə "sakit" vəziyyəti saxla
                    ParentId = joint.Parent?.Id ?? -1,
                    BoneLength = joint.BoneLength,
                    Rotation = joint.Rotation, // Bu, "sakit" vəziyyətdəki bucaqdır
                    Name = joint.Name
                });
            }
            foreach (var vertex in Vertices)
            {
                rigData.Mesh.Vertices.Add(new VertexData
                {
                    Id = vertex.Id,
                    Position = vertex.BindPosition,
                    Weights = vertex.Weights
                });
            }
            foreach (var triangle in Triangles)
            {
                rigData.Mesh.Triangles.Add(new TriangleData
                {
                    V1 = triangle.V1.Id,
                    V2 = triangle.V2.Id,
                    V3 = triangle.V3.Id
                });
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                FileName = $"{Path.GetFileNameWithoutExtension(_loadedImagePath)}.rig.json",
                Filter = "Rig JSON Faylı (*.rig.json)|*.rig.json|Bütün Fayllar (*.*)|*.*",
                Title = "Skelet Məlumatını Saxla"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(rigData, options);
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);
                    MessageBox.Show("Skelet və Mesh uğurla yadda saxlandı!", "Uğurlu");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yadda saxlayarkən xəta baş verdi: {ex.Message}", "Xəta");
                }
            }
        }


        private bool CanSaveRig()
        {
            return IsImageLoaded && (Joints.Count > 0 || Vertices.Count > 0);
        }

        #region Camera Controls
        public void ResetCamera()
        {
            CameraOffset = SKPoint.Empty;
            CameraScale = 1.0f;
        }
        public void CenterCamera(float canvasWidth, float canvasHeight, bool forceRecenter = false)
        {
            if (LoadedBitmap == null) return;
            if (forceRecenter || (CameraScale == 1.0f && CameraOffset == SKPoint.Empty))
            {
                float offsetX = (canvasWidth - (LoadedBitmap.Width * CameraScale)) / 2;
                float offsetY = (canvasHeight - (LoadedBitmap.Height * CameraScale)) / 2;
                CameraOffset = new SKPoint(offsetX, offsetY);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }
        public SKPoint ScreenToWorld(SKPoint screenPoint)
        {
            return new SKPoint(
                (screenPoint.X - CameraOffset.X) / CameraScale,
                (screenPoint.Y - CameraOffset.Y) / CameraScale
            );
        }
        public SKPoint WorldToScreen(SKPoint worldPoint)
        {
            return new SKPoint(
                (worldPoint.X * CameraScale) + CameraOffset.X,
                (worldPoint.Y * CameraScale) + CameraOffset.Y
            );
        }
        public void StartPan(SKPoint screenPos)
        {
            _isPanning = true;
            _lastPanPosition = screenPos;
        }
        public void StopPan()
        {
            _isPanning = false;
        }
        public void HandleZoom(SKPoint screenPos, int delta)
        {
            float zoomFactor = 1.1f;
            float newScale;
            if (delta > 0) newScale = CameraScale * zoomFactor;
            else newScale = CameraScale / zoomFactor;
            newScale = Math.Max(0.1f, Math.Min(newScale, 10.0f));
            if (Math.Abs(newScale - CameraScale) < 0.001f) return;
            SKPoint worldPosBefore = ScreenToWorld(screenPos);
            CameraScale = newScale;
            CameraOffset = new SKPoint(
                screenPos.X - (worldPosBefore.X * CameraScale),
                screenPos.Y - (worldPosBefore.Y * CameraScale)
            );
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        // === OnCanvasLeftClicked (YENİLƏNMİŞ - Ctrl+Klik Məntiqi) ===
        public void OnCanvasLeftClicked(SKPoint screenPos, bool isCtrlPressed)
        {
            SKPoint worldPos = ScreenToWorld(screenPos);
            float clickRadiusScreen = 10f;
            float clickRadiusWorld = clickRadiusScreen / CameraScale;
            float clickRadiusSq = clickRadiusWorld * clickRadiusWorld;

            if (CurrentTool == RiggingToolMode.CreateJoint)
            {
                var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
                if (newJoint.Parent != null)
                {
                    float dx = newJoint.Position.X - newJoint.Parent.Position.X;
                    float dy = newJoint.Position.Y - newJoint.Parent.Position.Y;
                    float len = MathF.Sqrt(dx * dx + dy * dy);
                    newJoint.BoneLength = (len < EPS) ? 0f : len;
                    newJoint.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);
                }
                AddJoint(newJoint); // DƏYİŞİKLİK
                SelectedJoint = newJoint;
                SaveRigCommand.NotifyCanExecuteChanged();
            }
            else if (CurrentTool == RiggingToolMode.Edit || CurrentTool == RiggingToolMode.Pose)
            {
                JointModel closestJoint = FindClosestJoint(worldPos, clickRadiusSq);
                SelectedJoint = closestJoint;
                if (SelectedJoint != null)
                {
                    _isDraggingJoint = true;
                    _dragOffset = SelectedJoint.Position - worldPos;
                }
            }
            else if (CurrentTool == RiggingToolMode.EditMesh)
            {
                VertexModel closestVertex = FindClosestVertex(worldPos, clickRadiusSq);

                if (isCtrlPressed)
                {
                    if (closestVertex != null)
                    {
                        if (VertexSelectionForTriangle.Contains(closestVertex))
                        {
                            VertexSelectionForTriangle.Remove(closestVertex);
                        }
                        else
                        {
                            VertexSelectionForTriangle.Add(closestVertex);
                        }
                        SelectedVertex = closestVertex;
                        if (VertexSelectionForTriangle.Count == 3)
                        {
                            var v1 = VertexSelectionForTriangle[0];
                            var v2 = VertexSelectionForTriangle[1];
                            var v3 = VertexSelectionForTriangle[2];
                            if (!TriangleExists(v1, v2, v3))
                            {
                                Triangles.Add(new TriangleModel(v1, v2, v3));
                                SaveRigCommand.NotifyCanExecuteChanged();
                            }
                            VertexSelectionForTriangle.Clear();
                            SelectedVertex = null;
                        }
                    }
                }
                else
                {
                    VertexSelectionForTriangle.Clear();
                    if (closestVertex != null)
                    {
                        SelectedVertex = closestVertex;
                        _isDraggingVertex = true;
                        _vertexDragOffset = SelectedVertex.BindPosition - worldPos;
                    }
                    else
                    {
                        var newVertex = new VertexModel(_vertexIdCounter++, worldPos);
                        AddVertex(newVertex); // DƏYİŞİKLİK
                        SelectedVertex = newVertex;
                        _isDraggingVertex = true;
                        _vertexDragOffset = SKPoint.Empty;
                        SaveRigCommand.NotifyCanExecuteChanged();
                    }
                }
            }

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        // === OnCanvasMouseMoved (YENİLƏNMİŞ - DEFORMASIYA ƏLAVƏ EDİLDİ) ===
        public void OnCanvasMouseMoved(SKPoint screenPos, bool isCtrlPressed)
        {
            if (_isPanning)
            {
                SKPoint delta = new SKPoint(screenPos.X - _lastPanPosition.X, screenPos.Y - _lastPanPosition.Y);
                CameraOffset = new SKPoint(CameraOffset.X + delta.X, CameraOffset.Y + delta.Y);
                _lastPanPosition = screenPos;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
                return;
            }

            SKPoint worldPos = ScreenToWorld(screenPos);
            CurrentMousePosition = worldPos;

            if (isCtrlPressed)
            {
                _isDraggingJoint = false;
                _isDraggingVertex = false;
            }

            if (_isDraggingJoint && SelectedJoint != null)
            {
                if (CurrentTool == RiggingToolMode.Edit)
                {
                    // Edit rejimində "sakit" vəziyyəti (Bind Pose) redaktə edirik
                    SelectedJoint.Position = worldPos + _dragOffset;
                    RecomputeBoneParamsFromPositions(); // "Sakit" bucaq/uzunluqları yenilə
                }
                else if (CurrentTool == RiggingToolMode.Pose)
                {
                    if (SelectedJoint.Parent == null)
                    {
                        SKPoint newJointPos = worldPos + _dragOffset;
                        SKPoint delta = newJointPos - SelectedJoint.Position;
                        ApplyRecursiveMove(SelectedJoint, delta);
                    }
                    else
                    {
                        SKPoint parentPos = SelectedJoint.Parent.Position;
                        float newRotation = MathF.Atan2(worldPos.Y - parentPos.Y, worldPos.X - parentPos.X);
                        float rotationDelta = newRotation - SelectedJoint.Rotation;
                        UpdatePoseHierarchy(SelectedJoint, rotationDelta);
                    }

                    // === YENİ (PLAN 3 - FINAL): DEFORMASIYANI ÇAĞIR ===
                    DeformMesh();
                    // ===============================================
                }
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (_isDraggingVertex && SelectedVertex != null && CurrentTool == RiggingToolMode.EditMesh)
            {
                // EditMesh rejimində biz BindPosition-u (əsas mövqe) redaktə edirik
                SKPoint newVertexPos = worldPos + _vertexDragOffset;
                SelectedVertex.BindPosition = newVertexPos;
                SelectedVertex.CurrentPosition = newVertexPos; // Sakit vəziyyətdə bərabərdirlər
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (CurrentTool == RiggingToolMode.CreateJoint && SelectedJoint != null)
            {
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        #region Find Helpers
        private JointModel FindClosestJoint(SKPoint worldPos, float radiusSq)
        {
            JointModel closestJoint = null;
            float minDistanceSq = radiusSq;
            foreach (var joint in Joints)
            {
                float dx = worldPos.X - joint.Position.X;
                float dy = worldPos.Y - joint.Position.Y;
                float distanceSq = (dx * dx) + (dy * dy);
                if (distanceSq < minDistanceSq)
                {
                    minDistanceSq = distanceSq;
                    closestJoint = joint;
                }
            }
            return closestJoint;
        }
        private VertexModel FindClosestVertex(SKPoint worldPos, float radiusSq)
        {
            VertexModel closestVertex = null;
            float minDistanceSq = radiusSq;
            foreach (var vertex in Vertices)
            {
                float dx = worldPos.X - vertex.BindPosition.X;
                float dy = worldPos.Y - vertex.BindPosition.Y;
                float distanceSq = (dx * dx) + (dy * dy);
                if (distanceSq < minDistanceSq)
                {
                    minDistanceSq = distanceSq;
                    closestVertex = vertex;
                }
            }
            return closestVertex;
        }
        private bool TriangleExists(VertexModel v1, VertexModel v2, VertexModel v3)
        {
            var idSet = new HashSet<int> { v1.Id, v2.Id, v3.Id };
            foreach (var triangle in Triangles)
            {
                var existingSet = new HashSet<int> { triangle.V1.Id, triangle.V2.Id, triangle.V3.Id };
                if (idSet.SetEquals(existingSet))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Rigging Helpers
        public void RecomputeBoneParamsFromPositions()
        {
            foreach (var j in Joints)
            {
                if (j.Parent == null)
                {
                    j.BoneLength = 0f;
                    j.Rotation = 0f;
                    continue;
                }
                float dx = j.Position.X - j.Parent.Position.X;
                float dy = j.Position.Y - j.Parent.Position.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                j.BoneLength = (len < EPS) ? 0f : len;
                j.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);
            }
        }
        #endregion

        #region Deformation
        private void ApplyRecursiveMove(JointModel joint, SKPoint delta)
        {
            if (joint == null) return;
            joint.Position += delta;
            foreach (var child in Joints.Where(j => j.Parent == joint).ToList())
            {
                ApplyRecursiveMove(child, delta);
            }
        }
        private void UpdatePoseHierarchy(JointModel joint, float rotationDelta)
        {
            if (joint == null) return;
            joint.Rotation += rotationDelta;
            if (joint.Parent != null)
            {
                if (joint.BoneLength > EPS)
                {
                    joint.Position = new SKPoint(
                        joint.Parent.Position.X + MathF.Cos(joint.Rotation) * joint.BoneLength,
                        joint.Parent.Position.Y + MathF.Sin(joint.Rotation) * joint.BoneLength
                    );
                }
                else
                {
                    joint.Position = joint.Parent.Position;
                }
            }
            foreach (var child in Joints.Where(j => j.Parent == joint).ToList())
            {
                UpdatePoseHierarchy(child, rotationDelta);
            }
        }

        // === YENİ (PLAN 3 - FINAL): ƏSAS DEFORMASİYA METODU (XƏTA DÜZƏLDİLİB) ===
        private void DeformMesh()
        {
            var jointMap = Joints.ToDictionary(j => j.Id);

            foreach (var vertex in Vertices)
            {
                SKPoint finalDeformedPos = SKPoint.Empty;
                float totalWeight = 0;

                foreach (var weight in vertex.Weights)
                {
                    int jointId = weight.Key;
                    float w = weight.Value;

                    if (!jointMap.TryGetValue(jointId, out var joint) ||
                        !_jointBindPositions.TryGetValue(jointId, out var bindPos) ||
                        !_jointBindRotations.TryGetValue(jointId, out var bindRot))
                    {
                        continue;
                    }

                    SKPoint posedPos = joint.Position;
                    float posedRot = joint.Rotation;

                    // 1. Nöqtənin sümüyün "sakit" mövqeyinə görə nisbi yerini tap
                    SKPoint vRel = vertex.BindPosition - bindPos;

                    // 2. Sümüyün nə qədər fırlandığını tap (hərəkətli - sakit)
                    float deltaRot = posedRot - bindRot;

                    // 3. Nöqtəni həmin bucaq qədər fırlat
                    float cos = MathF.Cos(deltaRot);
                    float sin = MathF.Sin(deltaRot);
                    float rotatedX = vRel.X * cos - vRel.Y * sin;
                    float rotatedY = vRel.X * sin + vRel.Y * cos;

                    // 4. Fırlanmış nöqtəni sümüyün yeni ("hərəkətli") mövqeyinə əlavə et
                    SKPoint vTransformed = new SKPoint(rotatedX, rotatedY) + posedPos;

                    // 5. Yekun mövqeyə ağırlıq dərəcəsində əlavə et
                    // === DÜZƏLİŞ 1 (Operator *) ===
                    finalDeformedPos += new SKPoint(vTransformed.X * w, vTransformed.Y * w);
                    // =============================
                    totalWeight += w;
                }

                // 6. Bütün təsirlərin ortalamasını al
                if (totalWeight > EPS)
                {
                    // === DÜZƏLİŞ 2 (Operator /) ===
                    vertex.CurrentPosition = new SKPoint(finalDeformedPos.X / totalWeight, finalDeformedPos.Y / totalWeight);
                    // =============================
                }
                else
                {
                    vertex.CurrentPosition = vertex.BindPosition;
                }
            }
        }
        // ========================================================

        #endregion


        public void OnCanvasLeftReleased()
        {
            _isDraggingJoint = false;
            _isDraggingVertex = false;
        }

        public void DeselectCurrentJoint()
        {
            if (SelectedJoint != null)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }
        public void DeselectCurrentVertex()
        {
            if (SelectedVertex != null)
            {
                SelectedVertex = null;
            }
            if (VertexSelectionForTriangle.Count > 0)
            {
                VertexSelectionForTriangle.Clear();
            }
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteSelectedJoint()
        {
            if (SelectedJoint == null) return;
            var jointToRemove = SelectedJoint;
            SelectedJoint = null;
            _isDraggingJoint = false;
            RemoveJoint(jointToRemove); // DƏYİŞİKLİK
            foreach (var joint in Joints.ToList())
            {
                if (joint.Parent == jointToRemove)
                {
                    joint.Parent = null;
                    joint.BoneLength = 0;
                    joint.Rotation = 0;
                }
            }
            foreach (var vertex in Vertices)
            {
                if (vertex.Weights.ContainsKey(jointToRemove.Id))
                {
                    vertex.Weights.Remove(jointToRemove.Id);
                }
            }
            RequestRedraw?.Invoke(this, EventArgs.Empty);
            SaveRigCommand.NotifyCanExecuteChanged();
        }

        public void DeleteSelectedVertex()
        {
            if (SelectedVertex == null) return;
            var vertexToRemove = SelectedVertex;
            SelectedVertex = null;
            _isDraggingVertex = false;
            RemoveVertex(vertexToRemove); // DƏYİŞİKLİK
            if (VertexSelectionForTriangle.Contains(vertexToRemove))
            {
                VertexSelectionForTriangle.Remove(vertexToRemove);
            }
            var trianglesToRemove = Triangles.Where(t =>
                t.V1 == vertexToRemove ||
                t.V2 == vertexToRemove ||
                t.V3 == vertexToRemove)
                .ToList();
            foreach (var triangle in trianglesToRemove)
            {
                Triangles.Remove(triangle);
            }
            RequestRedraw?.Invoke(this, EventArgs.Empty);
            SaveRigCommand.NotifyCanExecuteChanged();
        }


        private void ClearRiggingData()
        {
            Joints.Clear();
            SelectedJoint = null;
            _jointIdCounter = 0;
            Vertices.Clear();
            Triangles.Clear();
            SelectedVertex = null;
            VertexSelectionForTriangle.Clear();
            _vertexIdCounter = 0;

            _jointBindPositions.Clear();
            _jointBindRotations.Clear();

            SaveRigCommand?.NotifyCanExecuteChanged();
            AutoWeightCommand?.NotifyCanExecuteChanged();
        }

        // === YENİLƏNMİŞ (PLAN 3 - FINAL): ALƏT DƏYİŞMƏ MƏNTİQİ ===
        partial void OnCurrentToolChanged(RiggingToolMode value)
        {
            // Alət dəyişəndə köhnə vəziyyəti təmizlə
            if (_jointBindPositions.Count > 0)
            {
                ResetPoseToBindPose(); // Pose rejimindən çıxırıqsa, hər şeyi sıfırla
            }

            // Yeni rejimə keç
            if (value == RiggingToolMode.Pose)
            {
                StoreBindPose(); // Pose rejiminə giririksə, "sakit" vəziyyəti yadda saxla
            }

            // Bütün seçimləri ləğv et
            SelectedJoint = null;
            SelectedVertex = null;
            VertexSelectionForTriangle.Clear();
            RequestRedraw?.Invoke(this, EventArgs.Empty);

            _isDraggingJoint = false;
            _isDraggingVertex = false;

            AutoWeightCommand.NotifyCanExecuteChanged();
        }

        // === YENİ (PLAN 3 - FINAL): "SAKİT VƏZİYYƏT" METODLARI ===
        private void StoreBindPose()
        {
            // 1. Sümüklərin "sakit" bucaqlarını və uzunluqlarını hesabla
            RecomputeBoneParamsFromPositions();

            _jointBindPositions.Clear();
            _jointBindRotations.Clear();

            // 2. Həmin "sakit" vəziyyəti yadda saxla
            foreach (var joint in Joints)
            {
                _jointBindPositions[joint.Id] = joint.Position;
                _jointBindRotations[joint.Id] = joint.Rotation;
            }
        }

        private void ResetPoseToBindPose()
        {
            if (_jointBindPositions.Count == 0) return; // Artıq sıfırlanıbsa

            // Sümükləri "sakit" vəziyyətinə qaytar
            foreach (var joint in Joints)
            {
                if (_jointBindPositions.TryGetValue(joint.Id, out var bindPos))
                {
                    joint.Position = bindPos;
                }
            }

            // Nöqtələri (Vertices) "sakit" vəziyyətinə qaytar
            foreach (var vertex in Vertices)
            {
                vertex.CurrentPosition = vertex.BindPosition;
            }

            // Yaddaşı təmizlə
            _jointBindPositions.Clear();
            _jointBindRotations.Clear();
        }

        // === YENİ: Siyahı dəyişikliklərini izləmək üçün köməkçi metodlar ===
        private void AddJoint(JointModel newJoint)
        {
            Joints.Add(newJoint);
            AutoWeightCommand.NotifyCanExecuteChanged();
        }
        private void AddVertex(VertexModel newVertex)
        {
            Vertices.Add(newVertex);
            AutoWeightCommand.NotifyCanExecuteChanged();
            AutoTriangleCommand.NotifyCanExecuteChanged();
        }
        private void RemoveJoint(JointModel jointToRemove)
        {
            Joints.Remove(jointToRemove);
            AutoWeightCommand.NotifyCanExecuteChanged();
        }
        private void RemoveVertex(VertexModel vertexToRemove)
        {
            Vertices.Remove(vertexToRemove);
            AutoWeightCommand.NotifyCanExecuteChanged();
            AutoTriangleCommand.NotifyCanExecuteChanged();
        }




        // === YENİ (AVTO-TRİANGULATE - DÜZƏLDİLMİŞ) ===
        [RelayCommand(CanExecute = nameof(CanAutoTriangle))]
        private void AutoTriangle()
        {
            if (!CanAutoTriangle()) return;

            // 1. Köhnə üçbucaqları təmizlə
            Triangles.Clear();

            // === DÜZƏLİŞ: Sizin təklifiniz əsasında InputGeometry-ni Polygon ilə əvəz edirik ===
            // 2. Triangle.NET üçün bir "Polygon" yarat
            var polygon = new TriangleNet.Geometry.Polygon();
            // ==============================================================================

            // 3. Bizim VertexModel-lərimizi Triangle.NET-in Vertex-ləri ilə
            //    əlaqələndirmək üçün bir lüğət (map) yaradırıq. (Bu vacibdir)
            var vertexMap = new Dictionary<TriangleNet.Geometry.Vertex, VertexModel>();

            foreach (var vmVertex in Vertices)
            {
                // Bizim "BindPosition" (sakit vəziyyət) əsasında yeni nöqtə yaradırıq
                var tnVertex = new TriangleNet.Geometry.Vertex(
                    vmVertex.BindPosition.X,
                    vmVertex.BindPosition.Y
                );

                // === DÜZƏLİŞ: geometry.AddPoint(tnVertex) -> polygon.Add(tnVertex) ===
                polygon.Add(tnVertex);
                // ===================================================================

                vertexMap[tnVertex] = vmVertex; // Lüğətə əlavə et
            }

            // === DÜZƏLİŞ: geometry.Triangulate() -> polygon.Triangulate() ===
            // 4. ƏSAS MƏRHƏLƏ: Triangulate!
            var mesh = (TriangleNet.Mesh)polygon.Triangulate();
            // =============================================================

            // 5. Nəticəni (mesh.Triangles) bizim öz TriangleModel-lərimizə çeviririk
            foreach (var tnTriangle in mesh.Triangles)
            {
                // Hər üçbucağın 3 nöqtəsini (Vertex) alırıq
                var v0 = tnTriangle.GetVertex(0);
                var v1 = tnTriangle.GetVertex(1);
                var v2 = tnTriangle.GetVertex(2);

                // Lüğətdən (map) istifadə edərək bizim VertexModel-ləri tapırıq
                if (vertexMap.TryGetValue(v0, out var vmV0) &&
                    vertexMap.TryGetValue(v1, out var vmV1) &&
                    vertexMap.TryGetValue(v2, out var vmV2))
                {
                    // Yeni TriangleModel yaradıb siyahıya əlavə edirik
                    Triangles.Add(new TriangleModel(vmV0, vmV1, vmV2));
                }
            }

            // 6. UI-ı yenilə və yadda saxlama düyməsini aktiv et
            RequestRedraw?.Invoke(this, EventArgs.Empty);
            SaveRigCommand.NotifyCanExecuteChanged();
            MessageBox.Show($"{mesh.Triangles.Count} üçbucaq avtomatik yaradıldı.", "Uğurlu");
        }

        private bool CanAutoTriangle()
        {
            // Yalnız ən azı 3 nöqtə varsa işləsin
            return Vertices.Count >= 3;
        }
        // ==================================


    }
}