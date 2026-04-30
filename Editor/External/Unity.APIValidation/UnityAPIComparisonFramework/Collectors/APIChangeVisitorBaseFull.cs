using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class APIChangeVisitorBaseFull : APIChangeVisitorBase, IEntityChangeVisitor
    {
        public void Visit(TypeChange typeChange)
        {
            foreach (var change in typeChange.Changes)
            {
                change.Accept(this);
            }
        }
    }
}
