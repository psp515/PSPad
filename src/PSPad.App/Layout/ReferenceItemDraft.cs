using PSPad.Module.Tasks.References;

namespace PSPad.App.Layout;

public sealed class ReferenceItemDraft
{
    public string Name { get; set; } = "";

    public Guid? ListId { get; set; }

    public List<ReferenceField> Fields { get; } = [];

    public string Description { get; set; } = "";
}
