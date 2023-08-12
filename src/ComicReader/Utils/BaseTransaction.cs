using System;

namespace ComicReader.Utils
{
    interface IBaseTransaction<out T> {}

    public abstract class BaseTransaction<T> : IBaseTransaction<T>
    {
        private bool _committed = false;

        public T Commit()
        {
            if (_committed)
            {
                throw new Exception("Cannot commit a transaction twice");
            }
            _committed = true;
            return CommitImpl();
        }

        protected abstract T CommitImpl();
    }

    // Specialization for 'void'
    public abstract class BaseTransaction
    {
        private bool _committed = false;

        public void Commit()
        {
            if (_committed)
            {
                throw new Exception("Cannot commit a transaction twice");
            }
            _committed = true;
            CommitImpl();
        }

        protected abstract void CommitImpl();
    }
}
