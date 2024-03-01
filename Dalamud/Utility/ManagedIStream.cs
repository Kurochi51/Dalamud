using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TerraFX.Interop;
using TerraFX.Interop.Windows;

namespace Dalamud.Utility;

/// <summary>An <see cref="IStream"/> wrapper for <see cref="Stream"/>.</summary>
[Guid("a620678b-56b9-4202-a1da-b821214dc972")]
internal sealed unsafe class ManagedIStream : IStream.Interface, IRefCountable
{
    private static readonly Guid MyGuid = typeof(ManagedIStream).GUID;

    private readonly Stream inner;
    private readonly nint[] comObject;
    private readonly IStream.Vtbl<IStream> vtbl;
    private GCHandle gchThis;
    private GCHandle gchComObject;
    private GCHandle gchVtbl;
    private int refCount;

    /// <summary>Initializes a new instance of the <see cref="ManagedIStream"/> class.</summary>
    /// <param name="inner">The inner stream.</param>
    public ManagedIStream(Stream inner)
    {
        this.inner = inner;
        this.comObject = new nint[2];

        this.vtbl.QueryInterface = &QueryInterfaceStatic;
        this.vtbl.AddRef = &AddRefStatic;
        this.vtbl.Release = &ReleaseStatic;
        this.vtbl.Read = &ReadStatic;
        this.vtbl.Write = &WriteStatic;
        this.vtbl.Seek = &SeekStatic;
        this.vtbl.SetSize = &SetSizeStatic;
        this.vtbl.CopyTo = &CopyToStatic;
        this.vtbl.Commit = &CommitStatic;
        this.vtbl.Revert = &RevertStatic;
        this.vtbl.LockRegion = &LockRegionStatic;
        this.vtbl.UnlockRegion = &UnlockRegionStatic;
        this.vtbl.Stat = &StatStatic;
        this.vtbl.Clone = &CloneStatic;

        this.gchThis = GCHandle.Alloc(this);
        this.gchVtbl = GCHandle.Alloc(this.vtbl, GCHandleType.Pinned);
        this.gchComObject = GCHandle.Alloc(this.comObject, GCHandleType.Pinned);
        this.comObject[0] = this.gchVtbl.AddrOfPinnedObject();
        this.comObject[1] = GCHandle.ToIntPtr(this.gchThis);
        this.refCount = 1;

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IStream.Interface? ToManagedObject(void* pThis) =>
            GCHandle.FromIntPtr(((nint*)pThis)[1]).Target as IStream.Interface;

        [UnmanagedCallersOnly]
        static int QueryInterfaceStatic(IStream* pThis, Guid* riid, void** ppvObject) =>
            ToManagedObject(pThis)?.QueryInterface(riid, ppvObject) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static uint AddRefStatic(IStream* pThis) => ToManagedObject(pThis)?.AddRef() ?? 0;

        [UnmanagedCallersOnly]
        static uint ReleaseStatic(IStream* pThis) => ToManagedObject(pThis)?.Release() ?? 0;

        [UnmanagedCallersOnly]
        static int ReadStatic(IStream* pThis, void* pv, uint cb, uint* pcbRead) =>
            ToManagedObject(pThis)?.Read(pv, cb, pcbRead) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int WriteStatic(IStream* pThis, void* pv, uint cb, uint* pcbWritten) =>
            ToManagedObject(pThis)?.Write(pv, cb, pcbWritten) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int SeekStatic(
            IStream* pThis, LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition) =>
            ToManagedObject(pThis)?.Seek(dlibMove, dwOrigin, plibNewPosition) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int SetSizeStatic(IStream* pThis, ULARGE_INTEGER libNewSize) =>
            ToManagedObject(pThis)?.SetSize(libNewSize) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int CopyToStatic(
            IStream* pThis, IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead,
            ULARGE_INTEGER* pcbWritten) =>
            ToManagedObject(pThis)?.CopyTo(pstm, cb, pcbRead, pcbWritten) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int CommitStatic(IStream* pThis, uint grfCommitFlags) =>
            ToManagedObject(pThis)?.Commit(grfCommitFlags) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int RevertStatic(IStream* pThis) => ToManagedObject(pThis)?.Revert() ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int LockRegionStatic(IStream* pThis, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) =>
            ToManagedObject(pThis)?.LockRegion(libOffset, cb, dwLockType) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int UnlockRegionStatic(
            IStream* pThis, ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) =>
            ToManagedObject(pThis)?.UnlockRegion(libOffset, cb, dwLockType) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int StatStatic(IStream* pThis, STATSTG* pstatstg, uint grfStatFlag) =>
            ToManagedObject(pThis)?.Stat(pstatstg, grfStatFlag) ?? E.E_FAIL;

        [UnmanagedCallersOnly]
        static int CloneStatic(IStream* pThis, IStream** ppstm) => ToManagedObject(pThis)?.Clone(ppstm) ?? E.E_FAIL;
    }

    /// <inheritdoc cref="INativeGuid.NativeGuid"/>
    public static Guid* NativeGuid => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in MyGuid));

    public static implicit operator IUnknown*(ManagedIStream mis) =>
        (IUnknown*)mis.gchComObject.AddrOfPinnedObject();

    public static implicit operator ISequentialStream*(ManagedIStream mis) =>
        (ISequentialStream*)mis.gchComObject.AddrOfPinnedObject();

    public static implicit operator IStream*(ManagedIStream mis) =>
        (IStream*)mis.gchComObject.AddrOfPinnedObject();

    /// <inheritdoc/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        if (ppvObject == null)
            return E.E_POINTER;

        if (*riid == IID.IID_IUnknown ||
            *riid == IID.IID_ISequentialStream ||
            *riid == IID.IID_IStream ||
            *riid == MyGuid)
        {
            try
            {
                this.AddRef();
            }
            catch
            {
                return E.E_FAIL;
            }

            *ppvObject = (IUnknown*)this;
            return S.S_OK;
        }

        *ppvObject = null;
        return E.E_NOINTERFACE;
    }

    /// <inheritdoc/>
    public int AddRef() => IRefCountable.AlterRefCount(1, ref this.refCount, out var newRefCount) switch
    {
        IRefCountable.RefCountResult.StillAlive => newRefCount,
        IRefCountable.RefCountResult.AlreadyDisposed => throw new ObjectDisposedException(nameof(ManagedIStream)),
        IRefCountable.RefCountResult.FinalRelease => throw new InvalidOperationException(),
        _ => throw new InvalidOperationException(),
    };

    /// <inheritdoc/>
    public int Release()
    {
        switch (IRefCountable.AlterRefCount(-1, ref this.refCount, out var newRefCount))
        {
            case IRefCountable.RefCountResult.StillAlive:
                return newRefCount;

            case IRefCountable.RefCountResult.FinalRelease:
                this.gchThis.Free();
                this.gchComObject.Free();
                this.gchVtbl.Free();
                return newRefCount;

            case IRefCountable.RefCountResult.AlreadyDisposed:
                throw new ObjectDisposedException(nameof(ManagedIStream));

            default:
                throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    uint IUnknown.Interface.AddRef()
    {
        try
        {
            return (uint)this.AddRef();
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    uint IUnknown.Interface.Release()
    {
        try
        {
            return (uint)this.Release();
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public HRESULT Read(void* pv, uint cb, uint* pcbRead)
    {
        if (pcbRead == null)
        {
            var tmp = stackalloc uint[1];
            pcbRead = tmp;
        }

        ref var read = ref *pcbRead;
        for (read = 0u; read < cb;)
        {
            var chunkSize = unchecked((int)Math.Min(0x10000000u, cb));
            var chunkRead = (uint)this.inner.Read(new(pv, chunkSize));
            if (chunkRead == 0)
                break;
            pv = (byte*)pv + chunkRead;
            read += chunkRead;
        }

        return read == cb ? S.S_OK : S.S_FALSE;
    }

    /// <inheritdoc/>
    public HRESULT Write(void* pv, uint cb, uint* pcbWritten)
    {
        if (pcbWritten == null)
        {
            var tmp = stackalloc uint[1];
            pcbWritten = tmp;
        }

        ref var written = ref *pcbWritten;
        try
        {
            for (written = 0u; written < cb;)
            {
                var chunkSize = Math.Min(0x10000000u, cb);
                this.inner.Write(new(pv, (int)chunkSize));
                pv = (byte*)pv + chunkSize;
                written += chunkSize;
            }

            return S.S_OK;
        }
        catch (Exception e) when (e.HResult == unchecked((int)(0x80070000u | ERROR.ERROR_HANDLE_DISK_FULL)))
        {
            return STG.STG_E_MEDIUMFULL;
        }
        catch (Exception e) when (e.HResult == unchecked((int)(0x80070000u | ERROR.ERROR_DISK_FULL)))
        {
            return STG.STG_E_MEDIUMFULL;
        }
        catch (IOException)
        {
            return STG.STG_E_CANTSAVE;
        }
    }

    /// <inheritdoc/>
    public HRESULT Seek(LARGE_INTEGER dlibMove, uint dwOrigin, ULARGE_INTEGER* plibNewPosition)
    {
        SeekOrigin seekOrigin;

        switch ((STREAM_SEEK)dwOrigin)
        {
            case STREAM_SEEK.STREAM_SEEK_SET:
                seekOrigin = SeekOrigin.Begin;
                break;
            case STREAM_SEEK.STREAM_SEEK_CUR:
                seekOrigin = SeekOrigin.Current;
                break;
            case STREAM_SEEK.STREAM_SEEK_END:
                seekOrigin = SeekOrigin.End;
                break;
            default:
                return STG.STG_E_INVALIDFUNCTION;
        }

        try
        {
            var position = this.inner.Seek(dlibMove.QuadPart, seekOrigin);
            if (plibNewPosition != null)
            {
                *plibNewPosition = new() { QuadPart = (ulong)position };
            }

            return S.S_OK;
        }
        catch
        {
            return STG.STG_E_INVALIDFUNCTION;
        }
    }

    /// <inheritdoc/>
    public HRESULT SetSize(ULARGE_INTEGER libNewSize)
    {
        try
        {
            this.inner.SetLength(checked((long)libNewSize.QuadPart));
            return S.S_OK;
        }
        catch (Exception e) when (e.HResult == unchecked((int)(0x80070000u | ERROR.ERROR_HANDLE_DISK_FULL)))
        {
            return STG.STG_E_MEDIUMFULL;
        }
        catch (Exception e) when (e.HResult == unchecked((int)(0x80070000u | ERROR.ERROR_DISK_FULL)))
        {
            return STG.STG_E_MEDIUMFULL;
        }
        catch (IOException)
        {
            return STG.STG_E_INVALIDFUNCTION;
        }
    }

    /// <inheritdoc/>
    public HRESULT CopyTo(IStream* pstm, ULARGE_INTEGER cb, ULARGE_INTEGER* pcbRead, ULARGE_INTEGER* pcbWritten)
    {
        if (pcbRead == null)
        {
            var temp = stackalloc ULARGE_INTEGER[1];
            pcbRead = temp;
        }

        if (pcbWritten == null)
        {
            var temp = stackalloc ULARGE_INTEGER[1];
            pcbWritten = temp;
        }

        ref var cbRead = ref pcbRead->QuadPart;
        ref var cbWritten = ref pcbWritten->QuadPart;
        cbRead = cbWritten = 0;

        var buf = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            fixed (byte* pbuf = buf)
            {
                while (cbRead < cb)
                {
                    var read = checked((uint)this.inner.Read(buf.AsSpan()));
                    if (read == 0)
                        break;
                    cbRead += read;

                    var written = 0u;
                    var writeResult = pstm->Write(pbuf, read, &written);
                    if (writeResult.FAILED)
                        return writeResult;
                    cbWritten += written;
                }
            }

            return S.S_OK;
        }
        catch (Exception e) when (e.HResult == unchecked((int)(0x80070000u | ERROR.ERROR_HANDLE_DISK_FULL)))
        {
            return STG.STG_E_MEDIUMFULL;
        }
        catch (Exception e) when (e.HResult == unchecked((int)(0x80070000u | ERROR.ERROR_DISK_FULL)))
        {
            return STG.STG_E_MEDIUMFULL;
        }
        catch (Exception e)
        {
            // Undefined return value according to the documentation, but meh
            return e.HResult < 0 ? e.HResult : E.E_FAIL;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    /// <inheritdoc/>
    // On streams open in direct mode, this method has no effect.
    public HRESULT Commit(uint grfCommitFlags) => S.S_OK;

    /// <inheritdoc/>
    // On streams open in direct mode, this method has no effect.
    public HRESULT Revert() => S.S_OK;

    /// <inheritdoc/>
    // Locking is not supported at all or the specific type of lock requested is not supported.
    public HRESULT LockRegion(ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) =>
        STG.STG_E_INVALIDFUNCTION;

    /// <inheritdoc/>
    // Locking is not supported at all or the specific type of lock requested is not supported.
    public HRESULT UnlockRegion(ULARGE_INTEGER libOffset, ULARGE_INTEGER cb, uint dwLockType) =>
        STG.STG_E_INVALIDFUNCTION;

    /// <inheritdoc/>
    public HRESULT Stat(STATSTG* pstatstg, uint grfStatFlag)
    {
        if (pstatstg is null)
            return STG.STG_E_INVALIDPOINTER;
        ref var streamStats = ref *pstatstg;
        streamStats.type = (uint)STGTY.STGTY_STREAM;
        streamStats.cbSize = (ulong)this.inner.Length;
        streamStats.grfMode = 0;
        if (this.inner.CanRead && this.inner.CanWrite)
            streamStats.grfMode |= STGM.STGM_READWRITE;
        else if (this.inner.CanRead)
            streamStats.grfMode |= STGM.STGM_READ;
        else if (this.inner.CanWrite)
            streamStats.grfMode |= STGM.STGM_WRITE;
        else
            return STG.STG_E_REVERTED;
        return S.S_OK;
    }

    /// <inheritdoc/>
    // Undefined return value according to the documentation, but meh
    public HRESULT Clone(IStream** ppstm) => E.E_NOTIMPL;
}
