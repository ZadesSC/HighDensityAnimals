using RimWorld;
namespace HighDensityAnimals
{
    // Core storage controls without DLC-specific casts or irrelevant priority controls.
    public sealed class ITab_DensifierStorage : ITab_Storage
    {
        protected override bool IsPrioritySettingVisible => false;
    }
}
