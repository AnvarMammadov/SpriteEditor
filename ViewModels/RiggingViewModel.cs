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

    // 1. Alət rejimlərini təyin edən Enum
    public enum RiggingToolMode
    {
        None,
        CreateJoint // Oynaq (sümük) yaratma
    }
    public partial class RiggingViewModel : ObservableObject
    {
        // View-a "Yenidən çək" siqnalı göndərmək üçün hadisə (event)
        public event EventHandler RequestRedraw;

        public event EventHandler RequestCenterCamera;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadImageCommand))]
        private bool _isImageLoaded = false;

        [ObservableProperty]
        private SKBitmap _loadedBitmap; // Şəkli BitmapImage yox, SKBitmap olaraq saxlayacağıq


        // === YENİ XASSƏLƏR (PROPERTIES) ===

        [ObservableProperty]
        private RiggingToolMode _currentTool = RiggingToolMode.None; // Hazırkı aktiv alət

        // Bütün yaradılmış oynaql (joint) siyahısı
        public ObservableCollection<JointModel> Joints { get; } = new ObservableCollection<JointModel>();

        [ObservableProperty]
        private JointModel _selectedJoint; // Kliklə seçilən son oynaq (yeni sümüyün başlanğıcı)


        [ObservableProperty]
        private SKPoint _currentMousePosition; // Siçanın kətan üzərindəki yeri (önizləmə üçün)

        private int _jointIdCounter = 0; // Oynaqlara unikal ID vermək üçün

        // === YENİ: KAMERA VƏZİYYƏTİ (STATE) ===

        // Kameranın sürüşməsi (offseti)
        public SKPoint CameraOffset { get; private set; } = SKPoint.Empty;
        // Kameranın miqyası (yaxınlaşdırma)
        public float CameraScale { get; private set; } = 1.0f;

        // Sürüşdürmə (Pan) üçün köməkçi dəyişənlər
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
                    // Cache probleminin qarşısını almaq üçün byte massivi olaraq oxuyuruq
                    byte[] fileBytes = File.ReadAllBytes(openDialog.FileName);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        // SkiaSharp-ın daxili metodu ilə şəkli decode edirik
                        LoadedBitmap = SKBitmap.Decode(ms);
                    }

                    if (LoadedBitmap == null)
                    {
                        throw new Exception("Fayl formatı dəstəklənmir və ya fayl zədəlidir.");
                    }

                    IsImageLoaded = true;
                    // View-a xəbər veririk ki, şəkil yükləndi, kətanı yeniləsin
                    ClearRiggingData();
                    ResetCamera();
                    RequestCenterCamera?.Invoke(this, EventArgs.Empty);
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
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



        /// <summary>
        /// Kameranı başlanğıc vəziyyətinə qaytarır və yenidən çəkir.
        /// </summary>
        public void ResetCamera()
        {
            CameraOffset = SKPoint.Empty;
            CameraScale = 1.0f;
        }

        /// <summary>
        /// DÜZƏLDİLMİŞ: Şəkli kətanın mərkəzinə çəkmək üçün ofseti hesablayır.
        /// </summary>
        public void CenterCamera(float canvasWidth, float canvasHeight, bool forceRecenter = false)
        {
            if (LoadedBitmap == null) return;

            // Nə vaxt mərkəzləşdirməli:
            // 1. forceRecenter = true (yəni LoadImage məcbur edir)
            // 2. VƏ YA hələ heç bir dəyişiklik edilməyibsə (ilkin yükləmə)
            if (forceRecenter || (CameraScale == 1.0f && CameraOffset == SKPoint.Empty))
            {
                // DÜZƏLİŞ BURADADIR: Miqyası (Scale) nəzərə alırıq
                float offsetX = (canvasWidth - (LoadedBitmap.Width * CameraScale)) / 2;
                float offsetY = (canvasHeight - (LoadedBitmap.Height * CameraScale)) / 2;

                CameraOffset = new SKPoint(offsetX, offsetY);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        // === YENİ: Koordinat Çevirmə (Transform) Metodları ===

        /// <summary>
        /// Siçanın kliklədiyi "Ekran" (Screen) koordinatını "Dünya" (World) koordinatına çevirir.
        /// </summary>
        public SKPoint ScreenToWorld(SKPoint screenPoint)
        {
            return new SKPoint(
                (screenPoint.X - CameraOffset.X) / CameraScale,
                (screenPoint.Y - CameraOffset.Y) / CameraScale
            );
        }

        /// <summary>
        /// "Dünya" (World) koordinatını "Ekran" (Screen) koordinatına çevirir.
        /// (Gələcəkdə UI elementləri üçün lazım ola bilər)
        /// </summary>
        public SKPoint WorldToScreen(SKPoint worldPoint)
        {
            return new SKPoint(
                (worldPoint.X * CameraScale) + CameraOffset.X,
                (worldPoint.Y * CameraScale) + CameraOffset.Y
            );
        }

        // === YENİ: Siçan İdarəetmə Metodları (View tərəfindən çağırılacaq) ===

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
        /// DÜZƏLDİLMİŞ: Siçanın olduğu yerə yaxınlaşdırma (Zoom to Mouse) məntiqi
        /// </summary>
        public void HandleZoom(SKPoint screenPos, int delta)
        {
            float zoomFactor = 1.1f;
            float newScale;

            if (delta > 0)
                newScale = CameraScale * zoomFactor; // Yaxınlaşdır
            else
                newScale = CameraScale / zoomFactor; // Uzaqlaşdır

            // Zoom limitləri
            newScale = Math.Max(0.1f, Math.Min(newScale, 10.0f));

            if (Math.Abs(newScale - CameraScale) < 0.001f) return;

            // Siçanın "Dünya"dakı mövqeyini tap
            SKPoint worldPosBefore = ScreenToWorld(screenPos);

            // Miqyası yenilə
            CameraScale = newScale;

            // Miqyas dəyişdikdən sonra siçanın "Ekran" mövqeyinin altındakı YENİ "Dünya" mövqeyini tap
            // DÜZƏLİŞ: Bu hesablama artıq birbaşa ofset dəyişikliyində edilməlidir.
            // SKPoint worldPosAfter = ScreenToWorld(screenPos); // Buna ehtiyac yoxdur

            // Kameranı elə sürüşdürürük ki, siçanın altındakı "dünya" nöqtəsi eyni qalsın
            // Riyazi izahı: newOffset = screenPos - (worldPos * newScale)
            CameraOffset = new SKPoint(
                screenPos.X - (worldPosBefore.X * CameraScale),
                screenPos.Y - (worldPosBefore.Y * CameraScale)
            );

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }




        // DÜZƏLDİLMİŞ: OnCanvasLeftClicked (artıq if silindi)
        /// <summary>
        /// Kətan üzərinə klikləndikdə View (code-behind) tərəfindən çağırılacaq.
        /// </summary>
        public void OnCanvasLeftClicked(SKPoint screenPos)
        {
            SKPoint worldPos = ScreenToWorld(screenPos);

            if (CurrentTool == RiggingToolMode.CreateJoint)
            {
                var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
                Joints.Add(newJoint);
                SelectedJoint = newJoint;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Siçan tərpəndikcə View (code-behind) tərəfindən çağırılacaq.
        /// </summary>
        public void OnCanvasMouseMoved(SKPoint screenPos) // Dəyişdirildi: screenPos
        {
            // Əgər Pan ediriksə, kameranı hərəkət etdir
            if (_isPanning)
            {
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
        /// Bütün sümük/oynaq məlumatlarını sıfırlayır
        /// </summary>
        private void ClearRiggingData()
        {
            Joints.Clear();
            SelectedJoint = null;
            _jointIdCounter = 0;
        }

        // Partial metod: Alət dəyişəndə (UI-dan) xəbərimiz olsun
        partial void OnCurrentToolChanged(RiggingToolMode value)
        {
            // Əgər "Sümük Yarat" alətindən çıxırıqsa, seçilmiş oynağı sıfırla
            if (value != RiggingToolMode.CreateJoint)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty); // Seçim halqasını gizlət
            }
        }
    }
}
