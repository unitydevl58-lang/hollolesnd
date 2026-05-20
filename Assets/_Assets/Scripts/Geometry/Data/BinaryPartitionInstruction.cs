/// <summary>
/// Optional instruction for creating two spatially separated halves from one command.
/// </summary>
public sealed class BinaryPartitionInstruction
{
    public bool Enabled;
    public PartitionAxis Axis = PartitionAxis.X;
    public float Gap = 0.06f;
    public string FirstColor;
    public string SecondColor;

    /// <summary>
    /// Creates a disabled partition instruction.
    /// </summary>
    public static BinaryPartitionInstruction None()
    {
        return new BinaryPartitionInstruction { Enabled = false };
    }
}
