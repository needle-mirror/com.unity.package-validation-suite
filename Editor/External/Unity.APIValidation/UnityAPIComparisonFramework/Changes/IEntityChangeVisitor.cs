namespace Unity.APIComparison.Framework.Changes
{
    public interface IEntityChangeVisitor
    {
        void Visit(TypeChange change);
    }
}
