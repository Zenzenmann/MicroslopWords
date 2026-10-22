using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public interface IEditCommand
{
    string Description { get; }

    Document Apply(Document document);

    Document Revert(Document document);
}
