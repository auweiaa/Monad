# Project Monad

This repository contains the Unity project for the course **Design and Construction of Digital Games**.

---

## Team Git Workflow

### Branches
- **`main`** – stable / playable / release-ready  
- **`development`** – integration branch (new work is merged here first)  
- **`<initials>/feature/<description>`** – new features  
- **`<initials>/bugfix/<description>`** – bug fixes  



### Rules
1. **Never push directly to `main`.**  
   All changes must go through a Pull Request.

2. **Feature branches are created from `development`** and merged back into `development` via Pull Request.

3. **Merge strategy:**
   - Feature → `development`: **Merge commit** or **Squash and merge**
   - `development` → `main`: **Merge commit**  


4. **Reviews:**
   - Pull Requests into `development` require **at least 1 approval**
   - Pull Requests into `main` require **2 approvals**

---

## Git Commands

Always create new work from the `development` branch and keep your feature branch up to date with `origin/development`.

**1) Start a new feature**
```bash
git checkout development
git fetch origin
git merge origin/development
git checkout -b <initials>/feature/<description>
```

**2) Keep your feature up to date**

- While working on your feature, make sure your branch stays up to date with `origin/development`

- Do this regularly, especially before pushing or opening a Pull Request. Two possibilities:

1. 
```bash
git checkout <initials>/feature/<description>
git fetch origin
git merge origin/development
```
2. If your feature branch has **not been pushed yet**, you **may rebase** instead:
```bash
git fetch origin
git rebase origin/development
```
- Both **merge and rebase are safe for local, unpushed** branches.

- ***!!! Never rebase a branch that has already been pushed,
as this rewrites commit history and breaks other clones !!!***

- If a merge conflict occurs, communicate with the team and resolve it locally before continuing!

**3) Work and push changes**
```bash
git add -A
git commit -m "<short message>"
git push -u origin <initials>/feature/<description>
```

**4) Make Pull Request on GitHub**
1. Open the repository on GitHub
2. Click "Compare & pull request" (or "New pull request")
3. Set:
   - base branch: `development`
   - compare branch: `<initials>/feature/<description>`
4. Add a short title and description of your changes
5. Request the required reviewers
6. Wait for approvals and address review comments
7. Merge the Pull Request once all requirements are met

**5) Clean up after merge**
```bash
git checkout development
git fetch origin
git merge origin/development
git fetch --prune
git branch -d <initials>/feature/<description>
```
---

## Unity-specific Rules

**Unity Project Settings:**
- Version Control: `Visible Meta Files`
- Asset Serialization: `Force Text`

**Additional notes:**
- Never delete or ignore `.meta` files.
- Avoid working on the same scene (`*.unity`) at the same time whenever possible.
  Scene files are merge-heavy and conflicts are difficult to resolve.

---

## Grid system (occupancy for pathing)

We use a central `GridManager` to track **which grid cells are occupied** by gameplay objects (towers, resources, core, etc.). This data is intended to be consumed by enemy pathfinding code.

### What blocks movement?
- **Rule**: anything registered as an occupant blocks movement. A cell is walkable only if it has ground and is not occupied.

### Scene setup
- Add an empty GameObject named `GridManager` and add the `GridManager` component.
- Assign:
  - `Grid`: your scene `Grid`
  - `GroundTilemap`: your ground `Tilemap` (usually named `Ground`)

### Prefab / object setup
Attach `GridOccupant` to any object that should reserve cells:
- **Towers**: `kind = Tower`, `footprint = TowerData.Footprint`
  - Towers placed at runtime are registered by `PlacementManager` automatically.
- **Resources**: `kind = Resource`, `footprint = 1x1` (or larger if needed)
  - If resources are already placed in the scene, keep `autoRegisterOnEnable = true`.
- **Core/Base**: `kind = Core`, set the correct `footprint`, and keep `autoRegisterOnEnable = true`.

### For path systems (API)
Enemy/path systems should query `GridManager`:
- `IsWalkable(cell)` for walkability
- `GetNeighbors8(cell)` for 8-way neighbor expansion (diagonals included)

