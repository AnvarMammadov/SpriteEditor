using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpriteEditor.Data.Story;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    // Xəttin çəkilməsi üçün köməkçi klass
    public class NodeConnection
    {
        public StoryNode Source { get; set; }
        public StoryNode Target { get; set; }
        // Xəttin başlanğıc və son nöqtələri (UI tərəfindən yenilənəcək)
        public Point StartPoint => new Point(Source.X + 150, Source.Y + 50); // Node eni 150, hündürlük yarısı
        public Point EndPoint => new Point(Target.X, Target.Y + 50);
    }

    public partial class StoryEditorViewModel : ObservableObject
    {
        public ObservableCollection<StoryNode> Nodes { get; } = new();

        // Ekranda görünən xətlər
        public ObservableCollection<NodeConnection> Connections { get; } = new();

        [ObservableProperty] private StoryNode _selectedNode;

        // Müvəqqəti xətt çəkmək üçün (Sürükləmə zamanı)
        [ObservableProperty] private Point _tempConnectionStart;
        [ObservableProperty] private Point _tempConnectionEnd;
        [ObservableProperty] private bool _isDraggingConnection;

        private StoryNode _dragSourceNode; // Hansı node-dan xətt çəkməyə başladıq?

        public StoryEditorViewModel()
        {
            // Test üçün 2 node
            var n1 = new StoryNode { Title = "Start", SpeakerName = "System", Text = "Game Starts", X = 100, Y = 100 };
            var n2 = new StoryNode { Title = "Scene 1", SpeakerName = "Alex", Text = "Hello!", X = 400, Y = 100 };
            Nodes.Add(n1);
            Nodes.Add(n2);
        }

        [RelayCommand]
        public void AddNode()
        {
            Nodes.Add(new StoryNode
            {
                Title = $"Node {Nodes.Count + 1}",
                SpeakerName = "Narrator",
                X = 200 + (Nodes.Count * 20),
                Y = 200 + (Nodes.Count * 20)
            });
        }

        [RelayCommand]
        public void DeleteNode()
        {
            if (SelectedNode == null) return;

            // 1. Əlaqələri təmizlə
            var linksToRemove = Connections.Where(c => c.Source == SelectedNode || c.Target == SelectedNode).ToList();
            foreach (var link in linksToRemove) Connections.Remove(link);

            // 2. Digər node-ların içindən seçimi sil
            foreach (var node in Nodes) node.Choices.RemoveAll(c => c.TargetNodeId == SelectedNode.Id);

            Nodes.Remove(SelectedNode);
            SelectedNode = null;
        }

        // === VİZUAL SCRIPTING MƏNTİQİ ===

        // 1. İstifadəçi Node-un sağındakı dairəyə basdı
        public void StartConnectionDrag(StoryNode source, Point startPos)
        {
            _dragSourceNode = source;
            TempConnectionStart = startPos;
            TempConnectionEnd = startPos;
            IsDraggingConnection = true;
        }

        // 2. İstifadəçi siçanı tərpədir
        public void UpdateConnectionDrag(Point currentPos)
        {
            if (IsDraggingConnection)
            {
                TempConnectionEnd = currentPos;
            }
        }

        // 3. İstifadəçi başqa bir Node-un üstündə buraxdı
        public void CompleteConnection(StoryNode target)
        {
            if (_dragSourceNode != null && target != null && _dragSourceNode != target)
            {
                // Data səviyyəsində əlaqə (JSON üçün)
                _dragSourceNode.Choices.Add(new StoryChoice
                {
                    Text = "Next", // Default mətn
                    TargetNodeId = target.Id
                });

                // Vizual səviyyədə əlaqə (Xətt üçün)
                Connections.Add(new NodeConnection { Source = _dragSourceNode, Target = target });
            }

            IsDraggingConnection = false;
            _dragSourceNode = null;
        }

        public void CancelConnectionDrag()
        {
            IsDraggingConnection = false;
            _dragSourceNode = null;
        }

        // Node tərpənəndə xətləri yeniləmək üçün (bunu View-dan çağıracağıq)
        public void RefreshConnections()
        {
            // Bu sadəcə Collection-a xəbər verir ki, "dəyişiklik var", 
            // beləliklə UI xətləri yenidən çəkir.
            var temp = Connections.ToList();
            Connections.Clear();
            foreach (var item in temp) Connections.Add(item);
        }
    }
}
