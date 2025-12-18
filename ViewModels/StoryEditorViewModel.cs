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
using SpriteEditor.Data.Story; // StoryCommand, SetVariableCommand və s. buradadır
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    // Xəttin çəkilməsi üçün köməkçi klass (Dəyişməyib)
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
        // === UI Panel İdarəetməsi ===
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

        // === Zoom və Pan ===
        [ObservableProperty] private double _zoomLevel = 1.0;
        [ObservableProperty] private double _panX = 0;
        [ObservableProperty] private double _panY = 0;

        public const double MinZoom = 0.2;
        public const double MaxZoom = 3.0;

        // === Kolleksiyalar ===
        public ObservableCollection<StoryNode> Nodes { get; } = new();
        public ObservableCollection<NodeConnection> Connections { get; } = new();

        [ObservableProperty] private StoryNode _selectedNode;

        // View üçün siyahılar
        public List<StoryNodeType> NodeTypes { get; } = Enum.GetValues(typeof(StoryNodeType)).Cast<StoryNodeType>().ToList();
        public ObservableCollection<StoryVariable> GlobalVariables { get; } = new();
        public ObservableCollection<StoryCharacter> Characters { get; } = new();
        public List<VariableType> VariableTypes { get; } = Enum.GetValues(typeof(VariableType)).Cast<VariableType>().ToList();
        public List<ConditionOperator> ConditionOperators { get; } = Enum.GetValues(typeof(ConditionOperator)).Cast<ConditionOperator>().ToList();
        public List<ActionOperation> ActionOperations { get; } = Enum.GetValues(typeof(ActionOperation)).Cast<ActionOperation>().ToList();
        public List<PortraitPosition> PortraitPositions { get; } = Enum.GetValues(typeof(PortraitPosition)).Cast<PortraitPosition>().ToList();
        public List<PortraitAnimation> PortraitAnimations { get; } = Enum.GetValues(typeof(PortraitAnimation)).Cast<PortraitAnimation>().ToList();

        // === Connection Dragging ===
        [ObservableProperty] private Point _tempConnectionStart;
        [ObservableProperty] private Point _tempConnectionEnd;
        [ObservableProperty] private bool _isDraggingConnection;
        private StoryNode _dragSourceNode;

        public StoryEditorViewModel()
        {
            ZoomLevel = 1.0;
            PanX = 0;
            PanY = 0;

            // Test Node (Start)
            var n1 = new StoryNode { Title = "Start", Type = StoryNodeType.Start, IsStartNode = true, SpeakerName = "System", Text = "Game Starts", X = 100, Y = 100 };
            Nodes.Add(n1);
        }

        // === DÜYÜN İDARƏETMƏSİ ===
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

            // Əlaqələri təmizlə
            var linksToRemove = Connections.Where(c => c.Source == SelectedNode || c.Target == SelectedNode).ToList();
            foreach (var link in linksToRemove) Connections.Remove(link);

            // Digər düyünlərdən bura gələn seçimləri təmizlə
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
            nodeToSet.Type = StoryNodeType.Start;
        }

        // === YENİ: COMMAND SYSTEM (Block Based Logic) ===

        // 1. Dəyişən Dəyişmək Əmri (Set Variable)
        [RelayCommand]
        public void AddSetVariableCommand()
        {
            if (SelectedNode == null) return;
            string defaultVar = GlobalVariables.FirstOrDefault()?.Name ?? "";

            // StoryNode daxilindəki Commands siyahısına əlavə edirik
            SelectedNode.Commands.Add(new SetVariableCommand
            {
                TargetVariableName = defaultVar,
                Operation = ActionOperation.Set,
                Value = "True"
            });
        }

        // 2. Gözləmə Əmri (Wait)
        [RelayCommand]
        public void AddWaitCommand()
        {
            if (SelectedNode == null) return;
            SelectedNode.Commands.Add(new WaitCommand { DurationSeconds = 1.0 });
        }

        // 3. Səs Əmri (Sound)
        [RelayCommand]
        public void AddSoundCommand()
        {
            if (SelectedNode == null) return;
            SelectedNode.Commands.Add(new PlaySoundCommand { AudioPath = "", Volume = 1.0f });
        }

        // 4. Əmri Silmək (Generic)
        [RelayCommand]
        public void DeleteCommand(StoryCommand command)
        {
            if (SelectedNode != null && command != null)
            {
                SelectedNode.Commands.Remove(command);
            }
        }

        // 5. Portret Göstər Əmri
        [RelayCommand]
        public void AddShowPortraitCommand()
        {
            if (SelectedNode == null) return;
            string defaultCharacterId = Characters.FirstOrDefault()?.Id ?? "";

            SelectedNode.Commands.Add(new ShowPortraitCommand
            {
                CharacterId = defaultCharacterId,
                PortraitName = "neutral",
                Position = PortraitPosition.Center,
                Animation = PortraitAnimation.FadeIn
            });
        }

        // 6. Portret Gizlət Əmri
        [RelayCommand]
        public void AddHidePortraitCommand()
        {
            if (SelectedNode == null) return;
            string defaultCharacterId = Characters.FirstOrDefault()?.Id ?? "";

            SelectedNode.Commands.Add(new HidePortraitCommand
            {
                CharacterId = defaultCharacterId,
                Animation = PortraitAnimation.FadeIn
            });
        }

        // 7. Mətn Göstər Əmri (Narrator)
        [RelayCommand]
        public void AddShowTextCommand()
        {
            if (SelectedNode == null) return;
            SelectedNode.Commands.Add(new ShowTextCommand { Text = "Narrator text here...", DisplayDuration = 2.0 });
        }

        // === DƏYİŞƏN İDARƏETMƏSİ ===
        [RelayCommand]
        public void AddVariable() => GlobalVariables.Add(new StoryVariable { Name = "Variable_" + (GlobalVariables.Count + 1) });

        [RelayCommand]
        public void DeleteVariable(StoryVariable variable) { if (variable != null) GlobalVariables.Remove(variable); }

        // === PERSONAJ İDARƏETMƏSİ ===
        [ObservableProperty] private StoryCharacter _selectedCharacter;
        [ObservableProperty] private CharacterPortrait _selectedPortrait;

        [RelayCommand]
        public void AddCharacter()
        {
            var character = new StoryCharacter { Name = "Character " + (Characters.Count + 1) };
            // Default portret əlavə et
            character.Portraits.Add(new CharacterPortrait { Name = "neutral" });
            Characters.Add(character);
            SelectedCharacter = character;
        }

        [RelayCommand]
        public void DeleteCharacter(StoryCharacter character)
        {
            if (character != null)
            {
                Characters.Remove(character);
                if (SelectedCharacter == character) SelectedCharacter = null;
            }
        }

        [RelayCommand]
        public void AddPortrait()
        {
            if (SelectedCharacter == null) return;
            var portrait = new CharacterPortrait { Name = "emotion_" + (SelectedCharacter.Portraits.Count + 1) };
            SelectedCharacter.Portraits.Add(portrait);
        }

        [RelayCommand]
        public void DeletePortrait(CharacterPortrait portrait)
        {
            if (SelectedCharacter != null && portrait != null)
            {
                SelectedCharacter.Portraits.Remove(portrait);
            }
        }

        [RelayCommand]
        public void SelectPortraitImage()
        {
            if (SelectedPortrait == null) return;
            BrowseFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp", path => SelectedPortrait.ImagePath = path);
        }


        // === VISUAL SCRIPTING LOGIC (Connection Dragging) ===
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

        // === PLAYER & SAVE/LOAD ===
        [RelayCommand]
        public void PlayStory()
        {
            if (Nodes.Count == 0) { MessageBox.Show("Hekayə boşdur!", "Xəbərdarlıq"); return; }

            var storyGraph = new StoryGraph
            {
                Nodes = Nodes.ToList(),
                StartNodeId = Nodes.FirstOrDefault(n => n.IsStartNode)?.Id ?? Nodes.FirstOrDefault()?.Id,
                Variables = GlobalVariables.ToList(),
                Characters = Characters.ToList()
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
                Variables = GlobalVariables.ToList(),
                Characters = Characters.ToList()
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
                    // Artıq polimorfik tiplər [JsonDerivedType] atributları sayəsində avtomatik serializasiya olunacaq
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
                    Characters.Clear();

                    if (storyGraph.Variables != null)
                        foreach (var v in storyGraph.Variables) GlobalVariables.Add(v);

                    if (storyGraph.Characters != null)
                        foreach (var c in storyGraph.Characters) Characters.Add(c);

                    foreach (var node in storyGraph.Nodes)
                    {
                        node.IsStartNode = (node.Id == storyGraph.StartNodeId);
                        Nodes.Add(node);
                    }

                    // Əlaqələri bərpa et
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

        // === ASSET SELECTION ===
        [RelayCommand] public void SelectBackgroundImage() { if (SelectedNode == null) return; BrowseFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp", path => SelectedNode.BackgroundImagePath = path); }
        [RelayCommand] public void SelectCharacterImage() { if (SelectedNode == null) return; BrowseFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp", path => SelectedNode.CharacterImagePath = path); }
        [RelayCommand] public void SelectAudio() { if (SelectedNode == null) return; BrowseFile("Audio|*.mp3;*.wav;*.ogg;*.m4a", path => SelectedNode.AudioPath = path); }

        private void BrowseFile(string filter, Action<string> onPathSelected)
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = filter, Title = "Select Asset" };
            if (openDialog.ShowDialog() == true) onPathSelected(openDialog.FileName);
        }

        // === SAMPLE STORY (DEMO) ===
        [RelayCommand]
        public void LoadSampleStory()
        {
            // Təmizlə
            Nodes.Clear();
            Connections.Clear();
            GlobalVariables.Clear();
            Characters.Clear();

            // 1. CHARACTERS YARADıRıQ
            var alice = new StoryCharacter
            {
                Name = "Alice",
                DisplayColor = "#3B82F6", // Mavi
                Description = "A cheerful young woman"
            };
            alice.Portraits.Add(new CharacterPortrait { Name = "neutral", ImagePath = "" });
            alice.Portraits.Add(new CharacterPortrait { Name = "happy", ImagePath = "" });
            alice.Portraits.Add(new CharacterPortrait { Name = "sad", ImagePath = "" });
            Characters.Add(alice);

            var bob = new StoryCharacter
            {
                Name = "Bob",
                DisplayColor = "#EF4444", // Qırmızı
                Description = "Alice's old friend"
            };
            bob.Portraits.Add(new CharacterPortrait { Name = "neutral", ImagePath = "" });
            bob.Portraits.Add(new CharacterPortrait { Name = "tired", ImagePath = "" });
            bob.Portraits.Add(new CharacterPortrait { Name = "surprised", ImagePath = "" });
            Characters.Add(bob);

            // 2. VARİABLES
            GlobalVariables.Add(new StoryVariable { Name = "metBob", Type = VariableType.Boolean, Value = "False" });
            GlobalVariables.Add(new StoryVariable { Name = "friendship", Type = VariableType.Integer, Value = "0" });

            // 3. NODES YARADıRıQ

            // Node 1: Start
            var node1 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Start",
                Type = StoryNodeType.Start,
                IsStartNode = true,
                SpeakerName = "Narrator",
                Text = "You wake up on a beautiful sunny morning. Today feels special...",
                X = 100,
                Y = 100
            };
            Nodes.Add(node1);

            // Node 2: Meet Alice
            var node2 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Meet Alice",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "Alice",
                Text = "Good morning! Isn't it a wonderful day?",
                X = 100,
                Y = 250
            };
            // Alice portretini göstər (happy)
            node2.Commands.Add(new ShowPortraitCommand
            {
                CharacterId = alice.Id,
                PortraitName = "happy",
                Position = PortraitPosition.Center,
                Animation = PortraitAnimation.FadeIn
            });
            Nodes.Add(node2);

            // Node 3: Player response choice
            var node3 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Response Choice",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "Alice",
                Text = "I'm heading to the park. Want to come along?",
                X = 100,
                Y = 400
            };
            Nodes.Add(node3);

            // Node 4: Accept invitation
            var node4 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Accept",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "You",
                Text = "Sure! I'd love to join you.",
                X = -50,
                Y = 550
            };
            // Friendship artır
            node4.Commands.Add(new SetVariableCommand
            {
                TargetVariableName = "friendship",
                Operation = ActionOperation.Add,
                Value = "10"
            });
            Nodes.Add(node4);

            // Node 5: Decline invitation
            var node5 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Decline",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "You",
                Text = "Sorry, I have things to do today.",
                X = 250,
                Y = 550
            };
            // Alice kədərlənir
            node5.Commands.Add(new ShowPortraitCommand
            {
                CharacterId = alice.Id,
                PortraitName = "sad",
                Position = PortraitPosition.Center
            });
            Nodes.Add(node5);

            // Node 6: At the park (after accepting)
            var node6 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "At Park",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "Alice",
                Text = "Look! There's Bob! I haven't seen him in ages!",
                X = -50,
                Y = 700
            };
            // Alice-i sola çək, Bob-u sağa əlavə et
            node6.Commands.Add(new ShowPortraitCommand
            {
                CharacterId = alice.Id,
                PortraitName = "happy",
                Position = PortraitPosition.Left
            });
            node6.Commands.Add(new WaitCommand { DurationSeconds = 0.5 });
            node6.Commands.Add(new ShowPortraitCommand
            {
                CharacterId = bob.Id,
                PortraitName = "surprised",
                Position = PortraitPosition.Right,
                Animation = PortraitAnimation.SlideFromRight
            });
            Nodes.Add(node6);

            // Node 7: Bob speaks
            var node7 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Bob Greeting",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "Bob",
                Text = "Alice! What a surprise! And who's your friend?",
                X = -50,
                Y = 850
            };
            // metBob dəyişənini True-ya çevir
            node7.Commands.Add(new SetVariableCommand
            {
                TargetVariableName = "metBob",
                Operation = ActionOperation.Set,
                Value = "True"
            });
            Nodes.Add(node7);

            // Node 8: Introductions
            var node8 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Introductions",
                Type = StoryNodeType.Dialogue,
                SpeakerName = "Alice",
                Text = "This is my new friend! We just met today.",
                X = -50,
                Y = 1000
            };
            Nodes.Add(node8);

            // Node 9: Happy ending (if accepted)
            var node9 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Happy Ending",
                Type = StoryNodeType.End,
                SpeakerName = "Narrator",
                Text = "You made two wonderful friends today. This is the beginning of a great adventure!",
                X = -50,
                Y = 1150
            };
            // Portretləri gizlət
            node9.Commands.Add(new HidePortraitCommand { CharacterId = alice.Id });
            node9.Commands.Add(new HidePortraitCommand { CharacterId = bob.Id });
            Nodes.Add(node9);

            // Node 10: Sad ending (if declined)
            var node10 = new StoryNode
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Sad Ending",
                Type = StoryNodeType.End,
                SpeakerName = "Narrator",
                Text = "Sometimes we miss out on great opportunities by being too busy...",
                X = 250,
                Y = 700
            };
            node10.Commands.Add(new HidePortraitCommand { CharacterId = alice.Id });
            Nodes.Add(node10);

            // 4. ELAQƏLƏRİ YARADıRıQ

            // Start → Meet Alice
            node1.Choices.Add(new StoryChoice { Text = "Continue", TargetNodeId = node2.Id });
            Connections.Add(new NodeConnection { Source = node1, Target = node2 });

            // Meet Alice → Response Choice
            node2.Choices.Add(new StoryChoice { Text = "Continue", TargetNodeId = node3.Id });
            Connections.Add(new NodeConnection { Source = node2, Target = node3 });

            // Response Choice → Accept OR Decline
            node3.Choices.Add(new StoryChoice { Text = "Sure! I'd love to.", TargetNodeId = node4.Id });
            node3.Choices.Add(new StoryChoice { Text = "Sorry, I'm busy.", TargetNodeId = node5.Id });
            Connections.Add(new NodeConnection { Source = node3, Target = node4 });
            Connections.Add(new NodeConnection { Source = node3, Target = node5 });

            // Accept → At Park
            node4.Choices.Add(new StoryChoice { Text = "Go to park", TargetNodeId = node6.Id });
            Connections.Add(new NodeConnection { Source = node4, Target = node6 });

            // At Park → Bob Greeting
            node6.Choices.Add(new StoryChoice { Text = "Wave at Bob", TargetNodeId = node7.Id });
            Connections.Add(new NodeConnection { Source = node6, Target = node7 });

            // Bob Greeting → Introductions
            node7.Choices.Add(new StoryChoice { Text = "Say hello", TargetNodeId = node8.Id });
            Connections.Add(new NodeConnection { Source = node7, Target = node8 });

            // Introductions → Happy Ending
            node8.Choices.Add(new StoryChoice { Text = "Continue", TargetNodeId = node9.Id });
            Connections.Add(new NodeConnection { Source = node8, Target = node9 });

            // Decline → Sad Ending
            node5.Choices.Add(new StoryChoice { Text = "Go home", TargetNodeId = node10.Id });
            Connections.Add(new NodeConnection { Source = node5, Target = node10 });

            CustomMessageBox.Show(
                "Demo hekayə yükləndi!\n\n" +
                "2 Personaj: Alice və Bob\n" +
                "10 Node: 2 fərqli son\n" +
                "Əmrlər: Portrait göstərmə, Variables, Wait\n\n" +
                "▶ Play Preview basaraq test edin!",
                "Sample Story", 
                MessageBoxButton.OK, 
                MsgImage.Success
            );
        }
    }
}