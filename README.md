# TP2 Unity Project

Unity 6 (`6000.4.8f1`) project repository.

## Repository Setup & Requirements

### 1. Git LFS (Large File Storage)
This project uses Git LFS to track binary assets (models, textures, audio, fonts).
Ensure Git LFS is installed before cloning or pushing:

```bash
git lfs install
```

### 2. Project Structure
- `Assets/`: Unity source assets, C# scripts, prefabs, and scenes.
- `Packages/`: Unity Package Manager manifests and custom packages.
- `ProjectSettings/`: Unity project setting configurations.
- `.github/workflows/`: GitHub Actions CI pipeline configurations.

### 3. Git Workflow Guidelines
- **`main`**: Production-ready / stable release branch.
- **`develop`**: Active integration branch for features.
- **`feature/*`**: Individual feature development branches.
