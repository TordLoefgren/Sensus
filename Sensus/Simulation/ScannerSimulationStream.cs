using System.IO;
using System.Text;
using Sensus.Models;
using Sensus.Protocol;
using Sensus.Serializers;

namespace Sensus.Simulation
{
    public class ScannerSimulationStream : Stream
    {

        #region Fields and properties

        private readonly Func<CancellationToken, IAsyncEnumerable<RangeSample>> _simulationSource;
        private readonly byte[] _handshakeResponse;

        private IAsyncEnumerator<RangeSample>? _simulationSourceEnumerator;
        private CancellationTokenSource? _sampleCancellation;
        private bool _hasHandshake;
        private bool _isRunning;
        private bool _resetEnumerator;
        private bool _stopResponsePending;

        private byte[] _pendingBytes = [];
        private int _pendingBytesOffset;
        private bool _disposed;

        public override bool CanRead => !_disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        #endregion

        public ScannerSimulationStream(
            Func<CancellationToken, IAsyncEnumerable<RangeSample>> simulationSource,
            string handshakeResponse
        )
        {
            _simulationSource = simulationSource;
            _handshakeResponse = Encoding.UTF8.GetBytes(handshakeResponse);
        }

        #region Synchronous stream operations

        public override void Flush()
        {
            // Commands are processed immediately. There is no write buffer to flush.
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
            if (command == ScannerProtocolMessages.Prepare + "\r\n")
            {
                _resetEnumerator = true;
                _hasHandshake = true;
                _isRunning = false;
                _stopResponsePending = false;

                _pendingBytes = _handshakeResponse;
                _pendingBytesOffset = 0;
                _sampleCancellation?.Cancel();
            }
            else if (_hasHandshake && command == ScannerProtocolMessages.Start + "\r\n")
            {
                _isRunning = true;
            }
            else if (command == ScannerProtocolMessages.Stop + "\r\n")
            {
                _hasHandshake = false;
                _isRunning = false;
                _stopResponsePending = true;
                _sampleCancellation?.Cancel();
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

            if (_resetEnumerator)
            {
                await ResetEnumeratorAsync();
                _resetEnumerator = false;
            }

            if (_pendingBytesOffset == _pendingBytes.Length)
            {
                if (_stopResponsePending)
                {
                    await ResetEnumeratorAsync();

                    QueueStopResponse();
                }
                else if (!_isRunning)
                {
                    return 0;
                }
                else
                {
                    _sampleCancellation ??= CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    _simulationSourceEnumerator ??= _simulationSource(_sampleCancellation.Token).GetAsyncEnumerator(_sampleCancellation.Token);

                    bool hasSample;

                    try
                    {
                        hasSample = await _simulationSourceEnumerator.MoveNextAsync();
                    }
                    catch (OperationCanceledException) when (_stopResponsePending || _resetEnumerator)
                    {
                        hasSample = false;
                    }

                    if (_resetEnumerator)
                    {
                        await ResetEnumeratorAsync();

                        _resetEnumerator = false;
                    }
                    else if (_stopResponsePending)
                    {
                        await ResetEnumeratorAsync();

                        QueueStopResponse();
                    }
                    else if (!hasSample)
                    {
                        return 0;
                    }
                    else
                    {
                        var serialized = RangeSampleSerializer.Serialize(_simulationSourceEnumerator.Current);

                        _pendingBytes = Encoding.UTF8.GetBytes(serialized);
                        _pendingBytesOffset = 0;
                    }
                }
            }

            var readCount = Math.Min(_pendingBytes.Length - _pendingBytesOffset, buffer.Length);

            _pendingBytes
                .AsMemory(_pendingBytesOffset, readCount)
                .CopyTo(buffer);

            _pendingBytesOffset += readCount;

            return readCount;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            return ReadAsync(
                buffer.AsMemory(offset, count),
                cancellationToken
            ).AsTask();
        }

        #endregion

        #region IDisposable

        private void QueueStopResponse()
        {
            _pendingBytes = Encoding.UTF8.GetBytes(ScannerProtocolMessages.Stopped + "\r\n");
            _pendingBytesOffset = 0;
            _stopResponsePending = false;
        }

        private async ValueTask ResetEnumeratorAsync()
        {
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
                _sampleCancellation?.Dispose();
                _sampleCancellation = null;
            }
        }

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

            try
            {
                _sampleCancellation?.Cancel();
                await ResetEnumeratorAsync();
            }
            finally
            {
                Dispose();
            }
        }

        #endregion

    }
}
