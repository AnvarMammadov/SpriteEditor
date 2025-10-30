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

        // === KAMERA VƏZİYYƏTİ ((P*S)+O MODELİ) ===
        // Bu modeldə CameraOffset EKRAN fəzasındadır (Screen-Space)
        public SKPoint CameraOffset { get; private set; } = SKPoint.Empty;
        public float CameraScale { get; private set; } = 1.0f;

        private bool _isPanning = false;
        private SKPoint _lastPanPosition;
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
                    byte[] fileBytes = File.ReadAllBytes(openDialog.FileName);
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
                    ClearRiggingData();
                    ResetCamera();
                }
            }
        }

        public void ResetCamera()
        {
            CameraOffset = SKPoint.Empty;
            CameraScale = 1.0f;
        }

        /// <summary>
        /// (P*S)+O MODELİ: Şəkli kətanın mərkəzinə çəkmək üçün EKRAN ofsetini hesablayır.
        /// </summary>
        public void CenterCamera(float canvasWidth, float canvasHeight, bool forceRecenter = false)
        {
            if (LoadedBitmap == null) return;

            if (forceRecenter || (CameraScale == 1.0f && CameraOffset == SKPoint.Empty))
            {
                // Ekran ofseti:
                float offsetX = (canvasWidth - (LoadedBitmap.Width * CameraScale)) / 2;
                float offsetY = (canvasHeight - (LoadedBitmap.Height * CameraScale)) / 2;

                CameraOffset = new SKPoint(offsetX, offsetY);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// (P*S)+O MODELİ: Ekran koordinatını Dünya koordinatına çevirir.
        /// Riyaziyyat: World = (Screen - ScreenOffset) / Scale
        /// </summary>
        public SKPoint ScreenToWorld(SKPoint screenPoint)
        {
            return new SKPoint(
                (screenPoint.X - CameraOffset.X) / CameraScale,
                (screenPoint.Y - CameraOffset.Y) / CameraScale
            );
        }

        /// <summary>
        /// (P*S)+O MODELİ: Dünya koordinatını Ekran koordinatına çevirir.
        /// Riyaziyyat: Screen = (World * Scale) + ScreenOffset
        /// </summary>
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

        /// <summary>
        /// (P*S)+O MODELİ: "Zoom to Mouse" məntiqi
        /// </summary>
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

            // Siçanın "Dünya"dakı mövqeyini tap
            SKPoint worldPosBefore = ScreenToWorld(screenPos);

            // Miqyası yenilə
            CameraScale = newScale;

            // Kameranı elə sürüşdürürük ki, siçanın altındakı "dünya" nöqtəsi eyni qalsın
            // Riyazi izahı: newOffset = screenPos - (worldPos * newScale)
            CameraOffset = new SKPoint(
                screenPos.X - (worldPosBefore.X * CameraScale),
                screenPos.Y - (worldPosBefore.Y * CameraScale)
            );

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        public void OnCanvasLeftClicked(SKPoint screenPos)
        {
            SKPoint worldPos = ScreenToWorld(screenPos);

            if (CurrentTool == RiggingToolMode.CreateJoint)
            {
                // === "SÜMÜK YARAT" REJİMİ ===
                // Bu kod olduğu kimi qalır
                var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
                Joints.Add(newJoint);
                SelectedJoint = newJoint;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (CurrentTool == RiggingToolMode.None)
            {
                // === YENİ KOD: "SEÇİM" REJİMİ ===

                // Kliklənən nöqtəyə ən yaxın oynağı tapmaq
                JointModel closestJoint = null;
                float minDistanceSq = float.MaxValue; // Kvadrat məsafə (daha sürətli hesablama üçün)

                // Ekranda 10 piksellik bir sahəni "klik sahəsi" kimi götürək
                // Bunu Dünya koordinatlarına çeviririk ki, zoom zamanı da düz işləsin
                float clickRadiusScreen = 10f;
                float clickRadiusWorld = clickRadiusScreen / CameraScale;
                float clickRadiusSq = clickRadiusWorld * clickRadiusWorld; // Kvadratı

                foreach (var joint in Joints)
                {
                    // Nöqtələr arasındakı məsafənin kvadratını tapırıq (Math.Sqrt daha yavaşdır)
                    float dx = worldPos.X - joint.Position.X;
                    float dy = worldPos.Y - joint.Position.Y;
                    float distanceSq = (dx * dx) + (dy * dy);

                    // Əgər bu oynaq klik radiusu daxilindədirsə VƏ indiyə qədər tapdığımızdan daha yaxındırsa
                    if (distanceSq < clickRadiusSq && distanceSq < minDistanceSq)
                    {
                        minDistanceSq = distanceSq;
                        closestJoint = joint;
                    }
                }

                // Ən yaxın oynağı seçilmiş edirik (əgər heç nə tapılmayıbsa, null olacaq)
                if (SelectedJoint != closestJoint)
                {
                    SelectedJoint = closestJoint;
                    RequestRedraw?.Invoke(this, EventArgs.Empty); // View-u yenilə
                }
            }
        }

        public void OnCanvasMouseMoved(SKPoint screenPos)
        {
            if (_isPanning)
            {
                // Pan əməliyyatı EKRAN fəzasında baş verir
                SKPoint delta = new SKPoint(screenPos.X - _lastPanPosition.X, screenPos.Y - _lastPanPosition.Y);
                CameraOffset = new SKPoint(CameraOffset.X + delta.X, CameraOffset.Y + delta.Y);
                _lastPanPosition = screenPos;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Pan etmiriksə, "Dünya" koordinatını hesablayıb önizləmə (preview) üçün istifadə et
            SKPoint worldPos = ScreenToWorld(screenPos);
            CurrentMousePosition = worldPos;

            if (CurrentTool == RiggingToolMode.CreateJoint && SelectedJoint != null)
            {
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Sümük yaratma zəncirini ləğv edir (seçimi sıfırlayır).
        /// View (code-behind) tərəfindən (Sağ Klik ilə) çağırılacaq.
        /// </summary>
        public void DeselectCurrentJoint()
        {
            // Yalnız o zaman yenidən çəkək ki, həqiqətən nəsə seçilmişdi
            if (SelectedJoint != null)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty); // View-a xəbər ver ki, preview xəttini gizlətsin
            }
        }

        private void ClearRiggingData()
        {
            Joints.Clear();
            SelectedJoint = null;
            _jointIdCounter = 0;
        }

        partial void OnCurrentToolChanged(RiggingToolMode value)
        {
            if (value != RiggingToolMode.CreateJoint)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}