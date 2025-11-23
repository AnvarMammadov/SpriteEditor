using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpriteEditor.Data.Story;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class StoryEditorView : UserControl
    {
        private StoryEditorViewModel ViewModel => DataContext as StoryEditorViewModel;

        // Sürükləmə vəziyyəti
        private bool _isDraggingNode = false;

        // Mouse-un Node-un sol yuxarı küncündən məsafəsi (Offset)
        private Point _dragOffset;

        public StoryEditorView()
        {
            InitializeComponent();
        }

        // === 1. NODE SEÇİMİ VƏ SÜRÜKLƏMƏYƏ HAZIRLIQ ===
        private void Node_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            var node = element?.DataContext as StoryNode;

            if (ViewModel != null && node != null)
            {
                ViewModel.SelectedNode = node;

                // Koordinatı EditorCanvas-a görə alırıq! (BU ƏSAS DÜZƏLİŞDİR)
                Point mousePos = e.GetPosition(EditorCanvas);

                // Node-un hazırkı koordinatlarını götürürük
                // Mouse node-un harasından tutub? O fərqi yadda saxlayırıq.
                _dragOffset = new Point(mousePos.X - node.X, mousePos.Y - node.Y);

                _isDraggingNode = true;
                element.CaptureMouse(); // Mouse-u elementə kilidləyirik
                e.Handled = true;
            }
        }

        // === 2. PORTDAN XƏTT ÇƏKMƏK ===
        private void Port_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            var node = btn?.Tag as StoryNode;

            if (ViewModel != null && node != null)
            {
                // Portun mərkəzini EditorCanvas-a nəzərən tapırıq
                Point p = btn.TranslatePoint(new Point(btn.ActualWidth / 2, btn.ActualHeight / 2), EditorCanvas);

                ViewModel.StartConnectionDrag(node, p);
                e.Handled = true;
            }
        }

        // === 3. MOUSE HƏRƏKƏTİ (DRAGGING) ===
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;

            // Koordinatı həmişə Canvas-a görə alırıq
            Point currentPos = e.GetPosition(EditorCanvas);

            // A) Node Sürükləmə
            if (_isDraggingNode && ViewModel.SelectedNode != null)
            {
                // Mouse hardadırsa, offset-i çıxırıq ki, node mouse-un altında qalsın (sürüşməsin)
                ViewModel.SelectedNode.X = currentPos.X - _dragOffset.X;
                ViewModel.SelectedNode.Y = currentPos.Y - _dragOffset.Y;

                ViewModel.RefreshConnections();
            }

            // B) Xətt Sürükləmə
            if (ViewModel.IsDraggingConnection)
            {
                ViewModel.UpdateConnectionDrag(currentPos);
            }
        }

        // === 4. BURAXMAQ (MOUSE UP) ===
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Node sürüşdürməni bitir
            if (_isDraggingNode)
            {
                _isDraggingNode = false;
                Mouse.Capture(null);
            }

            // Xətt çəkməni bitir
            if (ViewModel != null && ViewModel.IsDraggingConnection)
            {
                // Buraxılan yerdə nə var? (EditorCanvas-a nəzərən)
                var hitResult = VisualTreeHelper.HitTest(EditorCanvas, e.GetPosition(EditorCanvas));

                // Vizual ağacda yuxarı qalxıb StoryNode axtarırıq
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

        // Köməkçi: Boş yerə klikləyəndə seçimi ləğv etmək
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            // Əgər Canvas-a kliklənibsə və Node sürüşdürülmürsə
            if (ViewModel != null && !_isDraggingNode)
            {
                ViewModel.SelectedNode = null;
            }
        }

        // Köməkçi: Vizual ağacda DataContext-i axtarır
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