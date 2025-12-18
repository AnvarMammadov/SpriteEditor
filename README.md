# 🎨 Sprite Editor Pro

> **Professional 2D Game Development Toolkit** - Complete sprite manipulation, rigging, and animation solution for game developers and artists.

![Version](https://img.shields.io/badge/version-1.0.0%20Beta-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![License](https://img.shields.io/badge/license-Commercial-orange)

---

## ✨ Features

### 🔪 **Sprite Slicer**
- **Grid-based slicing** - Define custom rows and columns
- **Auto-detection** - Intelligent sprite boundary detection
- **Batch export** - Save all sprites at once
- **Adjustable precision** - Fine-tune detection parameters

### 🎭 **Background Eraser**
- **Magic wand tool** - Color-based background removal
- **Manual brush eraser** - Precise pixel-level control
- **Adjustable tolerance** - Control color matching sensitivity
- **Real-time preview** - See results before saving

### 🦴 **Rigging & Skinning**
- **2D Skeleton system** - Create bone hierarchies
- **Automatic mesh generation** - AI-powered vertex placement
- **Weight painting** - Advanced auto-weighting with customizable parameters
- **Mesh triangulation** - Automatic Delaunay triangulation
- **Pose mode** - Test animations with real-time deformation
- **Animation timeline** - Keyframe-based animation system
- **Export rig data** - Save/load `.rig.json` format

### 🔄 **Format Converter**
- **Multiple formats** - PNG, JPG, BMP, ICO, WebP
- **Batch conversion** - Convert multiple files at once
- **Quality presets** - Optimized settings for each format
- **Smart resizing** - Automatic size adjustments for ICO

### 🎬 **Frame Animator**
- **Sprite sheet playback** - Test frame-by-frame animations
- **Adjustable FPS** - Control animation speed
- **Loop mode** - Continuous playback
- **GIF export** - Create animated GIFs

### 📦 **Texture Packer**
- **Atlas generation** - Combine multiple sprites into one texture
- **Smart packing** - Efficient space utilization
- **Configurable padding** - Control sprite spacing
- **JSON metadata** - Export sprite coordinates
- **Transparency trimming** - Auto-crop transparent pixels

### 📖 **Story Editor** (Visual Novel)
- **Node-based editor** - Visual narrative graph
- **Branching dialogues** - Multiple choice support
- **Variable system** - Track game state
- **Conditional logic** - Dynamic story flow
- **Asset integration** - Add backgrounds, characters, audio

---

## 🌍 Language Support

- 🇺🇸 **English**
- 🇦🇿 **Azərbaycan**
- 🇷🇺 **Русский**
- 🇹🇷 **Türkçe**
- 🇪🇸 **Español**
- 🇧🇷 **Português**
- 🇨🇳 **中文 (简体)**

---

## 🚀 Getting Started

### System Requirements

- **OS**: Windows 10/11 (64-bit)
- **Framework**: .NET 8.0 Runtime
- **RAM**: 4 GB minimum (8 GB recommended)
- **Storage**: 200 MB available space
- **Graphics**: DirectX 11 compatible GPU

### Installation

1. Download the latest release from [Releases](https://github.com/yourusername/SpriteEditor/releases)
2. Run `SpriteEditorSetup.exe`
3. Follow the installation wizard
4. Launch **Sprite Editor Pro** from Start Menu

### Quick Start

1. **Open the app** and select a tool from the sidebar
2. **Load an image** using the "Load Image" button
3. **Apply your edits** using the tool-specific controls
4. **Export your work** with the "Save" or "Export" button

---

## 📚 Documentation

### Sprite Slicer
```
1. Load a spritesheet image
2. Choose mode: Grid-based or Auto-detect
3. Adjust parameters (rows/columns or tolerance)
4. Click "Slice Sprites" and choose output folder
```

### Rigging System
```
1. Load a sprite
2. Create bones in "Create Bone" mode
3. Add mesh vertices in "Edit Mesh" mode
4. Auto-triangulate the mesh
5. Apply auto-weights
6. Test in "Pose" mode
7. Save rig data as .rig.json
```

### Texture Packer
```
1. Load multiple sprite files
2. Set atlas size (512, 1024, 2048, 4096)
3. Adjust padding between sprites
4. Click "Pack Atlas"
5. Export PNG + JSON metadata
```

---

## 🛠️ Built With

- **[.NET 8](https://dotnet.microsoft.com/)** - Application framework
- **[WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)** - UI framework
- **[SkiaSharp](https://github.com/mono/SkiaSharp)** - 2D graphics rendering
- **[ImageSharp](https://sixlabors.com/products/imagesharp/)** - Image processing
- **[Magick.NET](https://github.com/dlemstra/Magick.NET)** - Advanced image manipulation
- **[Triangle.NET](https://github.com/garykac/triangle.net)** - Mesh triangulation
- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)** - MVVM framework

---

## 🎯 Use Cases

### Game Developers
- Prepare sprite assets for Unity, Godot, Unreal
- Create character rigs for 2D animation
- Generate texture atlases for optimized performance

### Pixel Artists
- Clean up sprite sheets
- Remove backgrounds efficiently
- Test animations before exporting

### Visual Novel Creators
- Build interactive narratives
- Manage character dialogues
- Create branching story paths

### Indie Studios
- All-in-one sprite workflow
- No need for multiple tools
- Fast iteration and prototyping

---

## 📦 Project Structure

```
SpriteEditor/
├── App.xaml                    # Application entry point
├── MainWindow.xaml             # Main UI shell
├── ViewModels/                 # Business logic (MVVM)
│   ├── MainViewModel.cs
│   ├── SpriteSlicerViewModel.cs
│   ├── RiggingViewModel.cs
│   ├── TexturePackerViewModel.cs
│   └── ...
├── Views/                      # UI components
│   ├── HomeView.xaml
│   ├── SpriteSlicerView.xaml
│   ├── RiggingView.xaml
│   └── ...
├── Services/                   # Core services
│   ├── ImageService.cs
│   └── TexturePackerService.cs
├── Data/                       # Data models
│   ├── RigData.cs
│   ├── MeshData.cs
│   └── ...
├── Resources/                  # Localization
│   └── Languages/
│       ├── Lang.en-US.xaml
│       ├── Lang.az-AZ.xaml
│       └── ...
└── Converters/                 # XAML value converters
```

---

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📝 License

This project is licensed under a **Commercial License**.  
For licensing inquiries, please contact: [your-email@domain.com](mailto:your-email@domain.com)

**Free Trial**: 30-day full-featured trial available  
**Purchase**: [Buy License](https://yourwebsite.com/buy)

---

## 🐛 Known Issues

- Large images (>8K resolution) may experience performance slowdown in Rigging mode
- GIF export is limited to 256 colors
- ICO format supports up to 256x256 resolution

---

## 🗓️ Roadmap

### Version 1.1 (Q2 2025)
- [ ] Undo/Redo system
- [ ] Drag & Drop support
- [ ] Auto-save functionality
- [ ] Performance optimizations

### Version 1.2 (Q3 2025)
- [ ] Plugin system
- [ ] Batch processing mode
- [ ] Cloud storage integration
- [ ] Collaborative editing

### Version 2.0 (Q4 2025)
- [ ] 3D sprite support
- [ ] AI-powered auto-rigging
- [ ] Real-time collaboration
- [ ] Mobile companion app

---

## 📧 Support

- **Email**: support@spriteeditorpro.com
- **Discord**: [Join our community](https://discord.gg/spriteeditor)
- **Documentation**: [docs.spriteeditorpro.com](https://docs.spriteeditorpro.com)
- **Bug Reports**: [GitHub Issues](https://github.com/yourusername/SpriteEditor/issues)

---

## 🌟 Showcase

*Add screenshots and GIFs here showcasing your app in action*

---

## 💖 Acknowledgments

- Thanks to all contributors and beta testers
- Inspired by Spine, Aseprite, and TexturePacker
- Built with passion for the game development community

---

<p align="center">
  <sub>Made with ❤️ by [Your Name/Studio]</sub>
</p>

