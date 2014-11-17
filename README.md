Unity Psd Importer
==================

Unity Psd Importer is an addon for Unity3D. It provides an editor window from which individual layers can be selected
and exported. The layers are exported either as individual PNGs or as an atlas image.

Installation
------------

This plug in can be installed in two ways

### Compiled DLL ###

The source code can be compiled into a DLL and placed into any directory in your Unity project. This repo contains a compiled DLL in `/bin/PhotoShop.dll`

### Unity Editor Compilation ###

To compile the Unity PSD Importer in the Unity3D Editor put the files `gmcs.rsp` and `smcs.rsp` in the root `Assets` directory of your project.

Then copy the following files and directories under the `PhotoShopFileType` into any directory of your project.

- `/Editor`
- `/PsdFile`

Usage
-----

To use the Unity PSD Importer, in the Unity3D Editor go to Sprites > PSD Import to access the importer, then drag and drop or search for the PSD file you wish to import.  

Alternatively, right click on a PSD in the project explorer and go to Sprites > PSD Import.

### Layers Settings ###

Check the layers that will be exported. This is initially set to the visible layers from the PSD document. If a layer group is unchecked, the child layers cannot be checked.

The layer size drop down lets you further reduce the absolute image size of the layer when it's imported while retaining the same display size in relation to the other imported layers.

The layer pivot drop down lets you set the pivot point of the layer when imported to Unity.

### Export Settings ###

The *1X, 2X, 4X* setting indicates how much in absolute terms the PSD will be resized by when importing layers. 1X means no reduction, 2X will scale the PSD down to 50% and 4X will scale it down to 25%.

*Pixels to unit size* and *pivot* are the default import settings that the exported sprites will use.

Press the **Export Visible Layers** button to export the layers. Depending on the size of the PSD, this may take a while.

Export settings are saved as asset tags on the PSD file.

### Sprite Creation ###

Sprite creation recreates the layout of the PSD document in your scene.

*Create Pivot* sets where the root of the PSD document will start from.

*Sorting Layer* sets the sorting layer the created sprites will be on.

Clicking on **Create at Selection** will recreate the PSD starting on the selected game object in the hierarchy, which also copies the layer of the selection.

Clicking on **Create Sprites** will recreate the PSD at the root of your scene hierarchy.

PSD support
-----------

It should support all image layers. However text, layer effects or other special layers will not be supported. It is best to rasterize text and layer effects before importing.

Atlas Support
-------------

The [original repo](https://github.com/Banbury/UnityPsdImporter) contains Atlas Support. However, since I do not need it yet, it is disabled in this fork.
