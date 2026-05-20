namespace HoloLensApp.Interaction.Math
{
    /// <summary>
    /// Wong (1969) spatial form-interaction states used by the MR app.
    /// Touching vs Penetration are distinguished by volumetric overlap, not rendering tricks.
    /// </summary>
    public enum WongInteractionState
    {
        Unknown = 0,
        Detachment,
        Touching,
        Overlapping,
        Penetration,
        Coinciding,
        Union,
        Subtraction,
        Intersection
    }
}
