namespace FuzzPhyte.Utility
{
    /// <summary>
    /// Controls how an outline target contributes pixels to the outline mask.
    /// </summary>
    public enum FPOutlineAlphaMode
    {
        MeshSilhouette = 0,
        MainTextureAlpha = 1,
        CustomMaskTexture = 2
    }
}
