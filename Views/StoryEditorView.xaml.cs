using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SpriteEditor.Data.Story;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Views
{
    public partial class StoryEditorView : UserControl
    {
        private StoryEditorViewModel ViewModel => DataContext as StoryEditorViewModel;
        private bool _isDraggingNode = false;
        private Point _lastMousePos;

        public StoryEditorView()
        {
            InitializeComponent();
        }

        // === NODE SÜRÜKLƏMƏK (Move) ===
        private void Node_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingNode = true;
            _lastMousePos = e.GetPosition(this);
            (sender as FrameworkElement).CaptureMouse();
        }

        // === PORTDAN TUTUB ÇƏKMƏK (Link Create) ===
        private void Port_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            var node = btn.Tag as StoryNode; // Tag-dən node-u alırıq

            if (ViewModel != null && node != null)
            {
                // Canvas üzərindəki koordinatı tapırıq
                Point p = btn.TranslatePoint(new Point(8, 8), this); // Buttonun mərkəzi
                ViewModel.StartConnectionDrag(node, p);

                e.Handled = true; // Node sürüşməsinin qarşısını alırıq
            }
        }

        // === MOUSE HƏRƏKƏTİ (Ümumi) ===
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;

            Point currentPos = e.GetPosition(this);

            // 1. Əgər Node sürüşdürürüksə
            if (_isDraggingNode && ViewModel.SelectedNode != null)
            {
                double dx = currentPos.X - _lastMousePos.X;
                double dy = currentPos.Y - _lastMousePos.Y;

                ViewModel.SelectedNode.X += dx;
                ViewModel.SelectedNode.Y += dy;

                _lastMousePos = currentPos;
                ViewModel.RefreshConnections(); // Xətləri yenilə
            }

            // 2. Əgər Xətt çəkiriksə (Link Dragging)
            if (ViewModel.IsDraggingConnection)
            {
                ViewModel.UpdateConnectionDrag(currentPos);
            }
        }

        // === BURAXMAQ (Mouse Up) ===
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingNode)
            {
                _isDraggingNode = false;
                Mouse.Capture(null);
            }

            if (ViewModel != null && ViewModel.IsDraggingConnection)
            {
                // Buraxılan yerdə Node varmı?
                var hitElement = InputHitTest(e.GetPosition(this)) as FrameworkElement;
                var targetNode = hitElement?.DataContext as StoryNode;

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
    }
}
