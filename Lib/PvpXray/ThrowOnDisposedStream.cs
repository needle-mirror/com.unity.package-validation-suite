using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PvpXray
{
    public sealed class ThrowOnDisposedStream : Stream
    {
        readonly Stream m_Stream;
        readonly bool m_PreventSeek;
        bool m_Disposed;

        public ThrowOnDisposedStream(Stream stream, bool preventSeek = false)
        {
            m_Stream = stream;
            m_PreventSeek = preventSeek;
        }

        void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            m_Stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            return m_Stream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            ThrowIfDisposed();
            return m_Stream.ReadByte();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return m_Stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            if (m_PreventSeek) throw new NotSupportedException();
            return m_Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            if (m_PreventSeek) throw new NotSupportedException();
            m_Stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            m_Stream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            m_Stream.WriteByte(value);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return m_Stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return m_Stream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override bool CanRead
        {
            get
            {
                ThrowIfDisposed();
                return m_Stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                ThrowIfDisposed();
                return !m_PreventSeek && m_Stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                ThrowIfDisposed();
                return m_Stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return m_Stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return m_Stream.Position;
            }
            set
            {
                ThrowIfDisposed();
                if (m_PreventSeek) throw new NotSupportedException();
                m_Stream.Position = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!m_Disposed)
            {
                if (disposing)
                {
                    m_Stream.Dispose();
                }

                m_Disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
