using System;
using System.Collections.Generic;

namespace Tesserae.A2UI
{
    /// <summary>Collects the subscriptions and child slots a built component owns, so rebuilding or removing it releases them all.</summary>
    public sealed class Disposer : IDisposable
    {
        private List<IDisposable> _items = new List<IDisposable>();

        public T Add<T>(T item) where T : IDisposable
        {
            _items.Add(item);
            return item;
        }

        public void Add(Action onDispose) => _items.Add(new ActionDisposable(onDispose));

        public void Dispose()
        {
            var items = _items;
            _items = new List<IDisposable>();
            foreach (var i in items) { try { i.Dispose(); } catch (Exception) { /* teardown must not stop the rest */ } }
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _a;
            public ActionDisposable(Action a) { _a = a; }
            public void Dispose() { var a = _a; _a = null; a?.Invoke(); }
        }
    }

    /// <summary>A value computed from the data model that recomputes, and hands the new result on, whenever data it read changes.</summary>
    internal sealed class LiveBinding<T> : IDisposable
    {
        private readonly ComponentContext _cx;
        private readonly Func<EvalContext, T> _eval;
        private readonly Action<T> _apply;
        private IDisposable _subscription;
        private bool _disposed;

        public LiveBinding(ComponentContext cx, Func<EvalContext, T> eval, Action<T> apply)
        {
            _cx    = cx;
            _eval  = eval;
            _apply = apply;
            Refresh();
        }

        private void Refresh()
        {
            if (_disposed) return;

            var ec = _cx.NewEval();
            T value = default(T);

            try { value = _eval(ec); }
            catch (A2UIFunctionException e) { _cx.Surface.Host.Report(new A2UIError(e.Code, e.Message, _cx.Surface.Id, "/components/" + _cx.Id)); }

            _subscription?.Dispose();
            _subscription = ec.Dependencies.Count == 0 ? null : _cx.Surface.Data.Subscribe(ec.Dependencies, Refresh);

            _apply(value);
        }

        public void Dispose()
        {
            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
