using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SpriteEditor.Data.Story;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    // Xəttin çəkilməsi üçün köməkçi klass
    public partial class NodeConnection : ObservableObject
    {
        public StoryNode Source { get; set; }
        public StoryNode Target { get; set; }

        public Point StartPoint => new Point(Source.X + 180, Source.Y + 42);
        public Point EndPoint => new Point(Target.X, Target.Y + 42);

        public Point ControlPoint1 => new Point(StartPoint.X + 80, StartPoint.Y);
        public Point ControlPoint2 => new Point(EndPoint.X - 80, EndPoint.Y);
    }

    public partial class StoryEditorViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isToolsPanelOpen = true;
        [ObservableProperty] private bool _isPropertiesPanelOpen = false;

        [RelayCommand]
        public void ToggleToolsPanel() => IsToolsPanelOpen = !IsToolsPanelOpen;

        [RelayCommand]
        public void TogglePropertiesPanel() => IsPropertiesPanelOpen = !IsPropertiesPanelOpen;

        partial void OnSelectedNodeChanged(StoryNode value)
        {
            if (value != null) IsPropertiesPanelOpen = true;
        }

        // Zoom və Pan
        [ObservableProperty] private double _zoomLevel = 1.0;
        [ObservableProperty] private double _panX = 0;
        [ObservableProperty] private double _panY = 0;

        public const double MinZoom = 0.2;
        public const double MaxZoom = 3.0;

        public ObservableCollection<StoryNode> Nodes { get; } = new();
        public ObservableCollection<NodeConnection> Connections { get; } = new();

        [ObservableProperty] private StoryNode _selectedNode;

        // ComboBox üçün Tiplər Siyahısı (View-a ötürmək üçün)
        public List<StoryNodeType> NodeTypes { get; } = Enum.GetValues(typeof(StoryNodeType)).Cast<StoryNodeType>().ToList();

        // Connection Dragging
        [ObservableProperty] private Point _tempConnectionStart;
        [ObservableProperty] private Point _tempConnectionEnd;
        [ObservableProperty] private bool _isDraggingConnection;
        private StoryNode _dragSourceNode;

        public ObservableCollection<StoryVariable> GlobalVariables { get; } = new();
        public List<VariableType> VariableTypes { get; } = Enum.GetValues(typeof(VariableType)).Cast<VariableType>().ToList();
        public List<ConditionOperator> ConditionOperators { get; } = Enum.GetValues(typeof(ConditionOperator)).Cast<ConditionOperator>().ToList();
        public List<ActionOperation> ActionOperations { get; } = Enum.GetValues(typeof(ActionOperation)).Cast<ActionOperation>().ToList();

        public StoryEditorViewModel()
        {
            ZoomLevel = 1.0;
            PanX = 0;
            PanY = 0;

            // Test Node (Start)
            var n1 = new StoryNode { Title = "Start", Type = StoryNodeType.Start, IsStartNode = true, SpeakerName = "System", Text = "Game Starts", X = 100, Y = 100 };
            Nodes.Add(n1);
        }

        [RelayCommand]
        public void AddNode()
        {
            Nodes.Add(new StoryNode
            {
                Title = $"Node {Nodes.Count + 1}",
                Type = StoryNodeType.Dialogue, // Default
                SpeakerName = "Narrator",
                X = (-PanX + 250) / ZoomLevel,
                Y = (-PanY + 250) / ZoomLevel
            });
        }

        [RelayCommand]
        public void DeleteNode()
        {
            if (SelectedNode == null) return;

            var linksToRemove = Connections.Where(c => c.Source == SelectedNode || c.Target == SelectedNode).ToList();
            foreach (var link in linksToRemove) Connections.Remove(link);

            foreach (var node in Nodes)
            {
                var choicesToRemove = node.Choices.Where(c => c.TargetNodeId == SelectedNode.Id).ToList();
                foreach (var choice in choicesToRemove) node.Choices.Remove(choice);
            }

            Nodes.Remove(SelectedNode);
            SelectedNode = null;
        }

        [RelayCommand]
        public void DeleteLink(StoryChoice choice)
        {
            if (SelectedNode == null || choice == null) return;

            var connectionToRemove = Connections.FirstOrDefault(c => c.Source == SelectedNode && c.Target.Id == choice.TargetNodeId);
            if (connectionToRemove != null) Connections.Remove(connectionToRemove);

            SelectedNode.Choices.Remove(choice);
        }

        [RelayCommand]
        public void SetAsStartNode(StoryNode targetNode)
        {
            var nodeToSet = targetNode ?? SelectedNode;
            if (nodeToSet == null) return;

            foreach (var node in Nodes) node.IsStartNode = false;
            nodeToSet.IsStartNode = true;
            // Tipini də Start edə bilərik
            nodeToSet.Type = StoryNodeType.Start;
        }

        // === VISUAL SCRIPTING LOGIC ===
        public void StartConnectionDrag(StoryNode source, Point startPos)
        {
            _dragSourceNode = source;
            TempConnectionStart = startPos;
            TempConnectionEnd = startPos;
            IsDraggingConnection = true;
        }

        public void UpdateConnectionDrag(Point currentPos)
        {
            if (IsDraggingConnection) TempConnectionEnd = currentPos;
        }

        public void CompleteConnection(StoryNode target)
        {
            if (_dragSourceNode != null && target != null && _dragSourceNode != target)
            {
                _dragSourceNode.Choices.Add(new StoryChoice
                {
                    Text = "Next",
                    TargetNodeId = target.Id
                });
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

        public void RefreshConnections()
        {
            var temp = Connections.ToList();
            Connections.Clear();
            foreach (var item in temp) Connections.Add(item);
        }

        [RelayCommand]
        public void PlayStory()
        {
            if (Nodes.Count == 0) { MessageBox.Show("Hekayə boşdur!", "Xəbərdarlıq"); return; }

            var storyGraph = new StoryGraph
            {
                Nodes = Nodes.ToList(),
                StartNodeId = Nodes.FirstOrDefault(n => n.IsStartNode)?.Id ?? Nodes.FirstOrDefault()?.Id,
                Variables = GlobalVariables.ToList()
            };

            var playerVm = new StoryPlayerViewModel();
            playerVm.LoadStory(storyGraph);

            var playerWindow = new StoryPlayerWindow
            {
                DataContext = playerVm,
                Owner = Application.Current.MainWindow
            };
            playerWindow.ShowDialog();
        }

        [RelayCommand]
        public async Task SaveStory()
        {
            var storyGraph = new StoryGraph
            {
                Nodes = Nodes.ToList(),
                StartNodeId = Nodes.FirstOrDefault(n => n.IsStartNode)?.Id ?? Nodes.FirstOrDefault()?.Id,
                Variables = GlobalVariables.ToList()
            };

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Story JSON (*.story.json)|*.story.json",
                FileName = "MyStory.story.json",
                Title = "Hekayəni Yadda Saxla"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(storyGraph, options);
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);
                    CustomMessageBox.Show("Hekayə uğurla yadda saxlanıldı!", "Uğurlu", MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Xəta: {ex.Message}", "Xəta", MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task LoadStory()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Story JSON (*.story.json)|*.story.json",
                Title = "Hekayəni Yüklə"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string jsonString = await File.ReadAllTextAsync(openDialog.FileName);
                    var storyGraph = JsonSerializer.Deserialize<StoryGraph>(jsonString);
                    if (storyGraph == null) return;

                    Nodes.Clear();
                    Connections.Clear();
                    GlobalVariables.Clear();

                    if (storyGraph.Variables != null)
                        foreach (var v in storyGraph.Variables) GlobalVariables.Add(v);

                    foreach (var node in storyGraph.Nodes)
                    {
                        node.IsStartNode = (node.Id == storyGraph.StartNodeId);
                        Nodes.Add(node);
                    }

                    foreach (var sourceNode in Nodes)
                    {
                        foreach (var choice in sourceNode.Choices)
                        {
                            if (!string.IsNullOrEmpty(choice.TargetNodeId))
                            {
                                var targetNode = Nodes.FirstOrDefault(n => n.Id == choice.TargetNodeId);
                                if (targetNode != null)
                                {
                                    Connections.Add(new NodeConnection { Source = sourceNode, Target = targetNode });
                                }
                            }
                        }
                    }
                    CustomMessageBox.Show("Hekayə uğurla yükləndi.", "Tamamlandı", MessageBoxButton.OK, MsgImage.Info);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Xəta: {ex.Message}", "Xəta", MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }

        [RelayCommand]
        public void AddVariable() => GlobalVariables.Add(new StoryVariable { Name = "Variable_" + (GlobalVariables.Count + 1) });

        [RelayCommand]
        public void DeleteVariable(StoryVariable variable) { if (variable != null) GlobalVariables.Remove(variable); }

        [RelayCommand]
        public void AddAction()
        {
            if (SelectedNode == null) return;
            string defaultVar = GlobalVariables.FirstOrDefault()?.Name ?? "";
            SelectedNode.OnEnterActions.Add(new StoryNodeAction { TargetVariableName = defaultVar, Operation = ActionOperation.Set, Value = "True" });
        }

        [RelayCommand]
        public void DeleteAction(StoryNodeAction action) { if (SelectedNode != null && action != null) SelectedNode.OnEnterActions.Remove(action); }

        // Asset Selection
        [RelayCommand] public void SelectBackgroundImage() { if (SelectedNode == null) return; BrowseFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp", path => SelectedNode.BackgroundImagePath = path); }
        [RelayCommand] public void SelectCharacterImage() { if (SelectedNode == null) return; BrowseFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp", path => SelectedNode.CharacterImagePath = path); }
        [RelayCommand] public void SelectAudio() { if (SelectedNode == null) return; BrowseFile("Audio|*.mp3;*.wav;*.ogg;*.m4a", path => SelectedNode.AudioPath = path); }

        private void BrowseFile(string filter, Action<string> onPathSelected)
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = filter, Title = "Select Asset" };
            if (openDialog.ShowDialog() == true) onPathSelected(openDialog.FileName);
        }
    }
}