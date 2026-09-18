using System.IO;
using System.Text;
using Sensus.Services;

namespace Sensus
{
    public class ScannerSimulationStream : Stream
    {
        private readonly Func<CancellationToken, IAsyncEnumerable<RangeSample>> _simulationSource;
        private readonly IRangeSampleSerializerService _rangeSampleSerializerService;

        private IAsyncEnumerator<RangeSample>? _simulationSourceEnumerator;
        private byte[] _pendingBytes = [];
        private int pendingBytesOffset = 0;
        private int pendingBytesCount = 0;
        private bool _disposed;

        #region Empty Overrides

        public override bool CanRead => !_disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion

        public ScannerSimulationStream(
            Func<CancellationToken, IAsyncEnumerable<RangeSample>> simulationSource,
            IRangeSampleSerializerService rangeSampleSerializerService
        )
        {
            _simulationSource = simulationSource;
            _rangeSampleSerializerService = rangeSampleSerializerService;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // https://learn.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8

            cancellationToken.ThrowIfCancellationRequested();

            if (buffer.Length == 0)
            {
                return 0;
            }

            // Expose and use the enumerator directly, instead of invoking the generator directly every time.
            _simulationSourceEnumerator ??= _simulationSource(cancellationToken).GetAsyncEnumerator(cancellationToken);

            if (pendingBytesCount == 0)
            {
                if (!await _simulationSourceEnumerator.MoveNextAsync())
                {
                    return 0;
                }

                var serialized = _rangeSampleSerializerService.Serialize(_simulationSourceEnumerator.Current);

                _pendingBytes = Encoding.UTF8.GetBytes(serialized);

                pendingBytesOffset = 0;
                pendingBytesCount = _pendingBytes.Length;
            }

            var readCount = Math.Min(pendingBytesCount, buffer.Length);

            _pendingBytes
                .AsMemory(pendingBytesOffset, readCount)
                .CopyTo(buffer);

            pendingBytesOffset += readCount;
            pendingBytesCount -= readCount;

            return readCount;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return ReadAsync(
                buffer.AsMemory(offset, count),
                cancellationToken
            ).AsTask();
        }

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            var enumerator = _simulationSourceEnumerator;
            _simulationSourceEnumerator = null;

            try
            {
                if (enumerator is not null)
                {
                    await enumerator.DisposeAsync();
                }
            }
            finally
            {
                Dispose();
            }
        }

        #endregion

    }
}
