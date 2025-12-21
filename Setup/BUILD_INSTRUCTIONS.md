# Installer Build Instructions

## Prerequisites

1. **Inno Setup 6.x**
   - Download from: https://jrsoftware.org/isdl.php
   - Install with default settings

2. **.NET 8.0 SDK**
   - Required to build the application
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0

## Steps to Build Installer

### 1. Build the Application (Release Mode)

```bash
cd /path/to/SpriteEditor
dotnet publish -c Release -r win-x64 --self-contained false
```

This creates the release build in: `bin/Release/net8.0-windows/`

### 2. Prepare Assets

Ensure these files exist:
- `Resources/AppIcon.ico` - Application icon
- `Setup/installer-image.bmp` - Large installer image (164x314 px)
- `Setup/installer-icon.bmp` - Small installer icon (55x58 px)
- `LICENSE.txt` - License file
- `README.md` - Documentation

### 3. Compile the Installer

**Option A: Using Inno Setup GUI**
1. Open Inno Setup Compiler
2. File → Open → Select `Setup/SpriteEditorSetup.iss`
3. Build → Compile
4. The installer will be created in `bin/Setup/`

**Option B: Using Command Line**
```bash
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Setup/SpriteEditorSetup.iss
```

### 4. Test the Installer

1. Run the generated `SpriteEditorProSetup-v1.0.0.exe`
2. Test installation in a clean environment (VM recommended)
3. Verify:
   - Application launches correctly
   - File associations work (.rig.json)
   - Desktop shortcut created (if selected)
   - Uninstall works properly

## Installer Output

The compiled installer will be located at:
```
bin/Setup/SpriteEditorProSetup-v1.0.0.exe
```

## Customization

### Changing Version Number

Edit in `SpriteEditorSetup.iss`:
```inno
#define MyAppVersion "1.0.0"
```

### Adding/Removing Languages

Edit the `[Languages]` section in `.iss` file.

### Modifying File Associations

Edit the `[Registry]` section for custom file types.

### Custom Install Screens

Add to `[Messages]` and use custom `WelcomeLabel`, `FinishedLabel`, etc.

## Code Signing (Optional but Recommended)

To digitally sign the installer:

```bash
signtool sign /f "YourCertificate.pfx" /p "password" /t http://timestamp.digicert.com "bin/Setup/SpriteEditorProSetup-v1.0.0.exe"
```

## Distribution

### Recommended Checksums

After building, generate checksums for verification:

```bash
# PowerShell
Get-FileHash -Algorithm SHA256 "bin/Setup/SpriteEditorProSetup-v1.0.0.exe"
```

Include the SHA256 hash on your download page.

### Upload Locations

- Your website: https://spriteeditorpro.com/download
- GitHub Releases: https://github.com/yourusername/SpriteEditor/releases
- Alternative: Itch.io, Steam, Microsoft Store

## Troubleshooting

### "Missing .NET Runtime" Error
- Ensure the installer checks for .NET 8.0 Desktop Runtime
- The `[Code]` section handles this automatically
- Users will be redirected to download if missing

### Files Not Included
- Check `[Files]` section paths
- Ensure relative paths are correct
- Use `recursesubdirs` flag for directories

### Installer Won't Run on Target Machine
- Verify architecture (x64 only)
- Check Windows version (10.0.17763 minimum)
- Ensure admin privileges

## Automated Build (CI/CD)

For GitHub Actions or similar:

```yaml
- name: Build Installer
  run: |
    dotnet publish -c Release
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Setup/SpriteEditorSetup.iss
    
- name: Upload Artifact
  uses: actions/upload-artifact@v3
  with:
    name: SpriteEditorInstaller
    path: bin/Setup/*.exe
```

---

For questions or issues, contact: dev@spriteeditorpro.com





