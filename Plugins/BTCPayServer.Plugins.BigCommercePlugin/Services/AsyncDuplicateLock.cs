using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public sealed class AsyncDuplicateLock
{
    private sealed class LockRef
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private readonly Dictionary<string, LockRef> _locks = new();
    private readonly object _gate = new();

    public async Task<IDisposable> LockAsync(string key, CancellationToken ct = default)
    {
        LockRef lockRef;
        lock (_gate)
        {
            if (!_locks.TryGetValue(key, out lockRef!))
            {
                lockRef = new LockRef();
                _locks[key] = lockRef;
            }
            lockRef.RefCount++;
        }
        try
        {
            await lockRef.Semaphore.WaitAsync(ct);
        }
        catch
        {
            Release(key, lockRef, releaseSemaphore: false);
            throw;
        }
        return new Releaser(this, key, lockRef);
    }

    private void Release(string key, LockRef lockRef, bool releaseSemaphore)
    {
        lock (_gate)
        {
            if (releaseSemaphore)
                lockRef.Semaphore.Release();

            lockRef.RefCount--;
            if (lockRef.RefCount == 0)
            {
                _locks.Remove(key);
                lockRef.Semaphore.Dispose();
            }
        }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncDuplicateLock _parent;
        private readonly string _key;
        private readonly LockRef _lockRef;
        private int _disposed;

        public Releaser(AsyncDuplicateLock parent, string key, LockRef lockRef)
            => (_parent, _key, _lockRef) = (parent, key, lockRef);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _parent.Release(_key, _lockRef, releaseSemaphore: true);
        }
    }
}

