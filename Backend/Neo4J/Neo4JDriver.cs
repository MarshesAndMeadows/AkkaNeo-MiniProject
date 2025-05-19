using Neo4j.Driver;

namespace GraphServer.Neo4j
{
    public class Neo4jDriver : IDisposable
    {
        private readonly IDriver _driver;
        private bool _disposed = false;

        public Neo4jDriver(string uri, string user, string password)
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        }

        public IAsyncSession CreateAsyncSession()
        {
            return _driver.AsyncSession();
        }

        public async Task<bool> VerifyConnectivityAsync()
        {
            try
            {
                await _driver.VerifyConnectivityAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _driver?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
