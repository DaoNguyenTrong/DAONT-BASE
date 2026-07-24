# Contributing to FEEDBACK-HUB

## Git Workflow

```
main ─────●───────────────●───────● (production, tagged releases)
           \             /       /
dev ────────●────●────●─●───────●── (integration/staging)
             \       /   \     /
feature       ●────●      ●───●
```

## Branches

| Branch | Purpose |
|--------|---------|
| `main` | Production releases |
| `dev` | Integration/staging |
| `feature/*` | New features |
| `fix/*` | Bug fixes |
| `hotfix/*` | Emergency production fixes |

## Development Flow

### 1. Start a Feature

```bash
git checkout dev
git pull origin dev
git checkout -b feature/your-feature-name
```

### 2. Develop & Commit

```bash
git add <files>
git commit -m "feat: add new feature"
```

**Commit message format:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation
- `refactor:` - Code refactoring
- `test:` - Adding tests
- `chore:` - Maintenance

### 3. Create Pull Request

```bash
git push origin feature/your-feature-name
```

Then create PR to `dev` branch on GitHub.

### 4. Update CHANGELOG

When your PR is merged, add entry to `CHANGELOG.md` under `[Unreleased]`:

```markdown
## [Unreleased]
### Added
- Your new feature description
```

## Release Process

1. Ensure all features are merged to `dev`
2. Update `CHANGELOG.md`:
   - Move `[Unreleased]` items to new version section
   - Add release date
3. Create PR: `dev` → `main`
4. After merge, tag release:

```bash
git checkout main
git pull origin main
git tag v1.x.0 -m "Release 1.x.0"
git push origin main --tags
```

5. GitHub Actions will create the release automatically

## Versioning (SemVer)

- **MAJOR** (v2.0.0): Breaking changes
- **MINOR** (v1.1.0): New features, backward compatible
- **PATCH** (v1.0.1): Bug fixes

Version is managed automatically by MinVer based on git tags.
