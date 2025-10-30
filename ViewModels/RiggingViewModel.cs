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
        None,
        CreateJoint
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
        private RiggingToolMode _currentTool = RiggingToolMode.None;

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

        // === YENİ ƏLAVƏ: Oynaq sürükləmə vəziyyəti ===
        private bool _isDraggingJoint = false;
        // ======================================


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


        // === YENİ ƏMR: Skeleti Yüklə ===
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
                    // 1. Faylı oxu
                    string jsonString = await File.ReadAllTextAsync(openDialog.FileName);

                    // 2. JSON-u RigData obyektinə çevir (Deserializasiya)
                    var rigData = JsonSerializer.Deserialize<RigData>(jsonString);

                    if (rigData == null || rigData.Joints == null)
                    {
                        throw new Exception("JSON faylının strukturu düzgün deyil.");
                    }

                    // 3. (İstəyə bağlı) Yoxlama: JSON-dakı şəkil adı ilə mövcud şəkil adını yoxla
                    string jsonImageName = rigData.ImageFileName;
                    string currentImageName = Path.GetFileName(_loadedImagePath);
                    if (jsonImageName != currentImageName)
                    {
                        var result = MessageBox.Show(
                            $"Bu skelet faylı ('{jsonImageName}') yüklənmiş şəkildən ('{currentImageName}') fərqli bir şəkil üçün yaradılıb.\n\nDavam etmək istəyirsinizmi?",
                            "Xəbərdarlıq", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                            return; // Ləğv et
                    }

                    // 4. Mövcud skeleti təmizlə
                    ClearRiggingData();

                    // === 5. İYERARXİYANIN BƏRPASI (Ən vacib hissə) ===

                    // Köməkçi lüğət (dictionary) yaradırıq ki, ID-yə görə JointModel-i tez tapa bilək
                    var jointMap = new Dictionary<int, JointModel>();

                    // Birinci döngü (Pass 1): Bütün JointModel-ləri yarat, amma Parent-i təyin etmə
                    foreach (var jointData in rigData.Joints)
                    {
                        var newJoint = new JointModel(jointData.Id, jointData.Position, null); // Parent hələlik null
                        Joints.Add(newJoint);
                        jointMap.Add(newJoint.Id, newJoint);
                    }

                    // İkinci döngü (Pass 2): İndi Parent referanslarını təyin et
                    foreach (var jointData in rigData.Joints)
                    {
                        if (jointData.ParentId != -1) // Əgər bu "root" deyilsə
                        {
                            // Lüğətdən özünü və valideynini tap
                            if (jointMap.TryGetValue(jointData.Id, out JointModel currentJoint) &&
                                jointMap.TryGetValue(jointData.ParentId, out JointModel parentJoint))
                            {
                                // Referansı təyin et
                                currentJoint.Parent = parentJoint;
                            }
                        }
                    }

                    // 6. ID sayğacını (counter) yenilə ki, yeni sümüklər köhnələrlə toqquşmasın
                    // Ən böyük ID-ni tap və üzərinə 1 gəl
                    if (Joints.Count > 0)
                    {
                        _jointIdCounter = Joints.Max(j => j.Id) + 1;
                    }

                    // 7. Ekrana yeniləmə və düymələri aktiv etmə
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                    SaveRigCommand.NotifyCanExecuteChanged();

                    MessageBox.Show("Skelet uğurla yükləndi.", "Uğurlu");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yükləyərkən xəta baş verdi: {ex.Message}", "Xəta");
                    // Uğursuz olarsa, təmizlə
                    ClearRiggingData();
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
        }



        // LoadRigCommand-ın nə vaxt aktiv olacağını bildirir
        private bool CanLoadRig()
        {
            // Yalnız bir şəkil yüklənibsə, köhnə skeleti yükləmək olar
            return IsImageLoaded;
        }




        [RelayCommand(CanExecute = nameof(CanSaveRig))]
        private async Task SaveRigAsync()
        {
            if (!CanSaveRig()) return;

            // 1. Saxlamaq üçün data strukturunu hazırla
            var rigData = new RigData
            {
                // Şəklin adını (yol olmadan) JSON-a yaz
                ImageFileName = Path.GetFileName(_loadedImagePath)
            };

            // 2. Mövcud JointModel-ləri JointData-ya çevir
            foreach (var joint in Joints)
            {
                rigData.Joints.Add(new JointData
                {
                    Id = joint.Id,
                    Position = joint.Position,
                    // Valideyn varsa onun ID-sini, yoxdursa -1 yaz
                    ParentId = joint.Parent?.Id ?? -1
                });
            }

            // 3. SaveFileDialog göstər
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                // Defolt fayl adı təklif et (məs: "character.png" -> "character.rig.json")
                FileName = $"{Path.GetFileNameWithoutExtension(_loadedImagePath)}.rig.json",
                Filter = "Rig JSON Faylı (*.rig.json)|*.rig.json|Bütün Fayllar (*.*)|*.*",
                Title = "Skelet Məlumatını Saxla"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // 4. JSON-a çevirmə (Serializasiya)
                    // "WriteIndented" JSON-un oxunaqlı (gözəl) formatda yazılmasını təmin edir
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(rigData, options);

                    // 5. Fayla yaz (Asinxron)
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);

                    MessageBox.Show("Skelet uğurla yadda saxlandı!", "Uğurlu");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yadda saxlayarkən xəta baş verdi: {ex.Message}", "Xəta");
                }
            }
        }


        // SaveRigCommand-ın nə vaxt aktiv olacağını bildirir
        private bool CanSaveRig()
        {
            // Yalnız şəkil yüklənibsə və ən az bir oynaq varsa saxlamaq olar
            return IsImageLoaded && Joints.Count > 0;
        }


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
                newScale = CameraScale * zoomFactor; // Yaxınlaşdır
            else
                newScale = CameraScale / zoomFactor; // Uzaqlaşdır

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

        // === DƏYİŞİKLİK BURADA (OnCanvasLeftClicked) ===
        public void OnCanvasLeftClicked(SKPoint screenPos)
        {
            SKPoint worldPos = ScreenToWorld(screenPos);

            if (CurrentTool == RiggingToolMode.CreateJoint)
            {
                // === "SÜMÜK YARAT" REJİMİ ===
                var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
                Joints.Add(newJoint);
                SelectedJoint = newJoint;
                RequestRedraw?.Invoke(this, EventArgs.Empty);

                SaveRigCommand.NotifyCanExecuteChanged();
            }
            else if (CurrentTool == RiggingToolMode.None)
            {
                // === "SEÇİM" REJİMİ ===

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

                // Ən yaxın oynağı seçilmiş edirik
                SelectedJoint = closestJoint;
                RequestRedraw?.Invoke(this, EventArgs.Empty); // View-u yenilə

                // YENİ ƏLAVƏ: Əgər bir oynaq tapdıqsa, sürükləməyə başla
                if (SelectedJoint != null)
                {
                    _isDraggingJoint = true;
                }
            }
        }

        // === DƏYİŞİKLİK BURADA (OnCanvasMouseMoved) ===
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
            CurrentMousePosition = worldPos; // Preview üçün həmişə yenilə

            // === YENİ KOD: Sürükləmə (Dragging) ===
            if (_isDraggingJoint && SelectedJoint != null && CurrentTool == RiggingToolMode.None)
            {
                // Seçilmiş oynağın mövqeyini birbaşa yenilə
                SelectedJoint.Position = worldPos;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            // ======================================
            else if (CurrentTool == RiggingToolMode.CreateJoint && SelectedJoint != null) // 'else if' etdik
            {
                // Sümük yaratma preview-u
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        // === YENİ METOD (OnCanvasLeftReleased) ===
        /// <summary>
        /// Sol siçan düyməsi buraxıldıqda (View tərəfindən çağırılır)
        /// </summary>
        public void OnCanvasLeftReleased()
        {
            _isDraggingJoint = false;
        }
        // ========================================

        public void DeselectCurrentJoint()
        {
            if (SelectedJoint != null)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }


        /// <summary>
        /// YENİ METOD: Seçilmiş oynağı silir.
        /// </summary>
        public void DeleteSelectedJoint()
        {
            if (SelectedJoint == null)
                return; // Silinəcək bir şey seçilməyib

            var jointToRemove = SelectedJoint;

            // 1. Seçimi ləğv et
            SelectedJoint = null;
            _isDraggingJoint = false; // Sürükləməni də dayandır

            // 2. Oynağı siyahıdan sil
            Joints.Remove(jointToRemove);

            // 3. Digər oynaqları yoxla və "yetim" qalanların valideynini (Parent) null et
            // Bu, kaskadlı silmənin qarşısını alır və uşaqları "root" oynaqlara çevirir.
            foreach (var joint in Joints)
            {
                if (joint.Parent == jointToRemove)
                {
                    joint.Parent = null; // Bu oynaq artıq bir "root" oynağıdır
                }
            }

            // 4. Ekrana yeniləmə tələbi göndər
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

        partial void OnCurrentToolChanged(RiggingToolMode value)
        {
            if (value != RiggingToolMode.CreateJoint)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            // Sürükləməni də ləğv edək (ehtiyat üçün)
            _isDraggingJoint = false;
        }
    }
}