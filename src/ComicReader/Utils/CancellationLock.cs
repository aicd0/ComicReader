using System.Threading;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class CancellationLock
    {
        private int m_cancellation_requests;
        private SemaphoreSlim m_semaphore;

        public bool CancellationRequested => m_cancellation_requests > 0;

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

        public async Task WaitAsync(int millisecond_timeout)
        {
            _ = Interlocked.Increment(ref m_cancellation_requests);
            await m_semaphore.WaitAsync(millisecond_timeout);
            _ = Interlocked.Decrement(ref m_cancellation_requests);
        }

        public void Release()
        {
            _ = m_semaphore.Release();
        }
    }
}
