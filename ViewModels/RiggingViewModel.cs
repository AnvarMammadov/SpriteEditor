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



namespace SpriteEditor.ViewModels
{
    public enum RiggingToolMode
    {
        Edit, // "None" adını "Edit" olaraq dəyişdirdik
        CreateJoint,
        Pose  // Yeni hərəkət etdirmə rejimimiz
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

        public ObservableCollection<JointModel> Joints { get; } = new ObservableCollection<JointModel>();

        [ObservableProperty]
        private JointModel _selectedJoint;

        [ObservableProperty]
        private SKPoint _currentMousePosition;

        private int _jointIdCounter = 0;
        private string _loadedImagePath;

        // === KAMERA VƏZİYYƏTİ ((P*S)+O MODELİ) ===
        public SKPoint CameraOffset { get; private set; } = SKPoint.Empty;
        public float CameraScale { get; private set; } = 1.0f;

        private bool _isPanning = false;
        private SKPoint _lastPanPosition;

        // === Oynaq sürükləmə vəziyyəti ===
        private bool _isDraggingJoint = false;
        private SKPoint _dragOffset;

        // === YENİ: Stabillik üçün Epsilon ===
        private const float EPS = 1e-4f;


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


        // === Skeleti Yüklə (DÜZƏLİŞLİ) ===
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

                    if (rigData == null || rigData.Joints == null)
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

                    // Pass 1: Bütün JointModel-ləri yarat
                    foreach (var jointData in rigData.Joints)
                    {
                        // BoneLength və Rotation da yüklənir
                        var newJoint = new JointModel(jointData.Id, jointData.Position, null)
                        {
                            BoneLength = jointData.BoneLength,
                            Rotation = jointData.Rotation
                        };
                        Joints.Add(newJoint);
                        jointMap.Add(newJoint.Id, newJoint);
                    }

                    // Pass 2: Parent referanslarını təyin et
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

                    // === YENİ (HƏLL A): Parametrləri mövqelərə görə yenidən sinxronlaşdır ===
                    RecomputeBoneParamsFromPositions();
                    // ===================================================================

                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                    SaveRigCommand.NotifyCanExecuteChanged();

                    MessageBox.Show("Skelet uğurla yükləndi.", "Uğurlu");
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




        // === Skeleti Yadda Saxla (Dəyişməyib) ===
        [RelayCommand(CanExecute = nameof(CanSaveRig))]
        private async Task SaveRigAsync()
        {
            if (!CanSaveRig()) return;

            var rigData = new RigData
            {
                ImageFileName = Path.GetFileName(_loadedImagePath)
            };

            foreach (var joint in Joints)
            {
                rigData.Joints.Add(new JointData
                {
                    Id = joint.Id,
                    Position = joint.Position,
                    ParentId = joint.Parent?.Id ?? -1,
                    BoneLength = joint.BoneLength,
                    Rotation = joint.Rotation
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
                    MessageBox.Show("Skelet uğurla yadda saxlandı!", "Uğurlu");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yadda saxlayarkən xəta baş verdi: {ex.Message}", "Xəta");
                }
            }
        }


        private bool CanSaveRig()
        {
            return IsImageLoaded && Joints.Count > 0;
        }

        // === Kamera Metodları (Dəyişməyib) ===
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

            if (delta > 0)
                newScale = CameraScale * zoomFactor;
            else
                newScale = CameraScale / zoomFactor;

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

        // === OnCanvasLeftClicked (Dəyişməyib) ===
        public void OnCanvasLeftClicked(SKPoint screenPos)
        {
            SKPoint worldPos = ScreenToWorld(screenPos);

            if (CurrentTool == RiggingToolMode.CreateJoint)
            {
                var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);

                // Sümük uzunluğunu və bucağını dərhal hesabla
                if (newJoint.Parent != null)
                {
                    float dx = newJoint.Position.X - newJoint.Parent.Position.X;
                    float dy = newJoint.Position.Y - newJoint.Parent.Position.Y;

                    float len = MathF.Sqrt(dx * dx + dy * dy);
                    newJoint.BoneLength = (len < EPS) ? 0f : len;
                    newJoint.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);
                }

                Joints.Add(newJoint);
                SelectedJoint = newJoint;
                RequestRedraw?.Invoke(this, EventArgs.Empty);

                SaveRigCommand.NotifyCanExecuteChanged();
            }
            else if (CurrentTool == RiggingToolMode.Edit || CurrentTool == RiggingToolMode.Pose)
            {
                JointModel closestJoint = null;
                float minDistanceSq = float.MaxValue;
                float clickRadiusScreen = 10f;
                float clickRadiusWorld = clickRadiusScreen / CameraScale;
                float clickRadiusSq = clickRadiusWorld * clickRadiusWorld;

                foreach (var joint in Joints)
                {
                    float dx = worldPos.X - joint.Position.X;
                    float dy = worldPos.Y - joint.Position.Y;
                    float distanceSq = (dx * dx) + (dy * dy);

                    if (distanceSq < clickRadiusSq && distanceSq < minDistanceSq)
                    {
                        minDistanceSq = distanceSq;
                        closestJoint = joint;
                    }
                }

                SelectedJoint = closestJoint;
                RequestRedraw?.Invoke(this, EventArgs.Empty);

                if (SelectedJoint != null)
                {
                    _isDraggingJoint = true;
                    _dragOffset = SelectedJoint.Position - worldPos;
                }
            }
        }

        // === OnCanvasMouseMoved (DÜZƏLİŞLİ) ===
        public void OnCanvasMouseMoved(SKPoint screenPos)
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

            if (_isDraggingJoint && SelectedJoint != null)
            {
                if (CurrentTool == RiggingToolMode.Edit)
                {
                    // EDIT REJİMİ: Yalnız seçilmiş oynağı tərpət
                    SelectedJoint.Position = worldPos + _dragOffset;

                    // Edit rejimində sümüklərin parametrlərini də yeniləyək
                    RecomputeBoneParamsFromPositions();
                }
                else if (CurrentTool == RiggingToolMode.Pose)
                {
                    if (SelectedJoint.Parent == null)
                    {
                        // 1. ROOT sümüyüdür (sürüşdürmə)
                        SKPoint newJointPos = worldPos + _dragOffset;
                        SKPoint delta = newJointPos - SelectedJoint.Position;
                        ApplyRecursiveMove(SelectedJoint, delta);
                    }
                    else
                    {
                        // 2. ÖVLAD sümüyüdür (fırlatma)

                        // === YENİ (HƏLL B): Fırlatmazdan öncə alt zənciri sinxronlaşdır ===
                        // Yalnız bütün ağacı yox, ana sümükdən başlayan zənciri yeniləyirik
                        RecomputeSubtree(SelectedJoint.Parent);
                        // =============================================================

                        SKPoint parentPos = SelectedJoint.Parent.Position;

                        // === YENİ (HƏLL B): Atan2 DÜZƏLİŞİ (X, Y yox, Y, X olmalıdır) ===
                        // Səhv: MathF.Atan2(worldPos.Y - parentPos.Y, worldPos.X - parentPos.Y);
                        // Düzgün:
                        float newRotation = MathF.Atan2(worldPos.Y - parentPos.Y, worldPos.X - parentPos.X);
                        // =============================================================

                        float rotationDelta = newRotation - SelectedJoint.Rotation;

                        UpdatePoseHierarchy(SelectedJoint, rotationDelta);
                    }
                }

                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (CurrentTool == RiggingToolMode.CreateJoint && SelectedJoint != null)
            {
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        // === YENİ: Köməkçi metodlar (HƏLL A, B) ===
        #region Rigging Helpers

        /// <summary>
        /// (HƏLL A) Skeletin bütün sümük parametrlərini (Uzunluq/Bucaq)
        /// mövcud mövqelərinə görə yenidən hesablayır.
        /// </summary>
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
                j.BoneLength = (len < EPS) ? 0f : len;   // sıfıra yaxınsa 0 qəbul et
                j.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);
            }
        }

        /// <summary>
        /// Verilən oynağın bütün övladlarını qaytarır
        /// </summary>
        private IEnumerable<JointModel> ChildrenOf(JointModel p)
            => Joints.Where(j => j.Parent == p);

        /// <summary>
        /// (HƏLL B) Verilmiş oynağın alt zəncirini rekursiv olaraq yenidən hesablayır.
        /// </summary>
        private void RecomputeSubtree(JointModel root)
        {
            foreach (var child in ChildrenOf(root).ToList())
            {
                float dx = child.Position.X - root.Position.X;
                float dy = child.Position.Y - root.Position.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);

                child.BoneLength = (len < EPS) ? 0f : len;
                child.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);

                RecomputeSubtree(child);
            }
        }

        #endregion

        /// <summary>
        /// (KÖHNƏ) Hərəkət fərqini (delta) bu oynağa və bütün övladlarına tətbiq edir.
        /// (ROOT oynağı "Pose" rejimində tərpətmək üçün saxlanılır)
        /// </summary>
        private void ApplyRecursiveMove(JointModel joint, SKPoint delta)
        {
            if (joint == null) return;
            joint.Position += delta;

            foreach (var child in Joints.Where(j => j.Parent == joint).ToList())
            {
                ApplyRecursiveMove(child, delta);
            }
        }


        /// <summary>
        /// YENİ METOD (DÜZƏLİŞLİ - HƏLL C): Fırlanma fərqini (delta) bu oynağa tətbiq edir
        /// və sümük uzunluğunu qoruyaraq bütün övladlarını yeniləyir.
        /// </summary>
        private void UpdatePoseHierarchy(JointModel joint, float rotationDelta)
        {
            if (joint == null) return;

            // 1. Öz mütləq bucağını yenilə
            joint.Rotation += rotationDelta;

            // 2. Valideyni varsa, bucaq və sümük uzunluğuna görə yeni mövqeyini hesabla
            if (joint.Parent != null)
            {
                // === YENİ (HƏLL C): Sıfır uzunluqlu sümükləri fırlatma ===
                if (joint.BoneLength > EPS)
                {
                    joint.Position = new SKPoint(
                        joint.Parent.Position.X + MathF.Cos(joint.Rotation) * joint.BoneLength,
                        joint.Parent.Position.Y + MathF.Sin(joint.Rotation) * joint.BoneLength
                    );
                }
                else
                {
                    // Uzunluq sıfırsa parent üstündə qalır
                    joint.Position = joint.Parent.Position;
                }
                // =======================================================
            }

            // 3. Bütün övladlarını tap və eyni fırlanma fərqini onlara da tətbiq et
            foreach (var child in Joints.Where(j => j.Parent == joint).ToList())
            {
                UpdatePoseHierarchy(child, rotationDelta);
            }
        }


        /// <summary>
        /// Sol siçan düyməsi buraxıldıqda (View tərəfindən çağırılır)
        /// </summary>
        public void OnCanvasLeftReleased()
        {
            _isDraggingJoint = false;
        }

        public void DeselectCurrentJoint()
        {
            if (SelectedJoint != null)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }


        /// <summary>
        /// Seçilmiş oynağı silir. (Dəyişməyib)
        /// </summary>
        public void DeleteSelectedJoint()
        {
            if (SelectedJoint == null)
                return;

            var jointToRemove = SelectedJoint;
            SelectedJoint = null;
            _isDraggingJoint = false;

            Joints.Remove(jointToRemove);

            foreach (var joint in Joints.ToList())
            {
                if (joint.Parent == jointToRemove)
                {
                    joint.Parent = null;

                    // Valideyni silindiyi üçün sümük uzunluğunu/bucağını sıfırla
                    joint.BoneLength = 0;
                    joint.Rotation = 0;
                }
            }

            RequestRedraw?.Invoke(this, EventArgs.Empty);
            SaveRigCommand.NotifyCanExecuteChanged();
        }




        private void ClearRiggingData()
        {
            Joints.Clear();
            SelectedJoint = null;
            _jointIdCounter = 0;
            SaveRigCommand?.NotifyCanExecuteChanged();
        }

        // === OnCurrentToolChanged (DÜZƏLİŞLİ) ===
        partial void OnCurrentToolChanged(RiggingToolMode value)
        {
            // Edit və ya Pose rejiminə keçəndə seçimi ləğv et
            if (value == RiggingToolMode.Edit || value == RiggingToolMode.Pose)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }

            // === YENİ (HƏLL A): Pose rejiminə keçəndə kalibr et ===
            if (value == RiggingToolMode.Pose)
            {
                RecomputeBoneParamsFromPositions();
            }
            // ====================================================

            _isDraggingJoint = false;
        }
    }
}