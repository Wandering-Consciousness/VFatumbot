using System;

namespace VFatumbot
{
    // Useful for local developing when ANU is being slow and it's not the randomness we're working on...
    public class PseudoRandomNumberGenerator : BaseRandomProvider, IDisposable
    {
        // If this property is != null, then we use this to determine what kind of entropy we get from the API (libwrapper server).
        // E.g. gid=<GID> to specify the entropy's ID for entropy uploaded from camera, shared etc.
        public string EntropySrcQueryString { get; set; } = null;

        ~PseudoRandomNumberGenerator()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private bool _disposed;
    }
}
