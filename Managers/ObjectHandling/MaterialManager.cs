using Rhino;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using System.Drawing;
using Axys;

namespace Axys.Managers.ObjectHandling
{
    /// <summary>
    /// Utility functions for assigning and selecting materials on Rhino objects.
    /// </summary>
    public class MaterialManager
    {
    /// <summary>
    /// Applies a material to the given object if it does not already have one.
    /// </summary>
    /// <param name="rhObj">Rhino object to modify.</param>
    /// <param name="doc">Active Rhino document.</param>
    /// <param name="materialIndex">Index of the material to assign.</param>
    public static void ApplyMaterialIfMissing(RhinoObject rhObj, RhinoDoc doc, int materialIndex)
    {
        // An object has valid material if:
        // 1. It has directly assigned material and the index is valid
        // 2. It inherits material from layer and the layer has a valid material

        bool hasDirectMaterial = rhObj.Attributes.MaterialSource == ObjectMaterialSource.MaterialFromObject &&
                                rhObj.Attributes.MaterialIndex >= 0 &&
                                rhObj.Attributes.MaterialIndex < doc.Materials.Count;

        bool hasLayerMaterial = false;
        if (rhObj.Attributes.MaterialSource == ObjectMaterialSource.MaterialFromLayer)
        {
            int layerIndex = rhObj.Attributes.LayerIndex;
            if (layerIndex >= 0 && layerIndex < doc.Layers.Count)
            {
                Layer layer = doc.Layers[layerIndex];
                if (layer != null)
                {
                    int layerMaterialIndex = layer.RenderMaterialIndex;
                    hasLayerMaterial = layerMaterialIndex >= 0 && layerMaterialIndex < doc.Materials.Count;
                }
            }
        }

        // If no valid material, apply the specified material
        if (!hasDirectMaterial && !hasLayerMaterial)
        {
            // Make sure the material index is valid
            if (materialIndex < 0 || materialIndex >= doc.Materials.Count)
            {
                Logger.LogWarning("Invalid material index, using default material (index 0).");
                materialIndex = 0;
            }

            ObjectAttributes attr = rhObj.Attributes.Duplicate();
            attr.MaterialIndex = materialIndex;
            attr.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            doc.Objects.ModifyAttributes(rhObj, attr, true);
            Logger.LogInfo($"Applied material (index {materialIndex}) to object.");
        }
        else
        {
            Logger.LogDebug("Object already has a valid material assigned.");
        }
    }


    /// <summary>
    /// Displays a Rhino UI prompt allowing the user to choose a material.
    /// Creates a default material when none exist.
    /// </summary>
    /// <param name="doc">Active Rhino document.</param>
    /// <returns>Index of the chosen material.</returns>
    public static int SelectMaterial(RhinoDoc doc)
    {
        // Get all materials from the document
        var materials = doc.Materials;
        if (materials.Count == 0)
        {
            // If no materials exist, create a default one
            int materialIndex = doc.Materials.Add();
            Material defaultMaterial = doc.Materials[materialIndex];
            defaultMaterial.Name = "Default Export Material";
            defaultMaterial.DiffuseColor = System.Drawing.Color.White;
            defaultMaterial.CommitChanges();
            return materialIndex;
        }

        // Create a list of material names
        string[] materialNames = new string[materials.Count];
        for (int i = 0; i < materials.Count; i++)
        {
            materialNames[i] = $"{i}: {materials[i].Name}";
        }

        // Prompt user for material selection
        int selectedIndex = 0;
        var getOption = new GetOption();
        getOption.SetCommandPrompt("Select material for export");

        for (int i = 0; i < materialNames.Length; i++)
        {
            getOption.AddOption(materialNames[i]);
        }

        getOption.Get();
        selectedIndex = getOption.OptionIndex();

        // Return the selected material index or 0 if canceled
        return selectedIndex >= 0 && selectedIndex < materials.Count ? selectedIndex : 0;
    }
}
}
