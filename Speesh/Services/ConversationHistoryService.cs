using System.Collections.Generic;
using System.Threading;
using Google.GenAI.Types;

namespace Speesh.Services
{
    public class ConversationHistoryService
    {
        private readonly List<Content> _history = new List<Content>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public List<Content> GetHistory()
        {
            _lock.EnterReadLock();
            try
            {
                // Return the actual list so callers can mutate it (we protect mutations with the AddContent method),
                // but callers should follow the AddContent pattern to be safe. For simplicity, we expose the list.
                return _history;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void AddContent(Content content)
        {
            _lock.EnterWriteLock();
            try
            {
                _history.Add(content);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _history.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
