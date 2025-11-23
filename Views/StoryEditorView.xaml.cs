using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpriteEditor.Data.Story;
using SpriteEditor.ViewModels;
using SpriteEditor.Data;

namespace SpriteEditor.Views
{
    public partial class StoryEditorView : UserControl
    {
        private StoryEditorViewModel ViewModel => DataContext as StoryEditorViewModel;

        // Vəziyyət bayraqları
        private bool _isDraggingNode = false;
        private bool _isPanning = false;

        // Yadda saxlanan koordinatlar
        private Point _lastMousePosForNode; // Node sürükləmək üçün
        private Point _lastMousePosForPan;  // Pan (Səhnəni çəkmək) üçün

        public StoryEditorView()
        {
            InitializeComponent();
        }

        // ===============================================
        // 1. ZOOM (SCROLL WHEEL)
        // ===============================================
        private void EditorCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            // Zoom mərkəzi (Mouse hardadırsa ora yaxınlaşsın)
            Point mousePos = e.GetPosition(WorldLayer);

            double zoomFactor = 1.1;
            double newZoom = e.Delta > 0
                ? ViewModel.ZoomLevel * zoomFactor
                : ViewModel.ZoomLevel / zoomFactor;

            // Hədləri yoxla
            newZoom = Math.Max(StoryEditorViewModel.MinZoom, Math.Min(StoryEditorViewModel.MaxZoom, newZoom));

            // Zoom-a görə Pan-ı (sürüşməni) düzəltmək lazımdır ki, mouse olduğu yerdə qalsın
            if (Math.Abs(newZoom - ViewModel.ZoomLevel) > 0.001)
            {
                double scaleChange = newZoom / ViewModel.ZoomLevel;

                // Yeni Pan hesablaması:
                // (KöhnəPan - Mouse) * Change + Mouse
                // Bu düstur "Mouse-a tərəf yaxınlaş" effektini verir.
                // Amma sadəlik üçün hələlik mərkəzi saxlamaq daha stabil ola bilər.
                // Gəlin sadə variantla (sadəcə zoom) başlayaq, lazım olsa düzəldərik.

                ViewModel.ZoomLevel = newZoom;
            }

            e.Handled = true;
        }

        // ===============================================
        // 2. PAN (BOŞ YERƏ BASIB ÇƏKMƏK)
        // ===============================================
        private void EditorCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Sol düymə basılıbsa və Node/Port üzərində deyilsə (çünki onlar Handled edir)
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMousePosForPan = e.GetPosition(this); // Pan üçün ekran koordinatı lazımdır
                EditorCanvas.CaptureMouse();

                // Seçimi təmizlə
                if (ViewModel != null) ViewModel.SelectedNode = null;
            }
        }

        // ===============================================
        // 3. MOUSE HƏRƏKƏTİ (PAN, NODE DRAG, LINK DRAG)
        // ===============================================
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;

            // A) PAN (Səhnəni sürüşdürmək)
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(this);
                double dx = currentPos.X - _lastMousePosForPan.X;
                double dy = currentPos.Y - _lastMousePosForPan.Y;

                ViewModel.PanX += dx;
                ViewModel.PanY += dy;

                _lastMousePosForPan = currentPos;
            }
            // B) NODE SÜRÜKLƏMƏK
            else if (_isDraggingNode && ViewModel.SelectedNode != null)
            {
                Point currentPos = e.GetPosition(this);

                // Ekranda nə qədər hərəkət edib?
                double dx = currentPos.X - _lastMousePosForNode.X;
                double dy = currentPos.Y - _lastMousePosForNode.Y;

                // VACİB: Zoom olduğu üçün hərəkəti ZoomLevel-ə bölmək lazımdır!
                // Əgər Zoom=2-dirsə, mouse 10px gedəndə node 5px getməlidir.
                ViewModel.SelectedNode.X += dx / ViewModel.ZoomLevel;
                ViewModel.SelectedNode.Y += dy / ViewModel.ZoomLevel;

                _lastMousePosForNode = currentPos;
                ViewModel.RefreshConnections();
            }
            // C) LINK (XƏTT) SÜRÜKLƏMƏK
            else if (ViewModel.IsDraggingConnection)
            {
                // Xəttin ucu "Dünya" koordinatında olmalıdır
                Point mouseInWorld = e.GetPosition(WorldLayer);
                ViewModel.UpdateConnectionDrag(mouseInWorld);
            }
        }

        // ===============================================
        // 4. MOUSE UP (BURAXMAQ)
        // ===============================================
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                EditorCanvas.ReleaseMouseCapture();
            }

            if (_isDraggingNode)
            {
                _isDraggingNode = false;
                Mouse.Capture(null);
            }

            if (ViewModel != null && ViewModel.IsDraggingConnection)
            {
                // Mouse-un altında nə var?
                // HitTest artıq WorldLayer (Zoom olunmuş qat) üzərində aparılmalıdır?
                // Xeyr, VisualTreeHelper ekran koordinatı ilə işləyir.

                var hitResult = VisualTreeHelper.HitTest(EditorCanvas, e.GetPosition(EditorCanvas));
                var targetNode = FindAncestorData<StoryNode>(hitResult?.VisualHit);

                if (targetNode != null)
                {
                    ViewModel.CompleteConnection(targetNode);
                }
                else
                {
                    ViewModel.CancelConnectionDrag();
                }
            }
        }

        // ===============================================
        // 5. NODE VE PORT EVENTS (KÖHNƏ MƏNTİQİN YENİLƏNMİŞ HALI)
        // ===============================================
        private void Node_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            var node = element?.DataContext as StoryNode;

            if (ViewModel != null && node != null)
            {
                ViewModel.SelectedNode = node;
                _isDraggingNode = true;

                // Sürükləmə üçün başlanğıc nöqtə (Ekran koordinatı)
                _lastMousePosForNode = e.GetPosition(this);

                element.CaptureMouse();
                e.Handled = true; // Pan-ın işə düşməsinin qarşısını alır
            }
        }

        private void Port_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            var node = btn?.Tag as StoryNode;

            if (ViewModel != null && node != null)
            {
                // Portun koordinatını WorldLayer-ə (Zoom olunmuş qata) görə tapmalıyıq
                Point p = btn.TranslatePoint(new Point(btn.ActualWidth / 2, btn.ActualHeight / 2), WorldLayer);

                ViewModel.StartConnectionDrag(node, p);
                e.Handled = true;
            }
        }

        private T FindAncestorData<T>(DependencyObject current) where T : class
        {
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is T data)
                    return data;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}