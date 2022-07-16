using System;

namespace ComicReader.Utils
{
    interface IBuilderBase<out T> {}

    public abstract class BuilderBase<T> : IBuilderBase<T>
    {
        private bool m_committed = false;

        public T Commit()
        {
            if (m_committed)
            {
                throw new Exception("Cannot commit a builder twice");
            }
            m_committed = true;
            return CommitImpl();
        }

        protected abstract T CommitImpl();
    }

    // Specialization for type 'void'.
    public abstract class BuilderBase
    {
        private bool m_committed = false;

        public void Commit()
        {
            if (m_committed)
            {
                throw new Exception("Cannot commit a builder twice");
            }
            m_committed = true;
            CommitImpl();
        }

        protected abstract void CommitImpl();
    }
}
