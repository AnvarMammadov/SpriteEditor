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


        [ObservableProperty]
        private bool _isToolsPanelOpen = true;

        [ObservableProperty]
        private bool _isPropertiesPanelOpen = false;

        [RelayCommand]
        public void ToggleToolsPanel()
        {
            IsToolsPanelOpen = !IsToolsPanelOpen;
        }

        [RelayCommand]
        public void TogglePropertiesPanel()
        {
            IsPropertiesPanelOpen = !IsPropertiesPanelOpen;
        }

        partial void OnSelectedNodeChanged(StoryNode value)
        {
            // Əgər bir Node seçilibsə, Sağ Paneli avtomatik aç
            if (value != null)
            {
                IsPropertiesPanelOpen = true;
            }
        }


        // === YENİ: Zoom və Pan üçün dəyişənlər ===
        [ObservableProperty] private double _zoomLevel = 1.0;
        [ObservableProperty] private double _panX = 0;
        [ObservableProperty] private double _panY = 0;

        // Zoom üçün minimum və maksimum hədlər
        public const double MinZoom = 0.2;
        public const double MaxZoom = 3.0;

        public ObservableCollection<StoryNode> Nodes { get; } = new();

        // Ekranda görünən xətlər
        public ObservableCollection<NodeConnection> Connections { get; } = new();

        [ObservableProperty] private StoryNode _selectedNode;

        // Müvəqqəti xətt çəkmək üçün (Sürükləmə zamanı)
        [ObservableProperty] private Point _tempConnectionStart;
        [ObservableProperty] private Point _tempConnectionEnd;
        [ObservableProperty] private bool _isDraggingConnection;

        private StoryNode _dragSourceNode; // Hansı node-dan xətt çəkməyə başladıq?


        public ObservableCollection<StoryVariable> GlobalVariables { get; } = new();
        public List<VariableType> VariableTypes { get; } = Enum.GetValues(typeof(VariableType)).Cast<VariableType>().ToList();
        public List<ConditionOperator> ConditionOperators { get; } = Enum.GetValues(typeof(ConditionOperator)).Cast<ConditionOperator>().ToList();
        public List<ActionOperation> ActionOperations { get; } = Enum.GetValues(typeof(ActionOperation)).Cast<ActionOperation>().ToList();

        public StoryEditorViewModel()
        {
            ZoomLevel = 1.0;    
            PanX = 0;   
            PanY = 0;
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
                // Ekranın ortasında yaranması üçün:
                X = (-PanX + 250) / ZoomLevel,
                Y = (-PanY + 250) / ZoomLevel
            });
        }

        [RelayCommand]
        public void DeleteNode()
        {
            if (SelectedNode == null) return;

            // 1. Vizual Əlaqələri (Xətləri) təmizlə
            var linksToRemove = Connections.Where(c => c.Source == SelectedNode || c.Target == SelectedNode).ToList();
            foreach (var link in linksToRemove) Connections.Remove(link);

            // 2. Digər node-ların içindən bu node-a gedən seçimləri (Button-ları) sil
            foreach (var node in Nodes)
            {
                // RemoveAll əvəzinə bu üsuldan istifadə edirik:
                var choicesToRemove = node.Choices.Where(c => c.TargetNodeId == SelectedNode.Id).ToList();
                foreach (var choice in choicesToRemove)
                {
                    node.Choices.Remove(choice);
                }
            }

            // 3. Node-un özünü sil
            Nodes.Remove(SelectedNode);
            SelectedNode = null;
        }


        [RelayCommand]
        public void DeleteLink(StoryChoice choice)
        {
            // Əgər seçim edilməyibsə və ya choice boşdursa, dayan
            if (SelectedNode == null || choice == null) return;

            // 1. Vizual əlaqəni (Xətti) tapıb silirik
            // Məntiq: Mənbəyi "SelectedNode" olan və Hədəfi silinən seçimin "TargetNodeId"-si olan xətti tap
            var connectionToRemove = Connections.FirstOrDefault(c => c.Source == SelectedNode && c.Target.Id == choice.TargetNodeId);

            if (connectionToRemove != null)
            {
                Connections.Remove(connectionToRemove);
            }

            // 2. Məlumatı (Seçimi) siyahıdan silirik
            // Bu, düyünün "Choices" siyahısından həmin düyməni ləğv edir
            SelectedNode.Choices.Remove(choice);
        }


        [RelayCommand]
        public void SetAsStartNode(StoryNode targetNode) // Parametr əlavə olundu
        {
            // Əgər parametr gəlməyibsə, SelectedNode-u yoxla
            var nodeToSet = targetNode ?? SelectedNode;

            if (nodeToSet == null) return;

            // 1. Hamısından statusu al
            foreach (var node in Nodes)
            {
                node.IsStartNode = false;
            }

            // 2. Hədəf düyünü Start et
            nodeToSet.IsStartNode = true;
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

        // === YENİ ƏLAVƏ: Play Preview Komandası ===
        [RelayCommand]
        public void PlayStory()
        {
            if (Nodes.Count == 0)
            {
                MessageBox.Show("Hekayə boşdur!", "Xəbərdarlıq");
                return;
            }

            var storyGraph = new StoryGraph
            {
                Nodes = Nodes.ToList(),
                // Başlanğıc Node
                StartNodeId = Nodes.FirstOrDefault(n => n.IsStartNode)?.Id ?? Nodes.FirstOrDefault()?.Id,

                // !!! VACİB: Dəyişənləri də bura əlavə etməliydik !!!
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
            // 1. Yadda saxlanacaq əsas obyekti (StoryGraph) yaradırıq
            var storyGraph = new StoryGraph
            {
                Nodes = Nodes.ToList(),
                // StartNodeId hələlik ilk düyün və ya xüsusi seçilmiş düyün ola bilər
                StartNodeId = Nodes.FirstOrDefault(n => n.IsStartNode)?.Id ?? Nodes.FirstOrDefault()?.Id,
                Variables = GlobalVariables.ToList()
            };

            // 2. "Save File" pəncərəsini açırıq
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
                    // 3. JSON Serializasiyası (Indented = oxunaqlı format)
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(storyGraph, options);

                    // 4. Faylı fiziki olaraq yazırıq
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);

                    // Uğurlu mesajı
                    CustomMessageBox.Show("Hekayə uğurla yadda saxlanıldı!", "Uğurlu", MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Yadda saxlama zamanı xəta: {ex.Message}", "Xəta", MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task LoadStory()
        {
            // 1. "Open File" pəncərəsini açırıq
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Story JSON (*.story.json)|*.story.json",
                Title = "Hekayəni Yüklə"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    // 2. Faylı oxuyuruq
                    string jsonString = await File.ReadAllTextAsync(openDialog.FileName);

                    // 3. JSON-u obyektə çeviririk (Deserialize)
                    var storyGraph = JsonSerializer.Deserialize<StoryGraph>(jsonString);

                    if (storyGraph == null) return;

                    // 4. Mövcud səhnəni təmizləyirik (Köhnə düyünləri silirik)
                    Nodes.Clear();
                    Connections.Clear(); // Vizual xətləri də silirik
                    GlobalVariables.Clear();

                    if (storyGraph.Variables != null)
                    {
                        foreach (var v in storyGraph.Variables)
                        {
                            GlobalVariables.Add(v);
                        }
                    }

                    // 5. Düyünləri bərpa edirik
                    foreach (var node in storyGraph.Nodes)
                    {
                        // Əgər bu düyünün ID-si qrafın StartNodeId-si ilə eynidirsə,
                        // onun bayrağını qaldırırıq (IsStartNode = true).
                        if (node.Id == storyGraph.StartNodeId)
                        {
                            node.IsStartNode = true;
                        }
                        else
                        {
                            node.IsStartNode = false;
                        }

                        Nodes.Add(node);
                    }

                    // 6. Əlaqələri (Vizual Xətləri) bərpa edirik
                    // QEYD: JSON-da yalnız "TargetNodeId" var. Biz bunu tapıb vizual xəttə (Connection) çevirməliyik.
                    foreach (var sourceNode in Nodes)
                    {
                        foreach (var choice in sourceNode.Choices)
                        {
                            if (!string.IsNullOrEmpty(choice.TargetNodeId))
                            {
                                // Hədəf düyünü ID-sinə görə tapırıq
                                var targetNode = Nodes.FirstOrDefault(n => n.Id == choice.TargetNodeId);

                                if (targetNode != null)
                                {
                                    // Vizual əlaqəni yaradırıq
                                    Connections.Add(new NodeConnection
                                    {
                                        Source = sourceNode,
                                        Target = targetNode
                                    });
                                }
                            }
                        }
                    }

                    CustomMessageBox.Show("Hekayə uğurla yükləndi.", "Tamamlandı", MessageBoxButton.OK, MsgImage.Info);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Yükləmə xətası: {ex.Message}", "Xəta", MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }



        [RelayCommand]
        public void AddVariable()
        {
            // Yeni dəyişən yaradanda adının təkrar olmamasını yoxlaya bilərik, amma hələlik sadə edək
            GlobalVariables.Add(new StoryVariable { Name = "Variable_" + (GlobalVariables.Count + 1) });
        }

        [RelayCommand]
        public void DeleteVariable(StoryVariable variable)
        {
            if (variable != null)
            {
                GlobalVariables.Remove(variable);
            }
        }

        [RelayCommand]
        public void AddAction()
        {
            if (SelectedNode == null) return;

            // Yeni hadisə yaradanda, ilk dəyişəni (əgər varsa) default olaraq seçək ki, boş qalmasın
            string defaultVar = GlobalVariables.FirstOrDefault()?.Name ?? "";

            var newAction = new StoryNodeAction
            {
                TargetVariableName = defaultVar,
                Operation = ActionOperation.Set,
                Value = "True" // Başlanğıc dəyər
            };

            SelectedNode.OnEnterActions.Add(newAction);
        }

        [RelayCommand]
        public void DeleteAction(StoryNodeAction action)
        {
            if (SelectedNode != null && action != null)
            {
                SelectedNode.OnEnterActions.Remove(action);
            }
        }

    }
}
