using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class CancellationLock
    {
        private int m_cancellation_requests;
        private SemaphoreSlim m_semaphore;

        public CancellationLock()
        {
            m_cancellation_requests = 0;
            m_semaphore = new SemaphoreSlim(1);
        }

        public async Task WaitAsync()
        {
            _ = Interlocked.Increment(ref m_cancellation_requests);
            await m_semaphore.WaitAsync();
            _ = Interlocked.Decrement(ref m_cancellation_requests);
        }

        public void Release()
        {
            _ = m_semaphore.Release();
        }

        public bool CancellationRequested()
        {
            return m_cancellation_requests > 0;
        }
    }
}
