using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ComicReader.Utils
{
    public sealed class ObservableCollectionPlus<T> : ObservableCollection<T> where T : INotifyPropertyChanged
    {
        //public event PropertyChangedEventHandler CollectionItemChanged;

        public ObservableCollectionPlus()
        {
            CollectionChanged += FullObservableCollectionCollectionChanged;
        }

        public ObservableCollectionPlus(IEnumerable<T> pItems) : this()
        {
            foreach (T item in pItems)
            {
                Add(item);
            }
        }

        private void FullObservableCollectionCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (object item in e.NewItems)
                {
                    ((INotifyPropertyChanged)item).PropertyChanged += ItemPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (object item in e.OldItems)
                {
                    ((INotifyPropertyChanged)item).PropertyChanged -= ItemPropertyChanged;
                }
            }
        }

        private void ItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            int idx = IndexOf((T)sender);

            if (idx < 0)
            {
                return;
            }

            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, sender, sender, idx);
            OnCollectionChanged(args);
        }
    }
}
