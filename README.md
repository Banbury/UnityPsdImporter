Unity Psd Importer
==================

Unity Psd Importer is an addon for Unity3D. It provides an editor window from which individual layers can be selected
and exported. The layers are exported either as individual PNGs or as an atlas image.

Installation
------------

This plug in can be installed in two ways

### Compiled DLL ###

The source code can be compiled into a DLL and placed into any folder of your project. This repo contains a compiled DLL in `/bin/Release/PhotoShop.dll`

### Unity Editor Compilation ###

To compile the Unity PSD Importer in the Unity3D Editor put the files `gmcs.rsp` and `smcs.rsp` in the root `Assets` directory of your project.

Then copy the following files and directories under the `PhotoShopFileType` into any directory of your project.

- `/Editor`
- `/PsdFile`
- `TextureResize.cs`

Usage
-----

To use the Unity PSD Importer, in the Unity3D Editor go to Sprites > PSD Import to access the importer, then drag and drop or search for the PSD file you wish to import.  

Alternatively, right click on a PSD in the project explorer and go to Sprites > PSD Import.

### Export Settings ###

Check the layers that will be exported. This is initially set to the visible layers from the PSD document. If a layer group is unchecked, the child layers cannot be checked.

The *1X, 2X, 4X* setting indicates how many times larger the PSD is in relation to the screen pixel size. This is useful for when your source PSD is created at a higher resolution than the target one.

*Pixels to unit size* and *pivot* are the import settings that all the exported sprites will use.

Press the **Export Visible Layers** button to export the layers. Depending on the size of the PSD, this may take a while.

**Important note**

If you are exporting with a pivot that is not center, you must select all the exported sprites in the project explorer after your first export and manually set the pivot to `custom` in the Unity inspector. This only has to be done once.

### Sprite Creation

Sprite creation recreates the layout of the PSD document in your scene.

*Create Pivot* sets where the root of the PSD document will start from.

*Sorting Layer* sets where the sorting layer the created sprites will be on.

Clicking on **Create at Selection** will recreate the PSD starting on the selected game object in the hierarchy, which also copies the layer of the selection.

Clicking on **Create Sprites** will recreate the PSD at the root of your scene hierarchy.

PSD support
-----------

It should support all image layers. However text, layer effects or other special layers will not be supported. It is best to rasterize text and layer effects before importing.

Atlas Support
-------------

The [original repo](https://github.com/Banbury/UnityPsdImporter) contains Atlas Support. However, since I do not need it yet, it is disabled in this fork.
