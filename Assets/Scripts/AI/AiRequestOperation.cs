using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns a provider's nested iterators, including cancellation during callbacks.</summary>
public sealed class AiRequestOperation
{
    private readonly MonoBehaviour owner;
    private readonly Stack<IEnumerator> iterators = new Stack<IEnumerator>();
    private Coroutine routine;
    private bool executing;
    public bool IsRunning { get; private set; }

    public AiRequestOperation(MonoBehaviour owner) { this.owner = owner; }

    public void Start(Func<IEnumerator> factory, Action<Exception> failed, Action ended)
    {
        if (IsRunning) throw new InvalidOperationException("Request already started.");
        IsRunning = true;
        Coroutine started = owner.StartCoroutine(Run(factory, failed, ended));
        // StartCoroutine may complete synchronously, before returning its handle.
        if (IsRunning) routine = started;
    }

    public void Cancel()
    {
        IsRunning = false;
        // Never dispose an iterator from inside its own MoveNext/factory/callback.
        if (executing) return;
        Coroutine abandoned = routine;
        routine = null;
        if (owner != null && abandoned != null) owner.StopCoroutine(abandoned);
        DisposeAll(); // Some Unity versions do not dispose every stopped iterator.
    }

    private IEnumerator Run(Func<IEnumerator> factory, Action<Exception> failed, Action ended)
    {
        executing = true;
        try
        {
            IEnumerator request = null;
            Exception error = null;
            try { request = factory(); }
            catch (Exception exception) { error = exception; }
            if (request != null) iterators.Push(request);
            if (!IsRunning) yield break;
            if (error != null) { failed?.Invoke(error); yield break; }
            if (request == null) { ended?.Invoke(); yield break; }

            while (IsRunning && iterators.Count > 0)
            {
                IEnumerator current = iterators.Peek();
                bool advanced = false;
                object yielded = null;
                error = null;
                try
                {
                    advanced = current.MoveNext();
                    if (advanced) yielded = current.Current;
                }
                catch (Exception exception) { error = exception; }
                if (!IsRunning) yield break;
                if (error != null) { failed?.Invoke(error); yield break; }
                if (!advanced) { iterators.Pop(); Dispose(current); }
                else if (yielded is IEnumerator nested) iterators.Push(nested);
                else
                {
                    executing = false;
                    yield return yielded;
                    executing = true;
                }
            }
            // A provider that ends without its completion callback must not lock UI.
            if (IsRunning) ended?.Invoke();
        }
        finally
        {
            IsRunning = false;
            routine = null;
            executing = true;
            DisposeAll();
            executing = false;
        }
    }

    private void DisposeAll()
    {
        while (iterators.Count > 0) Dispose(iterators.Pop());
    }

    private static void Dispose(IEnumerator request)
    {
        try { (request as IDisposable)?.Dispose(); }
        catch (Exception exception)
        { Debug.LogWarning("AI request cleanup failed: " + exception.GetType().Name); }
    }
}
