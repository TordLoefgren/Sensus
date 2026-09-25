using System.IO;
using System.Text;
using Sensus.Models;
using Sensus.Services;

namespace Sensus.Simulation
{
    public class ScannerSimulationStream : Stream
    {
        #region Fields and properties

        private readonly Func<CancellationToken, IAsyncEnumerable<RangeSample>> _simulationSource;
        private readonly IRangeSampleSerializerService _rangeSampleSerializerService;

        private IAsyncEnumerator<RangeSample>? _simulationSourceEnumerator;
        private bool _hasHandshake;
        private bool _isRunning;

        private byte[] _pendingBytes = [];
        private int _pendingBytesOffset;
        private int _pendingBytesCount;
        private bool _disposed;

        public override bool CanRead => !_disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        #endregion

        public ScannerSimulationStream(
            Func<CancellationToken, IAsyncEnumerable<RangeSample>> simulationSource,
            IRangeSampleSerializerService rangeSampleSerializerService
        )
        {
            _simulationSource = simulationSource;
            _rangeSampleSerializerService = rangeSampleSerializerService;
        }

        #region Synchronous stream operations

        public override void Flush()
        {
            // Commands are processed immediately; there is no write buffer to flush.
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

        #region Asynchronous stream operations

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            var command = Encoding.UTF8.GetString(buffer.Span);
            if (command == "HELLO\r\n")
            {
                _hasHandshake = true;
                _isRunning = false;

                _pendingBytes = Encoding.UTF8.GetBytes("HELLO BACK\r\n");
                _pendingBytesOffset = 0;
                _pendingBytesCount = _pendingBytes.Length;
            }

            if (_hasHandshake && command == "START\r\n")
            {
                _isRunning = true;
            }

            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            // https://learn.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8
            ObjectDisposedException.ThrowIf(_disposed, this);

            cancellationToken.ThrowIfCancellationRequested();

            if (buffer.Length == 0)
            {
                return 0;
            }

            // Expose and use the enumerator directly, instead of invoking the generator directly every time.
            _simulationSourceEnumerator ??= _simulationSource(cancellationToken).GetAsyncEnumerator(cancellationToken);

            if (_pendingBytesCount == 0)
            {
                if (!_isRunning)
                {
                    return 0;
                }

                if (!await _simulationSourceEnumerator.MoveNextAsync())
                {
                    return 0;
                }

                var serialized = _rangeSampleSerializerService.Serialize(_simulationSourceEnumerator.Current);

                _pendingBytes = Encoding.UTF8.GetBytes(serialized);

                _pendingBytesOffset = 0;
                _pendingBytesCount = _pendingBytes.Length;
            }

            var readCount = Math.Min(_pendingBytesCount, buffer.Length);

            _pendingBytes
                .AsMemory(_pendingBytesOffset, readCount)
                .CopyTo(buffer);

            _pendingBytesOffset += readCount;
            _pendingBytesCount -= readCount;

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

        #endregion

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
