using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FlaUInspect.Core;

public class ExtendedObservableCollection<T> : ObservableCollection<T> {
    public ExtendedObservableCollection() {
    }

    public ExtendedObservableCollection(IEnumerable<T> collection) : base(collection) {
    }

    public void AddRange(IEnumerable<T> range) {
        IList<T> rangeList = range as IList<T> ?? range.ToList();

        if (rangeList.Count == 0) {
            return;
        }

        if (rangeList.Count == 1) {
            Add(rangeList[0]);
            return;
        }

        foreach (T item in rangeList) {
            Items.Add(item);
        }
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void RemoveRange(int index, int count) {
        if (count <= 0 || index >= Items.Count) {
            return;
        }

        if (count == 1) {
            RemoveAt(index);
            return;
        }

        for (var i = 0; i < count; i++) {
            Items.RemoveAt(index);
        }
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void RemoveAll(Predicate<T> match) {
        var removedItem = false;

        for (int i = Items.Count - 1; i >= 0; i--) {
            if (match(Items[i])) {
                Items.RemoveAt(i);
                removedItem = true;
            }
        }

        if (removedItem) {
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public void Reset(IEnumerable<T> range) {
        ClearItems();
        AddRange(range);
    }
}