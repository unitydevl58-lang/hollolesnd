namespace HoloLensApp.Interaction.CSG
{
    /// <summary>
    /// Defines the type of Boolean Constructive Solid Geometry operations available.
    /// </summary>
    public enum CSGOperationType
    {
        /// <summary>
        /// Combines both meshes into a single solid mesh.
        /// </summary>
        Union,

        /// <summary>
        /// Keeps only the overlapping (intersecting) volume of both meshes.
        /// </summary>
        Intersection,

        /// <summary>
        /// Subtracts the volume of the second mesh from the first mesh.
        /// </summary>
        Subtraction
    }
}
