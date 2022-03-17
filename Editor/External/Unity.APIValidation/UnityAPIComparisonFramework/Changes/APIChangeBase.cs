using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public abstract class APIChangeBase<T> : IAPIChange where T : class, IMemberDefinition
    {
        protected APIChangeBase(T original, T current, string dataToBeHashed)
        {
            Original = original;
            Current = current;

            var data = GetType().Name + "-" + dataToBeHashed;
            Hash = data.ComputeHash();
            AffectedPlatforms = new List<string>();

            SourcePath = CurrentEntity.GetSourcePathFromDebugInformation();
        }

        protected APIChangeBase(T original, T current) : this(original, current, DataToBeHashedFor(original, current))
        {
        }

        public abstract void Accept(IAPIChangeVisitor visitor);

        public IMemberDefinition Original { get; private set; }
        public IMemberDefinition Current { get; private set; }

        public T OriginalEntity { get { return (T)Original; } }
        public T CurrentEntity { get { return (T)Current; } }

        public string Hash { get; private set; }

        public string SourcePath
        {
            get;
            private set;
        }

        public IList<string> AffectedPlatforms
        {
            get;
            private set;
        }

        public virtual bool IsAdd() => false;

        public override string ToString()
        {
            return string.Format("{0} ({1} -> {2})", GetType().Name, Original, Current);
        }

        private static string DataToBeHashedFor(T original, T current)
        {
            var data = (original != null ? original.FullName : "") + "-" + (current != null ? current.FullName : "");
            return data;
        }
    }
}
