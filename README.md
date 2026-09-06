# VSMC2 — Fowl's Extended Tooling

This is a fork of [Vintage Story Model Creator 2 (VSMC2)](https://github.com/anegostudios/vsmc2).

While learning VSMC and transitioning to VSMC2 to work on more modeling-focused endeavors, there have been a few features I've been wishing I had to help speed up my workflow. I'm keeping each feature on a separate branch based on upstream `main`, both to keep the changes easy to inspect and so others can use only the additions they are interested in.

There is also an [`extended-tools`](https://github.com/f0WLer/vsmc2/tree/extended-tools) branch containing all of the additions together.

> **Status:** These are unofficial, experimental additions that I use in my own modeling workflow. They may still have bugs or edge cases. VSMC2 itself is also under active development.

## Custom Feature Branches

### Name Children

Adds **Selection > Name Children**.

Automatically renames and sequentially numbers the direct children of the currently selected element (i.e "`<ParentName>`*`N`*"), making larger element hierarchies easier to organize and manage.

**Branch:** [`batch-name-children`](https://github.com/f0WLer/vsmc2/tree/batch-name-children)

Clone and try it:

```bash
git clone -b batch-name-children https://github.com/f0WLer/vsmc2.git
```

------

### True Mirroring

Adds **Selection > Mirror Across Axis > X/Y/Z**, a true geometric mirror for an element and its complete child hierarchy.

Unlike the common workaround of parenting geometry to an element at the model origin and rotating it 180°, this performs an actual reflection. This allows asymmetric geometry to properly mirror rather than merely rotating to the opposite side.

The mirrored result is an ordinary editable copy of the original hierarchy.

**Branch:** [`true-mirroring`](https://github.com/f0WLer/vsmc2/tree/true-mirroring)

Clone and try it:

```bash
git clone -b true-mirroring https://github.com/f0WLer/vsmc2.git
```

------

### Align Faces

Adds **Selection > Align Faces**.

Select a source face and a parallel target face. The source element is translated only in the direction necessary to make the two selected face planes coincide.

The tool intentionally:

- does not rotate either element
- does not center the faces
- preserves offsets along the other two directions
- allows overlapping/intersecting cuboids

This makes it useful for precisely placing elements against other model geometry without manual tuning.

**Branch:** [`align-faces`](https://github.com/f0WLer/vsmc2/tree/align-faces)

Clone and try it:

```bash
git clone -b align-faces https://github.com/f0WLer/vsmc2.git
```

------

### SpaceMouse Support

Adds native **3Dconnexion SpaceMouse / NDOF viewport navigation on Windows**, using Raw Input over the 3Dconnexion SDK.

The viewport controls are intended to feel similar to Blender's SpaceMouse navigation, including simultaneous pan, orbit, and dolly.

**Branch:** [`spacemouse-support`](https://github.com/f0WLer/vsmc2/tree/spacemouse-support)

Clone and try it:

```bash
git clone -b spacemouse-support https://github.com/f0WLer/vsmc2.git
```

------

## All Tools Together

If you want all of the above additions in one build, use the `extended-tools` branch.

**Branch:** [`extended-tools`](https://github.com/f0WLer/vsmc2/tree/extended-tools)

```bash
git clone -b extended-tools https://github.com/f0WLer/vsmc2.git
```

The individual feature branches remain separate so each addition can still be reviewed, tested, or used independently.

## Already Have the Repository Cloned?

Fetch the latest branches:

```bash
git fetch origin
```

Then switch to whichever version you want:

```bash
git switch spacemouse-support
```

For example, to use the combined toolset:

```bash
git switch extended-tools
```

## Building VSMC2

VSMC2 is built using the Unity game engine.

The Unity version currently used by upstream is:

**Unity 6000.0.68f1**

To build or edit one of these branches:

1.  Clone the desired branch
2.  Add the repository as a project in Unity Hub
3.  Install/open it with Unity `6000.0.68f1`
4.  Open the `ModelCreator` scene

*"There are two Unity scenes included in the project:*

- *`ModelCreator` — the main VSMC2 editor UI.*
- *`SampleScene` — used for testing and prototyping."*

## About VSMC2

VSMC2 is primarily developed by Nat at Anego Studios and is currently a work in progress.

This fork and the additions above are unofficial and are not replacements for upstream VSMC2 development.

For the original project, official development, releases, and issue reporting, see:

**[Anego Studios / VSMC2](https://github.com/anegostudios/vsmc2)**
